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
            foreach (KeyValuePair<string, JToken> contextElement in input.Context)
            {
                additionalMetadata.Add(contextElement.Key, contextElement.Value);
            }

            Type responseType = typeof(JArray);
            if( input.ResponseType.Equals("Object") )
            {
                responseType = typeof(JObject);
            }

            GitHubCollectionNode collectionNode = new GitHubCollectionNode()
            {
                RecordType = input.RecordType,
                ApiName = input.ApiName,
                GetInitialUrl = metadata => input.Url,
                AdditionalMetadata = additionalMetadata,
                ResponseType = responseType
            };

            await this.collector.ProcessAsync(collectionNode).ConfigureAwait(false);
            PointCollectorTableEntity collectionRecord = new PointCollectorTableEntity(input.Url);
            await this.cache.CacheAsync(collectionRecord).ConfigureAwait(false);
        }
    }
}
