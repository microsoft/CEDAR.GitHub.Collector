// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Auditing;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Collector;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class DefaultCollector : ICollector
    {
        protected ICache<PointCollectorTableEntity> PointCollectorCache { get; }
        protected readonly ITelemetryClient telemetryClient;

        public DefaultCollector(ICache<PointCollectorTableEntity> pointCollectorCache, ITelemetryClient telemetryClient)
        {
            this.PointCollectorCache = pointCollectorCache;
            this.telemetryClient = telemetryClient;
        }

        public virtual async Task ProcessWebhookPayloadAsync(JObject jsonObject, Repository repository)
        {
            repository = new Repository(repository.OrganizationId, 0, repository.OrganizationLogin, string.Empty);

            JToken organizationUrlToken = jsonObject.SelectToken("$.organization.url");
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
                };
                await PointCollector.OffloadToPointCollector(pointCollectorInput, this.PointCollectorCache, this.telemetryClient).ConfigureAwait(false);
            }
            
            JToken senderUrlToken = jsonObject.SelectToken("$.sender.url");
            if (senderUrlToken != null)
            {
                string senderUrl = senderUrlToken.Value<string>();
                PointCollectorInput pointCollectorInput = new PointCollectorInput()
                {
                    Url = senderUrl,
                    RecordType = DataContract.UserInstanceRecordType,
                    ApiName = DataContract.UsersApiName,
                    Repository = repository,
                    ResponseType = "Object",
                };
                await PointCollector.OffloadToPointCollector(pointCollectorInput, this.PointCollectorCache, this.telemetryClient).ConfigureAwait(false);
            }
        }
    }
}
