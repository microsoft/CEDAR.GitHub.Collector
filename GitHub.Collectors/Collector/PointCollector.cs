﻿using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Collector;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Collectors.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Collector;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Collector
{
    public class PointCollector
    {
        private readonly CollectorBase<GitHubCollectionNode> collector;

        private readonly List<IRecordWriter> recordWriters;
        private readonly ICache<PointCollectorTableEntity> cache;
        private readonly ITelemetryClient telemetryClient;
        private readonly string apiDomain;

        public PointCollector(IAuthentication authentication,
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
            additionalMetadata.Add("OrganizationId", input.Repository.OrganizationId);
            additionalMetadata.Add("OrganizationLogin", input.Repository.OrganizationLogin);

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

            GitHubCollectionNode collectionNode = new GitHubCollectionNode()
            {
                RecordType = input.RecordType,
                ApiName = input.ApiName,
                GetInitialUrl = metadata => input.Url,
                AdditionalMetadata = additionalMetadata,
                ResponseType = responseType,
                AllowlistedResponses = input.AllowListedResponses
            };

            await this.collector.ProcessAsync(collectionNode).ConfigureAwait(false);
            PointCollectorTableEntity collectionRecord = new PointCollectorTableEntity(input.Url);
            await this.cache.CacheAsync(collectionRecord).ConfigureAwait(false);
        }

        public static async Task OffloadToPointCollector(PointCollectorInput input, ICache<PointCollectorTableEntity> pointCollectorCache)
        {
            PointCollectorTableEntity tableEntity = new PointCollectorTableEntity(input.Url);
            tableEntity = await pointCollectorCache.RetrieveAsync(tableEntity).ConfigureAwait(false);

            if (tableEntity != null && DateTimeOffset.UtcNow < tableEntity.Timestamp.AddMinutes(5))
            {
                // has been collected in last 5 minuets, skip collection
                return;
            }

            //PointCollectorInput input = new PointCollectorInput()
            //{
              //  Url = url,
                //RecordType = recordType,
                //ApiName = apiName,
                //Repository = repository,
                //ResponseType = responseType,
                //AllowListedResponses = allowListedResponses
            //};

            CloudQueue pointCloudQueue = await AzureHelpers.GetStorageQueueAsync("pointcollector").ConfigureAwait(false);
            IQueue pointQueue = new CloudQueueWrapper(pointCloudQueue);
            await pointQueue.PutObjectAsJsonStringAsync(input).ConfigureAwait(false);
        }
    }
}
