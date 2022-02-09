// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Auditing;
using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Collector;
using Microsoft.CloudMine.Core.Collectors.Error;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Collector;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Processor
{
    public class OnboardingProcessor
    {
        private readonly CollectorBase<GitHubCollectionNode> collector;

        private readonly List<IRecordWriter> recordWriters;
        private readonly ICache<OnboardingTableEntity> cache;
        private readonly IQueue onboardingQueue;
        private readonly ITelemetryClient telemetryClient;
        private readonly string apiDomain;

        public const string GitRepositoryIsEmptyMessage = "Git Repository is empty.";
        public static readonly HttpResponseSignature GitRepositoryIsEmptySignature = new HttpResponseSignature(HttpStatusCode.Conflict, GitRepositoryIsEmptyMessage);
        public const string NotFoundMessage = "Not Found";
        public static readonly HttpResponseSignature NotFoundSignature = new HttpResponseSignature(HttpStatusCode.NotFound, NotFoundMessage);

        public OnboardingProcessor(IAuthentication authentication,
                                   List<IRecordWriter> recordWriters,
                                   GitHubHttpClient httpClient,
                                   ICache<OnboardingTableEntity> cache,
                                   IQueue onboardingQueue,
                                   ITelemetryClient telemetryClient,
                                   string apiDomain)
        {
            this.collector = new GitHubCollector(httpClient, authentication, telemetryClient, recordWriters);

            this.recordWriters = recordWriters;
            this.cache = cache;
            this.onboardingQueue = onboardingQueue;
            this.telemetryClient = telemetryClient;
            this.apiDomain = apiDomain;
        }

        public Task ProcessAsync(OnboardingInput onboardingInput)
        {
            return onboardingInput.OnboardingType switch
            {
                OnboardingType.Repository => OnboardRepositoryAsync(onboardingInput),
                OnboardingType.Organization => OnboardOrganizationAsync(onboardingInput),
                _ => throw new FatalException($"Received request for unknown onboarding type: '{onboardingInput.OnboardingType}'."),
            };
        }

        private async Task OnboardOrganizationAsync(OnboardingInput onboardingInput)
        {
            Dictionary<string, JToken> additionalMetadata = new Dictionary<string, JToken>()
            {
                { "OrganizationId", onboardingInput.OrganizationId },
                { "OrganizationLogin", onboardingInput.OrganizationLogin },
            };

            // Teams and team repositories
            GitHubCollectionNode teamMembersNode = new GitHubCollectionNode()
            {
                RecordType = DataContract.TeamMemberInstanceRecordType,
                ApiName = DataContract.TeamMembersApiName,
                GetInitialUrl = additionalMetadata => InitialTeamMembersUrl(additionalMetadata["TeamId"].Value<long>(), this.apiDomain),
                AdditionalMetadata = additionalMetadata,
            };

            GitHubCollectionNode teamRepositoriesNode = new GitHubCollectionNode()
            {
                RecordType = DataContract.TeamRepositoryInstanceRecordType,
                ApiName = DataContract.TeamRepositoriesApiName,
                GetInitialUrl = additionalMetadata => InitialTeamRepositoriesUrl(onboardingInput, additionalMetadata["TeamId"].Value<long>(), this.apiDomain),
                AdditionalMetadata = additionalMetadata,
                AllowlistedResponses = new List<HttpResponseSignature>()
                {
                    NotFoundSignature
                }
            };

            GitHubCollectionNode teamsNode = new GitHubCollectionNode()
            {
                RecordType = DataContract.TeamInstanceRecordType,
                ApiName = DataContract.TeamsApiName,
                GetInitialUrl = additionalMetadata => InitialTeamsUrl(onboardingInput, this.apiDomain),
                AdditionalMetadata = additionalMetadata,
                ProduceAdditionalMetadata = record => new Dictionary<string, JToken>()
                {
                    { "TeamId", record.SelectToken("$.id").Value<long>() }
                },
                ProduceChildrenAsync = (record, metadata) => Task.FromResult(new List<CollectionNode>()
                {
                    teamMembersNode,
                    teamRepositoriesNode,
                }),
            };
            foreach(IRecordWriter recordWriter in this.recordWriters)
            {
                await recordWriter.NewOutputAsync(DataContract.TeamInstanceRecordType).ConfigureAwait(false);
            }
            await this.ProcessAndCacheBatchingRequestAsync(onboardingInput, teamsNode).ConfigureAwait(false);

            GitHubCollectionNode repositoriesNode = new GitHubCollectionNode()
            {
                RecordType = DataContract.RepositoryInstanceRecordType,
                ApiName = DataContract.RepositoriesApiName,
                GetInitialUrl = additionalMetadata => InitialRepositoriesUrl(onboardingInput.OrganizationLogin, this.apiDomain),
                AdditionalMetadata = additionalMetadata,
                ProcessRecordAsync = async record =>
                {
                    string repositoryName = record.SelectToken("$.name").Value<string>();
                    long repositoryId = record.SelectToken("$.id").Value<long>();

                    OnboardingInput repositoryOnboardingInput = new OnboardingInput()
                    {
                        IgnoreCache = onboardingInput.IgnoreCache,
                        OnboardingType = OnboardingType.Repository,
                        OrganizationId = onboardingInput.OrganizationId,
                        OrganizationLogin = onboardingInput.OrganizationLogin,
                        PersonalAccessTokenEnvironmentVariable = onboardingInput.PersonalAccessTokenEnvironmentVariable,
                        IdentityEnvironmentVariable = onboardingInput.IdentityEnvironmentVariable,
                        RepositoryId = repositoryId,
                        RepositoryName = repositoryName,
                        IgnoreCacheForApis = onboardingInput.IgnoreCacheForApis,
                    };
                    await this.onboardingQueue.PutObjectAsJsonStringAsync(repositoryOnboardingInput, TimeSpan.MaxValue).ConfigureAwait(false);
                    return new List<RecordWithContext>();
                },
            };
            foreach (IRecordWriter recordWriter in this.recordWriters)
            {
                await recordWriter.NewOutputAsync(DataContract.RepositoryInstanceRecordType).ConfigureAwait(false);
            }
            await this.collector.ProcessAsync(repositoriesNode).ConfigureAwait(false);
        }

        private async Task OnboardRepositoryAsync(OnboardingInput onboardingInput)
        {
            Dictionary<string, JToken> additionalMetadata = new Dictionary<string, JToken>()
            {
                { "OrganizationId", onboardingInput.OrganizationId },
                { "OrganizationLogin", onboardingInput.OrganizationLogin },
                { "RepositoryId", onboardingInput.RepositoryId },
                { "RepositoryName", onboardingInput.RepositoryName },
            };

            // Commits and commit comments
            GitHubCollectionNode commitsNode = new GitHubCollectionNode()
            {
                RecordType = DataContract.CommitInstanceRecordType,
                ApiName = DataContract.CommitsApiName,
                GetInitialUrl = additionalMetadata => InitialCommitsUrl(onboardingInput, this.apiDomain),
                AdditionalMetadata = additionalMetadata,
                AllowlistedResponses = new List<HttpResponseSignature>()
                {
                    GitRepositoryIsEmptySignature,
                }
            };
            foreach (IRecordWriter recordWriter in this.recordWriters)
            {
                await recordWriter.NewOutputAsync(DataContract.CommitInstanceRecordType).ConfigureAwait(false);
            }
            await this.ProcessAndCacheBatchingRequestAsync(onboardingInput, commitsNode).ConfigureAwait(false);

            GitHubCollectionNode commitCommentsNode = new GitHubCollectionNode()
            {
                RecordType = DataContract.CommentInstanceRecordType,
                ApiName = DataContract.CommentsApiName,
                GetInitialUrl = additionalMetadata => InitialCommitCommentsUrl(onboardingInput, this.apiDomain),
                AdditionalMetadata = additionalMetadata,
            };
            foreach (IRecordWriter recordWriter in this.recordWriters)
            {
                await recordWriter.NewOutputAsync(DataContract.CommentInstanceRecordType).ConfigureAwait(false);
            }
            await this.ProcessAndCacheBatchingRequestAsync(onboardingInput, commitCommentsNode).ConfigureAwait(false);

            // Pull requests, pull request reviews, and pull request review comments
            GitHubCollectionNode pullRequestsNode = new GitHubCollectionNode()
            {
                RecordType = DataContract.PullRequestInstanceRecordType,
                ApiName = DataContract.PullRequestsApiName,
                GetInitialUrl = additionalMetadata => InitialPullRequestsUrl(onboardingInput, this.apiDomain),
                AdditionalMetadata = additionalMetadata,
                ProduceAdditionalMetadata = record => new Dictionary<string, JToken>()
                {
                    { "PullNumber", record.SelectToken("$.number").Value<long>() },
                    { "PullRequestId", record.SelectToken("$.id").Value<long>() }
                },
                ProduceChildrenAsync = (record, metadata) =>
                {
                    List<CollectionNode> result = new List<CollectionNode>();
                    GitHubCollectionNode pullRequestReviewNode = new GitHubCollectionNode()
                    {
                        RecordType = DataContract.PullRequestReviewsRecordType,
                        ApiName = DataContract.PullRequestReviewsApiName,
                        GetInitialUrl = additionalMetadata => InitialPullRequestReviewsUrl(onboardingInput, additionalMetadata["PullNumber"].Value<long>(), this.apiDomain),
                        AdditionalMetadata = additionalMetadata,
                    };
                    result.Add(pullRequestReviewNode);
                    return Task.FromResult(result);
                }
            };
            foreach (IRecordWriter recordWriter in this.recordWriters)
            {
                await recordWriter.NewOutputAsync(DataContract.PullRequestInstanceRecordType).ConfigureAwait(false);
            }
            await this.ProcessAndCacheBatchingRequestAsync(onboardingInput, pullRequestsNode).ConfigureAwait(false);

            GitHubCollectionNode pullRequestCommentsNode = new GitHubCollectionNode()
            {
                RecordType = DataContract.PullRequestCommentInstanceRecordType,
                ApiName = DataContract.PullRequestCommentsApiName,
                GetInitialUrl = additionalMetadata => InitialPullRequestCommentsUrl(onboardingInput, this.apiDomain),
                AdditionalMetadata = additionalMetadata,
            };
            foreach (IRecordWriter recordWriter in this.recordWriters)
            {
                await recordWriter.NewOutputAsync(DataContract.PullRequestCommentInstanceRecordType).ConfigureAwait(false);
            }
            await this.ProcessAndCacheBatchingRequestAsync(onboardingInput, pullRequestCommentsNode).ConfigureAwait(false);

            // Issues and issue comments
            GitHubCollectionNode issuesNode = new GitHubCollectionNode()
            {
                RecordType = DataContract.IssueInstanceRecordType,
                ApiName = DataContract.IssuesApiName,
                GetInitialUrl = additionalMetadata => InitialIssuesUrl(onboardingInput, this.apiDomain),
                AdditionalMetadata = additionalMetadata,
            };
            foreach (IRecordWriter recordWriter in this.recordWriters)
            {
                await recordWriter.NewOutputAsync(DataContract.IssueInstanceRecordType).ConfigureAwait(false);
            }
            await this.ProcessAndCacheBatchingRequestAsync(onboardingInput, issuesNode).ConfigureAwait(false);

            // Uses the date 1960 as an abitrary date before any issue comments could have been created. Cannot use DateTime.minValue becasue GitHub API will throw an error
            DateTime since = new DateTime(1960, 1, 1, 1, 0, 0);
            foreach (IRecordWriter recordWriter in this.recordWriters)
            {
                await recordWriter.NewOutputAsync(DataContract.IssueCommentInstanceRecordType).ConfigureAwait(false);
            }
            GitHubCollectionNode issueCommentsNode = CreateIssueCommentCollectionNode(since, onboardingInput, additionalMetadata, this.apiDomain);
            await this.ProcessAndCacheBatchingRequestAsync(onboardingInput, issueCommentsNode).ConfigureAwait(false);

            GitHubCollectionNode milestonesNode = new GitHubCollectionNode()
            {
                RecordType = DataContract.MilestoneInstanceRecordType,
                ApiName = DataContract.MilestonesApiName,
                GetInitialUrl = additionalMetadata => InitialMilestonesUrl(onboardingInput, this.apiDomain),
                AdditionalMetadata = additionalMetadata,
            };
            foreach (IRecordWriter recordWriter in this.recordWriters)
            {
                await recordWriter.NewOutputAsync(DataContract.MilestoneInstanceRecordType).ConfigureAwait(false);
            }
            await this.ProcessAndCacheBatchingRequestAsync(onboardingInput, milestonesNode).ConfigureAwait(false);
        }

        private static GitHubCollectionNode CreateIssueCommentCollectionNode(DateTime since, OnboardingInput onboardingInput, Dictionary<string, JToken> additionalMetadata, string apiDomain)
        {
            int issueCommentCount = 0;
            GitHubCollectionNode issueCommentsNode = new GitHubCollectionNode()
            {
                RecordType = DataContract.IssueCommentInstanceRecordType,
                ApiName = DataContract.IssueCommentsApiName,
                GetInitialUrl = additionalMetadata => InitialIssueCommentsUrl(onboardingInput, since, apiDomain),
                AdditionalMetadata = additionalMetadata,
                ProduceChildrenAsync = (record, metadata) =>
                {
                    List<CollectionNode> result = new List<CollectionNode>();
                    issueCommentCount++;
                    // GitHub API can only support 400 pages with 100 issue comments on each page, after 40k comments a new query is needed to retrieve more issue comments  
                    if (issueCommentCount == 40000)
                    {
                        issueCommentCount = 0;
                        DateTime nextSince = record.SelectToken("$.created_at").Value<DateTime>().AddSeconds(1);
                        result.Add(CreateIssueCommentCollectionNode(nextSince, onboardingInput, additionalMetadata, apiDomain));
                    }
                    return Task.FromResult(result);
                }
            };
            return issueCommentsNode;
        }

        private async Task ProcessAndCacheBatchingRequestAsync(OnboardingInput onboardingInput, GitHubCollectionNode collectionRoot)
        {
            Repository repositoryDetails = onboardingInput.ToRepository();
            string apiName = collectionRoot.ApiName;

            if (!onboardingInput.IgnoreCache && !onboardingInput.IgnoreCacheForApis.Contains(apiName))
            {
                bool apiOnboarded = await this.cache.ExistsAsync(new OnboardingTableEntity(repositoryDetails, apiName)).ConfigureAwait(false);
                if (apiOnboarded)
                {
                    Dictionary<string, string> properties = new Dictionary<string, string>()
                    {
                        { "ApiName", apiName },
                    };
                    this.telemetryClient.TrackEvent("OnboardingIgnoredApi", properties);
                     
                    return;
                }
            }

            await this.collector.ProcessAsync(collectionRoot).ConfigureAwait(false);

            string outputBlobPath = RecordWriterExtensions.GetOutputPaths(recordWriters);
            OnboardingTableEntity onboardingApiRecord = new OnboardingTableEntity(repositoryDetails, apiName, blobPath: outputBlobPath, onboardedOn: DateTime.UtcNow);
            await this.cache.CacheAsync(onboardingApiRecord).ConfigureAwait(false);
        }

        public static string InitialRepositoriesUrl(string organizationLogin, string apiDomain) => $"https://{apiDomain}/orgs/{organizationLogin}/repos?per_page=100";
        private static string InitialTeamsUrl(OnboardingInput onboardingInput, string apiDomain) => $"https://{apiDomain}/orgs/{onboardingInput.OrganizationLogin}/teams?per_page=100";
        private static string InitialTeamMembersUrl(long teamId, string apiDomain) => $"https://{apiDomain}/teams/{teamId}/members?per_page=100";
        private static string InitialTeamRepositoriesUrl(OnboardingInput onboardingInput, long teamId, string apiDomain) => $"https://{apiDomain}/organizations/{onboardingInput.OrganizationId}/team/{teamId}/repos?per_page=100";
        private static string InitialMilestonesUrl(OnboardingInput onboardingInput, string apiDomain) => $"https://{apiDomain}/repos/{onboardingInput.OrganizationLogin}/{onboardingInput.RepositoryName}/milestones?state=all&per_page=100";

        // ToDo: supports "since" and "until" parameters if we want to slice the data.
        private static string InitialCommitsUrl(OnboardingInput onboardingInput, string apiDomain) => $"https://{apiDomain}/repos/{onboardingInput.OrganizationLogin}/{onboardingInput.RepositoryName}/commits?per_page=100";
        private static string InitialCommitCommentsUrl(OnboardingInput onboardingInput, string apiDomain) => $"https://{apiDomain}/repos/{onboardingInput.OrganizationLogin}/{onboardingInput.RepositoryName}/comments?per_page=50"; // Use a lower pages (50 for now) since some large repos 100 pages timeout and return 502 (Bad Gateway). Suggested by the GitHub API folks.

        private static string InitialPullRequestsUrl(OnboardingInput onboardingInput, string apiDomain) => $"https://{apiDomain}/repos/{onboardingInput.OrganizationLogin}/{onboardingInput.RepositoryName}/pulls?state=all&per_page=100";
        private static string InitialPullRequestReviewsUrl(OnboardingInput onboardingInput, long pullNumber, string apiDomain) => $"https://{apiDomain}/repos/{onboardingInput.OrganizationLogin}/{onboardingInput.RepositoryName}/pulls/{pullNumber}/reviews";
        // ToDo: supports "since" parameter if we want to slice the data.
        private static string InitialPullRequestCommentsUrl(OnboardingInput onboardingInput, string apiDomain) => $"https://{apiDomain}/repos/{onboardingInput.OrganizationLogin}/{onboardingInput.RepositoryName}/pulls/comments?per_page=50"; // Use a lower pages (50 for now) since some large repos 100 pages timeout and return 502 (Bad Gateway). Suggested by the GitHub API folks.

        // ToDo: supports "since" parameter if we want to slice the data.
        private static string InitialIssuesUrl(OnboardingInput onboardingInput, string apiDomain) => $"https://{apiDomain}/repos/{onboardingInput.OrganizationLogin}/{onboardingInput.RepositoryName}/issues?state=all&per_page=100";
        // ToDo: supports "since" parameter if we want to slice the data.
        private static string InitialIssueCommentsUrl(OnboardingInput onboardingInput, DateTime since, string apiDomain)
        {
            // The since parameter must be URL encoded or the parameter will be ignored by GitHub API
            string sinceParameter = System.Web.HttpUtility.UrlEncode(since.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            return $"https://{apiDomain}/repos/{onboardingInput.OrganizationLogin}/{onboardingInput.RepositoryName}/issues/comments?since={sinceParameter}&per_page=50&sort=created"; // Use a lower pages (50 for now) since some large repos 100 pages timeout and return 502 (Bad Gateway). Suggested by the GitHub API folks.
        }
    }
}
