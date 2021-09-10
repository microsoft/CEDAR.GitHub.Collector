// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Context;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Collectors.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class DefaultCollector : ICollector
    {
        public static readonly HttpResponseSignature UserNotFoundResponse = new HttpResponseSignature(HttpStatusCode.NotFound, "Not Found");
        public static readonly HttpResponseSignature ResourceNotAccessibleByIntegrationResponse = new HttpResponseSignature(HttpStatusCode.Forbidden, "Resource not accessible by integration");

        protected FunctionContext FunctionContext { get; private set; }
        protected GitHubHttpClient HttpClient { get; private set; }
        protected List<IRecordWriter> RecordWriters { get; private set; }
        protected ICache<RepositoryItemTableEntity> Cache { get; private set; }
        protected ITelemetryClient TelemetryClient { get; private set; }
        protected IAuthentication Authentication { get; private set; }

        public DefaultCollector(FunctionContext functionContext,
                                IAuthentication authentication,
                                GitHubHttpClient httpClient,
                                List<IRecordWriter> recordWriters,
                                ICache<RepositoryItemTableEntity> cache,
                                ITelemetryClient telemetryClient)
        {
            this.FunctionContext = functionContext;
            this.HttpClient = httpClient;
            this.RecordWriters = recordWriters;
            this.Cache = cache;
            this.TelemetryClient = telemetryClient;
            this.Authentication = authentication;
        }

        public virtual async Task ProcessWebhookPayloadAsync(JObject jsonObject, Repository repository)
        {
            JToken organizationUrlToken = jsonObject.SelectToken($"$.organization.url");
            if (organizationUrlToken != null)
            {
                string organizationUrl = organizationUrlToken.Value<string>();
                await this.OffloadToPointCollector(organizationUrl, DataContract.OrganizationInstanceRecordType, DataContract.OrganizationsApiName, repository, "Object").ConfigureAwait(false);
            }
            

            JToken senderUrlToken = jsonObject.SelectToken($"$.sender.url");
            if (senderUrlToken != null)
            {
                string senderUrl = senderUrlToken.Value<string>();
                await this.OffloadToPointCollector(senderUrl, DataContract.UserInstanceRecordType, DataContract.UsersApiName, repository, "Object").ConfigureAwait(false);
            }
        }

        private async Task OffloadToPointCollector(string url, string recordType, string apiName, Repository repository, string responseType = "Array")
        {
            ICache<PointCollectorTableEntity> pointCache = new AzureTableCache<PointCollectorTableEntity>(this.TelemetryClient, "point");
            await pointCache.InitializeAsync().ConfigureAwait(false);
            PointCollectorTableEntity tableEntity = new PointCollectorTableEntity(url);
            tableEntity = await pointCache.RetrieveAsync(tableEntity).ConfigureAwait(false);

            if (tableEntity != null && DateTimeOffset.UtcNow < tableEntity.Timestamp.AddMinutes(5))
            {
                // has been collected in last 5 minuets, skip collection
                return;
            }

            Dictionary<string, JToken> context = new Dictionary<string, JToken>()
            {
                { "OrganizationLogin", JToken.FromObject(repository.OrganizationLogin) },
                { "OrganizationId", JToken.FromObject(repository.OrganizationId) },
                { "RepositoryId", JToken.FromObject(repository.RepositoryId) },
                { "RepositoryName", JToken.FromObject(repository.RepositoryName) }
            };

            PointCollectorInput input = new PointCollectorInput()
            {
                Url = url,
                RecordType = recordType,
                ApiName = apiName,
                Context = context,
                ResponseType = responseType
            };

            string queueItem = JsonConvert.SerializeObject(input);
            CloudQueue trafficCloudQueue = await AzureHelpers.GetStorageQueueAsync("pointcollector").ConfigureAwait(false);
            await trafficCloudQueue.AddMessageAsync(new CloudQueueMessage(queueItem), null, null, new QueueRequestOptions(), new OperationContext()).ConfigureAwait(false);
        }
    }
}
