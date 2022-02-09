// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Auditing;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Context;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Collector;
using Microsoft.CloudMine.GitHub.Collectors.Telemetry;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class PushCollector : DefaultCollector
    {
        private const string EmptyCommitSha = "0000000000000000000000000000000000000000";

        public const string NoCommitShaFoundResponsePrefix = "No commit found for SHA: ";
        public const string NotFoundMessage = "Not Found";

        public static string NoCommitShaFoundResponse(string commitSha) => $"{NoCommitShaFoundResponsePrefix}{commitSha}";
        
        private readonly string apiDomain;
        private readonly FunctionContext functionContext;
        private readonly ICache<RepositoryItemTableEntity> cache;

        public PushCollector(FunctionContext functionContext,
                             ICache<RepositoryItemTableEntity> cache,
                             ICache<PointCollectorTableEntity> pointCollectorCache,
                             ITelemetryClient telemetryClient,
                             string apiDomain)
            : base(pointCollectorCache, telemetryClient)
        {
            this.apiDomain = apiDomain;
            this.functionContext = functionContext;
            this.cache = cache;
        }

        public override async Task ProcessWebhookPayloadAsync(JObject jsonObject, Repository repository)
        {
            await base.ProcessWebhookPayloadAsync(jsonObject, repository).ConfigureAwait(false);

            string beforeSha = jsonObject.SelectToken("$.before").Value<string>();
            await this.ProcessCommitSha(beforeSha, repository, "$.before").ConfigureAwait(false);
            string afterSha = jsonObject.SelectToken("$.after").Value<string>();
            await this.ProcessCommitSha(afterSha, repository, "$.after").ConfigureAwait(false);

            // $.head_commit can be null (e.g., when $.after is EmptyCommitSha)
            JToken headCommitShaToken = jsonObject.SelectToken("$.head_commit.id");
            if (headCommitShaToken != null)
            {
                string headCommitSha = headCommitShaToken.Value<string>();
                await this.ProcessCommitSha(headCommitSha, repository, "$.head_commit.id").ConfigureAwait(false);
            }

            JToken commitsToken = jsonObject.SelectToken("$.commits");
            int counter = 0;
            foreach (JToken commitToken in commitsToken)
            {
                string commitSha = commitToken.SelectToken("$.id").Value<string>();
                await this.ProcessCommitSha(commitSha, repository, $"$.commits[{counter}].id").ConfigureAwait(false);
                counter++;
            }

            await base.ProcessWebhookPayloadAsync(jsonObject, repository).ConfigureAwait(false);
        }

        private async Task ProcessCommitSha(string commitSha, Repository repository, string jsonPath)
        {
            if (EmptyCommitSha.Equals(commitSha))
            {
                return;
            }

            string collectorIdentifier = $"{this.functionContext.SessionId}_{jsonPath}";
            RepositoryItemTableEntity cacheEntity = await this.cache.RetrieveAsync(new RepositoryItemTableEntity(repository, DataContract.CommitInstanceRecordType, commitSha)).ConfigureAwait(false);
            if (cacheEntity != null)
            {
                if (collectorIdentifier.Equals(cacheEntity.CollectorIdentifier))
                {
                    // This commit was processed and cached by the collector that processed its delivery. Re-process it.
                    this.telemetryClient.TrackCollectorCacheHit(repository, recordType: DataContract.CommitInstanceRecordType, recordValue: commitSha, collectorIdentifier, cacheEntity.CollectorIdentifier, decision: "Re-collect");
                }
                else
                {
                    // This commit was processed and cached by another collector that processed the corresponding delivery. Don't process it further.
                    this.telemetryClient.TrackCollectorCacheHit(repository, recordType: DataContract.CommitInstanceRecordType, recordValue: commitSha, collectorIdentifier, cacheEntity.CollectorIdentifier, decision: "Skip");
                    return;
                }
            }

            this.telemetryClient.TrackCollectorCacheMiss(repository, recordType: DataContract.CommitInstanceRecordType, recordValue: commitSha);
            await this.cache.CacheAsync(new RepositoryItemTableEntity(repository, DataContract.CommitInstanceRecordType, commitSha, collectorIdentifier)).ConfigureAwait(false);

            string url = $"https://{this.apiDomain}/repos/{repository.OrganizationLogin}/{repository.RepositoryName}/commits/{commitSha}";

            PointCollectorInput pointCollectorInput = new PointCollectorInput()
            {
                Url = url,
                RecordType = DataContract.CommitInstanceRecordType,
                ApiName = DataContract.CommitInstanceApiName,
                Repository = repository,
                ResponseType = "Object",
            };

            await PointCollector.OffloadToPointCollector(pointCollectorInput, this.PointCollectorCache, this.telemetryClient);
        }
    }
}
