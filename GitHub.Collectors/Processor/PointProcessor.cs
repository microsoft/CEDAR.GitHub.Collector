using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Collector;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Collectors.Telemetry;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Collector;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Processor
{
    public class PointProcessor
    {
        private readonly CollectorBase<GitHubCollectionNode> collector;

        private readonly List<IRecordWriter> recordWriters;
        private readonly ICache<PointCollectorTableEntity> cache;
        private readonly ITelemetryClient telemetryClient;
        private readonly string apiDomain;

        public PointProcessor(IAuthentication authentication,
                              List<IRecordWriter> recordWriters,
                              GitHubHttpClient httpClient,
                              ICache<PointCollectorTableEntity> cache,
                              ITelemetryClient telemetryClient,
                              string apiDomain)
        {
            this.collector = new GitHubCollector(httpClient, authentication, telemetryClient, recordWriters);
            this.recordWriters = recordWriters;
            this.cache = cache;
            this.telemetryClient = telemetryClient;
            this.apiDomain = apiDomain;
        }

        public async Task ProcessAsync(PointCollectorInput input)
        {
            Dictionary<string, JToken> additionalMetadata = new Dictionary<string, JToken>();
            JObject context = input.Context;
            if (context != null)
            {
                foreach (KeyValuePair<string, JToken> contextElement in context)
                {
                    additionalMetadata.Add(contextElement.Key, contextElement.Value);
                }
            }
    
            GitHubCollectionNode collectionNode = new GitHubCollectionNode()
            {
                RecordType = input.RecordType,
                ApiName = input.ApiName,
                GetInitialUrl = metadata => input.Url,
                AdditionalMetadata = additionalMetadata,
            };

            foreach (IRecordWriter recordWriter in this.recordWriters)
            {
                await recordWriter.NewOutputAsync(input.RecordType).ConfigureAwait(false);
            }
            await this.ProcessAndCacheBatchingRequestAsync(input, collectionNode).ConfigureAwait(false);
        }

        private async Task ProcessAndCacheBatchingRequestAsync(PointCollectorInput input, GitHubCollectionNode collectionNode)
        {
            string apiName = collectionNode.ApiName;
            if (!input.IgnoreCache && !input.IgnoreCacheForApis.Contains(apiName))
            {
                // PointCollectorTableEntity tableEntity = await this.cache.RetrieveAsync(new PointCollectorTableEntity(input));
                // TODO Logic for skipping if in cache goes here
                bool apiCollected = false;
                if (apiCollected)
                {
                    // TODO track event in telemetry
                    return;
                }
            }
            await this.collector.ProcessAsync(collectionNode).ConfigureAwait(false);
            PointCollectorTableEntity pointCollectorRecord = new PointCollectorTableEntity(input);
            await this.cache.CacheAsync(pointCollectorRecord).ConfigureAwait(false);
        }
    }
}
