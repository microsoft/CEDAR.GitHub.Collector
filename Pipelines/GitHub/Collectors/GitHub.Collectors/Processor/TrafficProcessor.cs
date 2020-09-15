// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Collector;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Collectors.Telemetry;
using Microsoft.CloudMine.GitHub.Collectors.Collector;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Processor
{
    public class TrafficProcessor
    {
        private readonly CollectorBase<GitHubCollectionNode> collector;

        private readonly List<IRecordWriter> recordWriters;

        private readonly string apiDomain;

        public TrafficProcessor(IAuthentication authentication,
                                List<IRecordWriter> recordWriters,
                                GitHubHttpClient httpClient,
                                ITelemetryClient telemetryClient,
                                string apiDomain)
        {
            this.collector = new GitHubCollector(httpClient, authentication, telemetryClient, recordWriters);
            this.recordWriters = recordWriters;
            this.apiDomain = apiDomain;
        }

        public async Task ProcessAsync(Repository repository)
        {
            Dictionary<string, JToken> additionalMetadata = new Dictionary<string, JToken>()
            {
                { "OrganizationId", repository.OrganizationId },
                { "OrganizationLogin", repository.OrganizationLogin },
                { "RepositoryId", repository.RepositoryId },
                { "RepositoryName", repository.RepositoryName },
            };

            // Referrers
            GitHubCollectionNode referrersNode = new GitHubCollectionNode()
            {
                RecordType = DataContract.ReferrerInstanceRecordType,
                ApiName = DataContract.ReferrersApiName,
                GetInitialUrl = additionalMetadata => InitialReferrersUrl(repository, this.apiDomain),
                AdditionalMetadata = additionalMetadata,
            };
            foreach(IRecordWriter recordWriter in this.recordWriters)
            {
                await recordWriter.NewOutputAsync(DataContract.ReferrerInstanceRecordType).ConfigureAwait(false);
            }
            await this.collector.ProcessAsync(referrersNode).ConfigureAwait(false);

            // Views
            GitHubCollectionNode viewsNode = new GitHubCollectionNode()
            {
                RecordType = DataContract.ViewInstanceRecordType,
                ApiName = DataContract.ViewsApiName,
                GetInitialUrl = additionalMetadata => InitialViewsUrl(repository, this.apiDomain),
                AdditionalMetadata = additionalMetadata,
                ResponseType = typeof(JObject),
            };
            foreach (IRecordWriter recordWriter in this.recordWriters)
            {
                await recordWriter.NewOutputAsync(DataContract.ViewInstanceRecordType).ConfigureAwait(false);
            }
            await this.collector.ProcessAsync(viewsNode).ConfigureAwait(false);

            // Clones
            GitHubCollectionNode clonesNode = new GitHubCollectionNode()
            {
                RecordType = DataContract.CloneInstanceRecordType,
                ApiName = DataContract.ClonesApiName,
                GetInitialUrl = additionalMetadata => InitialClonesUrl(repository, this.apiDomain),
                AdditionalMetadata = additionalMetadata,
                ResponseType = typeof(JObject),
            };
            foreach (IRecordWriter recordWriter in this.recordWriters)
            {
                await recordWriter.NewOutputAsync(DataContract.CloneInstanceRecordType).ConfigureAwait(false);
            }
            await this.collector.ProcessAsync(clonesNode).ConfigureAwait(false);

            // Paths
            GitHubCollectionNode pathsNode = new GitHubCollectionNode()
            {
                RecordType = DataContract.PathInstanceRecordType,
                ApiName = DataContract.PathsApiName,
                GetInitialUrl = additionalMetadata => InitialPathsUrl(repository, this.apiDomain),
                AdditionalMetadata = additionalMetadata,
            };
            foreach (IRecordWriter recordWriter in this.recordWriters)
            {
                await recordWriter.NewOutputAsync(DataContract.PathInstanceRecordType).ConfigureAwait(false);
            }
            await this.collector.ProcessAsync(pathsNode).ConfigureAwait(false);
        }

        // ToDo: kivancm: response returns ETag for these APIs. If we prefer to call these APIs more frequently in the future, this could be used to optimize the amount of requests.
        private static string InitialReferrersUrl(Repository repository, string apiDomain) => $"https://{apiDomain}/repos/{repository.OrganizationLogin}/{repository.RepositoryName}/traffic/popular/referrers";
        private static string InitialPathsUrl(Repository repository, string apiDomain) => $"https://{apiDomain}/repos/{repository.OrganizationLogin}/{repository.RepositoryName}/traffic/popular/paths";
        private static string InitialViewsUrl(Repository repository, string apiDomain) => $"https://{apiDomain}/repos/{repository.OrganizationLogin}/{repository.RepositoryName}/traffic/views";
        private static string InitialClonesUrl(Repository repository, string apiDomain) => $"https://{apiDomain}/repos/{repository.OrganizationLogin}/{repository.RepositoryName}/traffic/clones";
    }
}
