// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Collectors.Telemetry;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Context;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Processor
{
    public class WebHookProcessor
    {
        private readonly static int EventCountLimit;
        private const int DefaultEventCountLimit = 100;
        
        private readonly string requestBody;
        private readonly WebhookProcessorContext context;
        private readonly IAuthentication authentication;
        private readonly GitHubHttpClient httpClient;
        private readonly List<IRecordWriter> recordWriters;
        private readonly IEventsBookkeeper eventsBookkeeper;
        private readonly ICache<RecordTableEntity> recordsCache;
        private readonly ICache<RepositoryItemTableEntity> collectorCache;
        private readonly ITelemetryClient telemetryClient;
        private readonly string apiDomain;

        static WebHookProcessor()
        {
            int eventCountLimit = DefaultEventCountLimit;
            string eventCountLimitValue = Environment.GetEnvironmentVariable("EventCountLimit");
            if (!string.IsNullOrEmpty(eventCountLimitValue))
            {
                if (int.TryParse(eventCountLimitValue, out int temp))
                {
                    eventCountLimit = temp;
                }
            }

            EventCountLimit = eventCountLimit;
        }

        public WebHookProcessor(string requestBody,
                                WebhookProcessorContext context,
                                IAuthentication authentication,
                                GitHubHttpClient httpClient,
                                List<IRecordWriter> recordWriters,
                                IEventsBookkeeper eventsBookkeeper,
                                ICache<RecordTableEntity> recordsCache,
                                ICache<RepositoryItemTableEntity> collectorCache,
                                ITelemetryClient telemetryClient,
                                string apiDomain)
        {
            this.requestBody = requestBody;
            this.context = context;
            this.authentication = authentication;
            this.httpClient = httpClient;
            this.recordWriters = recordWriters;
            this.eventsBookkeeper = eventsBookkeeper;
            this.recordsCache = recordsCache;
            this.collectorCache = collectorCache;
            this.telemetryClient = telemetryClient;
            this.apiDomain = apiDomain;
        }

        public async Task<Dictionary<string, string>> ProcessAsync()
        {
            JObject record = JObject.Parse(this.requestBody);
            // remove app id installation info from record to maintain consistant data shape
            record.SelectToken("$.installation").Remove();

            // Some events e.g., "member_added", does not have "repository" as part of the Webhooks payload since they are only associated with the
            // organization and not with a specific repository. Use defaults (0 and empty string) for repository id and repository name since they are optional.
            long repositoryId = Repository.NoRepositoryId;
            string repositoryName = Repository.NoRepositoryName;

            JToken repositoryIdToken = record.SelectToken("$.repository.id");
            if (repositoryIdToken != null)
            {
                repositoryId = repositoryIdToken.Value<long>();
            }
            JToken repositoryNameToken = record.SelectToken("$.repository.name");
            if (repositoryNameToken != null)
            {
                repositoryName = repositoryNameToken.Value<string>();
            }

            // There have been GitHub payloads that are impossible to process because they are missing required attributes (e.g., organization, repository.id, etc.). 
            // Instead of failing with a cryptic Newtonsoft JSON parsing error, handle these cases more graciously and log them in telemetry for future debugging and validation.

            if (!repositoryName.Equals(Repository.NoRepositoryName) && repositoryId == Repository.NoRepositoryId)
            {
                // Case 1: there is a repository name but no repository id.
                Dictionary<string, string> properties = new Dictionary<string, string>()
                {
                    { "RequestBody", this.requestBody },
                    { "Reason", "repository.id is missing." },
                };
                this.telemetryClient.TrackEvent("UnexpectedWebhookPayload", properties);
                return new Dictionary<string, string>();
            }

            JToken organizationIdToken = record.SelectToken("$.organization.id");
            if (organizationIdToken == null)
            {
                // Case 2: organization login is missing.
                Dictionary<string, string> properties = new Dictionary<string, string>()
                {
                    { "RequestBody", this.requestBody },
                    { "Reason", "organization.id is missing." },
                };
                this.telemetryClient.TrackEvent("UnexpectedWebhookPayload", properties);
                return new Dictionary<string, string>();
            }
            long organizationId = organizationIdToken.Value<long>();

            JToken organizationLoginToken = record.SelectToken("$.organization.login");
            if (organizationLoginToken == null)
            {
                // Case 3: organization id is missing.
                Dictionary<string, string> properties = new Dictionary<string, string>()
                {
                    { "RequestBody", this.requestBody },
                    { "Reason", "organization.login is missing." },
                };
                this.telemetryClient.TrackEvent("UnexpectedWebhookPayload", properties);
                return new Dictionary<string, string>();
            }
            string organizationLogin = organizationLoginToken.Value<string>();

            Repository repository = new Repository(organizationId, repositoryId, organizationLogin, repositoryName);

            foreach(IRecordWriter recordWriter in this.recordWriters)
            {
                recordWriter.SetOutputPathPrefix($"{repository.OrganizationId}/{repository.RepositoryId}");
            }

            if (repository.IsValid())
            {
                int eventCount = await this.eventsBookkeeper.IncrementCountAsync(repository).ConfigureAwait(false);
                if (eventCount >= EventCountLimit)
                {
                    await this.eventsBookkeeper.SignalCountAsync(repository).ConfigureAwait(false);
                    await this.eventsBookkeeper.ResetCountAsync(repository).ConfigureAwait(false);
                }
            }

            string eventType = this.context.EventType;
            await this.CacheRecord(record, repository, eventType).ConfigureAwait(false);

            RecordContext recordContext = new RecordContext()
            {
                RecordType = eventType
            };

            foreach (IRecordWriter recordWriter in this.recordWriters)
            {
                await recordWriter.WriteRecordAsync(record, recordContext).ConfigureAwait(false);
            }

            ICollector collector = CollectorFactory.Instance.GetCollector(eventType, this.context, this.authentication, this.httpClient, this.recordWriters, this.collectorCache, this.telemetryClient, this.apiDomain);
            await collector.ProcessWebhookPayloadAsync(record, repository).ConfigureAwait(false);

            Dictionary<string, string> additionalSessionEndProperties = new Dictionary<string, string>(this.RetrieveAdditionalPrimaryKeys(record))
            {
                { "OrganizationLogin", repository.OrganizationLogin },
                { "OrganizationId", repository.OrganizationId.ToString() },
                { "RepositoryName", repository.RepositoryName },
                { "RepositoryId", repository.RepositoryId.ToString() },
            };

            return additionalSessionEndProperties;
        }

        private IDictionary<string, string> RetrieveAdditionalPrimaryKeys(JObject record)
        {
            try
            {
                return this.context.EventType switch
                {
                    "issues" => new Dictionary<string, string>()
                    {
                        { "IssueNumber", record.SelectToken("$.issue.number").Value<long>().ToString() },
                    },
                    "issue_comment" => new Dictionary<string, string>()
                    {
                        { "IssueNumber", record.SelectToken("$.issue.number").Value<long>().ToString() },
                        { "CommentId", record.SelectToken("$.comment.id").Value<long>().ToString() },
                    },
                    "pull_request" => new Dictionary<string, string>()
                    {
                        { "PullRequestNumber", record.SelectToken("$.pull_request.number").Value<long>().ToString() },
                    },
                    "pull_request_review_comment" => new Dictionary<string, string>()
                    {
                        { "PullRequestNumber", record.SelectToken("$.pull_request.number").Value<long>().ToString() },
                        { "CommentId", record.SelectToken("$.comment.id").Value<long>().ToString() },
                    },
                    _ => new Dictionary<string, string>(),
                };
            }
            catch (Exception exception)
            {
                // Done as best effort, don't fail processing for this. Just log in telemetry.
                Dictionary<string, string> properties = new Dictionary<string, string>()
                {
                    { "Fatal", false.ToString() },
                };
                this.telemetryClient.TrackException(exception, "Failed to retrieve additional primary keys.");
            }

            return new Dictionary<string, string>();
        }

        private Task CacheRecord(JObject record, Repository repository, string eventType)
        {
            if (eventType.Equals("ping"))
            {
                // Very first time a Webhook is setup, GitHub sends a special event called "ping". This event does not have any metadata the collectors need and neither important for data processing. Ignore.
                return Task.CompletedTask;
            }

            IHasher eventHasher = PayloadHasherFactory.Instance.GetEventHasher(eventType, this.telemetryClient);
            string recordSha = eventHasher.ComputeSha256Hash(record, repository);

            Dictionary<string, string> properties = new Dictionary<string, string>()
            {
                { "RecordSha", recordSha }
            };
            this.telemetryClient.TrackEvent("CachedWebhookPayload", properties);

            RecordTableEntity recordTableEntity = new RecordTableEntity(repository, recordType: this.context.EventType, recordSha, context.SessionId);
            return this.recordsCache.CacheAsync(recordTableEntity);
        }
    }
}
