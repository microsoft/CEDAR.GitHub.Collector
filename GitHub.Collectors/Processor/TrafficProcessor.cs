// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Telemetry;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Collector;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Processor
{
    public class TrafficProcessor
    {
        private readonly ITelemetryClient telemetryClient;
        private readonly ICache<PointCollectorTableEntity> pointCollectorCache;
        private readonly string apiDomain;

        public TrafficProcessor(ITelemetryClient telemetryClient,
                                ICache<PointCollectorTableEntity> pointCollectorCache,
                                string apiDomain)
        {
            this.telemetryClient = telemetryClient;
            this.pointCollectorCache = pointCollectorCache;
            this.apiDomain = apiDomain;
        }

        public async Task ProcessAsync(Repository repository)
        {
            // Referrers
            PointCollectorInput input = new PointCollectorInput()
            {
                Url = InitialReferrersUrl(repository, this.apiDomain),
                RecordType = DataContract.ReferrerInstanceRecordType,
                ApiName = DataContract.ReferrersApiName,
                Repository = repository,
                ResponseType = "Array",
            };
            await PointCollector.OffloadToPointCollector(input, this.pointCollectorCache, this.telemetryClient);


            // Views
            input = new PointCollectorInput()
            {
                Url = InitialViewsUrl(repository, this.apiDomain),
                RecordType = DataContract.ViewInstanceRecordType,
                ApiName = DataContract.ViewsApiName,
                Repository = repository,
                ResponseType = "Object",
            };
            await PointCollector.OffloadToPointCollector(input, this.pointCollectorCache, this.telemetryClient);

            // Clones
            input = new PointCollectorInput()
            {
                Url = InitialClonesUrl(repository, this.apiDomain),
                RecordType = DataContract.CloneInstanceRecordType,
                ApiName = DataContract.ClonesApiName,
                Repository = repository,
                ResponseType = "Object",
            };
            await PointCollector.OffloadToPointCollector(input, this.pointCollectorCache, this.telemetryClient);

            // Paths
            input = new PointCollectorInput()
            {
                Url = InitialPathsUrl(repository, this.apiDomain),
                RecordType = DataContract.PathInstanceRecordType,
                ApiName = DataContract.PathsApiName,
                Repository = repository,
                ResponseType = "Array",
            };
            await PointCollector.OffloadToPointCollector(input, this.pointCollectorCache, this.telemetryClient);
        }

        // ToDo: kivancm: response returns ETag for these APIs. If we prefer to call these APIs more frequently in the future, this could be used to optimize the amount of requests.
        private static string InitialReferrersUrl(Repository repository, string apiDomain) => $"https://{apiDomain}/repos/{repository.OrganizationLogin}/{repository.RepositoryName}/traffic/popular/referrers";
        private static string InitialPathsUrl(Repository repository, string apiDomain) => $"https://{apiDomain}/repos/{repository.OrganizationLogin}/{repository.RepositoryName}/traffic/popular/paths";
        private static string InitialViewsUrl(Repository repository, string apiDomain) => $"https://{apiDomain}/repos/{repository.OrganizationLogin}/{repository.RepositoryName}/traffic/views";
        private static string InitialClonesUrl(Repository repository, string apiDomain) => $"https://{apiDomain}/repos/{repository.OrganizationLogin}/{repository.RepositoryName}/traffic/clones";
    }
}
