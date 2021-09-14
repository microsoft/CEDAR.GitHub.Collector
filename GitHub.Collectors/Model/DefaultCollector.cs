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

        protected ICache<PointCollectorTableEntity> PointCollectorCache { get; }

        public DefaultCollector(ICache<PointCollectorTableEntity> pointCollectorCache)
        {
            this.PointCollectorCache = pointCollectorCache;
        }

        public virtual async Task ProcessWebhookPayloadAsync(JObject jsonObject, Repository repository)
        {
            repository = new Repository(repository.OrganizationId, 0, repository.OrganizationLogin, string.Empty);

            JToken organizationUrlToken = jsonObject.SelectToken($"$.organization.url");
            if (organizationUrlToken != null)
            {
                string organizationUrl = organizationUrlToken.Value<string>();
                PointCollectorInput pointCollectorInput = new PointCollectorInput()
                {
                    Url = organizationUrl,
                    RecordType = DataContract.OrganizationInstanceRecordType,
                    ApiName = DataContract.OrganizationsApiName,
                    Repository = repository,
                    ResponseType = "Object",
                    AllowListedResponses = new List<HttpResponseSignature>()
                };
                await PointCollector.OffloadToPointCollector(pointCollectorInput, this.PointCollectorCache).ConfigureAwait(false);
            }
            

            JToken senderUrlToken = jsonObject.SelectToken($"$.sender.url");
            if (senderUrlToken != null)
            {
                string senderUrl = senderUrlToken.Value<string>();
                List<HttpResponseSignature> allowlistedResponses = new List<HttpResponseSignature>()
                {
                    UserNotFoundResponse,
                    ResourceNotAccessibleByIntegrationResponse,
                };
                PointCollectorInput pointCollectorInput = new PointCollectorInput()
                {
                    Url = senderUrl,
                    RecordType = DataContract.UserInstanceRecordType,
                    ApiName = DataContract.UsersApiName,
                    Repository = repository,
                    ResponseType = "Object",
                    AllowListedResponses = allowlistedResponses
                };
                await PointCollector.OffloadToPointCollector(pointCollectorInput, this.PointCollectorCache).ConfigureAwait(false);
            }
        }
    }
}
