// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Context;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Collectors.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Collector;
using Microsoft.CloudMine.GitHub.Collectors.Telemetry;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class PushCollector : DefaultCollector
    {
        private const string EmptyCommitSha = "0000000000000000000000000000000000000000";

        // This can happen if the commits referenced in a push had been garbage collected (which can happen due to various reasons) and are no longer accessible through Git/Hub.
        public const string NoCommitShaFoundResponsePrefix = "No commit found for SHA: ";
        public static string NoCommitShaFoundResponse(string commitSha) => $"{NoCommitShaFoundResponsePrefix}{commitSha}";

        public const string NotFoundMessage = "Not Found";

        private readonly string apiDomain;
        private FunctionContext FunctionContext;
        private ICache<RepositoryItemTableEntity> Cache;
        private ITelemetryClient TelemetryClient;

        public PushCollector(FunctionContext functionContext,
                             ICache<RepositoryItemTableEntity> cache,
                             ICache<PointCollectorTableEntity> pointCollectorCache,
                             ITelemetryClient telemetryClient,
                             string apiDomain)
            : base(pointCollectorCache)
        {
            this.apiDomain = apiDomain;
            this.FunctionContext = functionContext;
            this.Cache = cache;
            this.TelemetryClient = telemetryClient;
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

            string collectorIdentifier = $"{this.FunctionContext.SessionId}_{jsonPath}";
            RepositoryItemTableEntity cacheEntity = await this.Cache.RetrieveAsync(new RepositoryItemTableEntity(repository, DataContract.CommitInstanceRecordType, commitSha)).ConfigureAwait(false);
            if (cacheEntity != null)
            {
                if (collectorIdentifier.Equals(cacheEntity.CollectorIdentifier))
                {
                    // This commit was processed and cached by the collector that processed its delivery. Re-process it.
                    this.TelemetryClient.TrackCollectorCacheHit(repository, recordType: DataContract.CommitInstanceRecordType, recordValue: commitSha, collectorIdentifier, cacheEntity.CollectorIdentifier, decision: "Re-collect");
                }
                else
                {
                    // This commit was processed and cached by another collector that processed the corresponding delivery. Don't process it further.
                    this.TelemetryClient.TrackCollectorCacheHit(repository, recordType: DataContract.CommitInstanceRecordType, recordValue: commitSha, collectorIdentifier, cacheEntity.CollectorIdentifier, decision: "Skip");
                    return;
                }
            }

            this.TelemetryClient.TrackCollectorCacheMiss(repository, recordType: DataContract.CommitInstanceRecordType, recordValue: commitSha);
            await this.Cache.CacheAsync(new RepositoryItemTableEntity(repository, DataContract.CommitInstanceRecordType, commitSha, collectorIdentifier)).ConfigureAwait(false);

            List<HttpResponseSignature> allowlistedResponses = new List<HttpResponseSignature>()
            {
                new HttpResponseSignature(HttpStatusCode.NotFound, NotFoundMessage),
                new HttpResponseSignature(HttpStatusCode.UnprocessableEntity, NoCommitShaFoundResponse(commitSha)),
            };

            string url = $"https://{this.apiDomain}/repos/{repository.OrganizationLogin}/{repository.RepositoryName}/commits/{commitSha}";

            PointCollectorInput pointCollectorInput = new PointCollectorInput()
            {
                Url = url,
                RecordType = DataContract.CommentInstanceRecordType,
                ApiName = DataContract.CommitInstanceApiName,
                Repository = repository,
                ResponseType = "Object",
                AllowListedResponses = allowlistedResponses
            };

            await PointCollector.OffloadToPointCollector(pointCollectorInput, this.PointCollectorCache);
        }
    }
}
