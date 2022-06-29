using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Collector;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Collector
{
    public class PointCollector
    {
        private readonly CollectorBase<GitHubCollectionNode> collector;
        private readonly ICache<PointCollectorTableEntity> cache;
        private static string storageAccountNameEnvironmentVariable;
        public PointCollector(IAuthentication authentication,
                              List<IRecordWriter> recordWriters,
                              GitHubHttpClient httpClient,
                              ICache<PointCollectorTableEntity> cache,
                              ITelemetryClient telemetryClient)
        {
            this.collector = new GitHubCollector(httpClient, authentication, telemetryClient, recordWriters);
            this.cache = cache;
            storageAccountNameEnvironmentVariable = Environment.GetEnvironmentVariable("StorageAccountName");
        }

        public async Task ProcessAsync(PointCollectorInput input)
        {
            Dictionary<string, JToken> additionalMetadata = new Dictionary<string, JToken>()
            {
                { "OrganizationId", input.Repository.OrganizationId },
                { "OrganizationLogin", input.Repository.OrganizationLogin }
            };

            if (input.Repository.RepositoryId != 0)
            {
                additionalMetadata.Add("RepositoryId", input.Repository.RepositoryId);
                additionalMetadata.Add("RepositoryName", input.Repository.RepositoryName);
            }

            Type responseType = typeof(JArray);

            if( input.ResponseType.Equals("Object") )
            {
                responseType = typeof(JObject);
            }

            List<HttpResponseSignature> allowListedResponses = new List<HttpResponseSignature>();

            switch (input.RecordType)
            {
                case DataContract.UserInstanceRecordType :
                    allowListedResponses.Add(new HttpResponseSignature(HttpStatusCode.NotFound, "Not Found"));
                    allowListedResponses.Add(new HttpResponseSignature(HttpStatusCode.Forbidden, "Resource not accessible by integration"));
                    break;
                case DataContract.CommentInstanceRecordType :
                    allowListedResponses.Add(new HttpResponseSignature(HttpStatusCode.NotFound, "Not Found"));
                    allowListedResponses.Add(new HttpResponseSignature(HttpStatusCode.UnprocessableEntity, "No commit found for SHA: [0-9a-f]*"));
                    break;
            }

            GitHubCollectionNode collectionNode = new GitHubCollectionNode()
            {
                RecordType = input.RecordType,
                ApiName = input.ApiName,
                GetInitialUrl = metadata => input.Url,
                AdditionalMetadata = additionalMetadata,
                ResponseType = responseType,
                AllowlistedResponses = allowListedResponses,
            };

            await this.collector.ProcessAsync(collectionNode).ConfigureAwait(false);
        }

        public static async Task OffloadToPointCollector(PointCollectorInput input, ICache<PointCollectorTableEntity> pointCollectorCache, ITelemetryClient telemetryClient)
        {
            PointCollectorTableEntity tableEntity = new PointCollectorTableEntity(input.Url);
            tableEntity = await pointCollectorCache.RetrieveAsync(tableEntity).ConfigureAwait(false);
            Dictionary<string, string> properties;
            if (tableEntity != null && DateTimeOffset.UtcNow < tableEntity.Timestamp.AddMinutes(5))
            {
                // has been collected in last 5 minuets, skip collection
                properties = new Dictionary<string, string>()
                {
                    {"Url", input.Url },
                    {"Skipped", "True" },
                };
                telemetryClient.TrackEvent("GitHubPointCollectorCache", properties);
                return;
            }

            properties = new Dictionary<string, string>()
            {
                {"Url", input.Url },
                {"Skipped", "False" },
            };
            telemetryClient.TrackEvent("GitHubPointCollectorCache", properties);
            CloudQueue pointCloudQueue = await AzureHelpers.GetStorageQueueUsingMsiAsync("pointcollector", storageAccountNameEnvironmentVariable, telemetryClient).ConfigureAwait(false);
            IQueue pointQueue = new CloudQueueMsiWrapper(pointCloudQueue, storageAccountNameEnvironmentVariable, telemetryClient);
            await pointQueue.PutObjectAsJsonStringAsync(input).ConfigureAwait(false);
            PointCollectorTableEntity collectionRecord = new PointCollectorTableEntity(input.Url);
            await pointCollectorCache.CacheAsync(collectionRecord).ConfigureAwait(false);
        }
    }
}
