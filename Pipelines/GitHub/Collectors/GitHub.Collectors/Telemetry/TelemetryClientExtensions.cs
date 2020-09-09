// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.CloudMine.Core.Collectors.Telemetry;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using System.Collections.Generic;

namespace Microsoft.CloudMine.GitHub.Collectors.Telemetry
{
    public static class TelemetryClientExtensions
    {
        public static void TrackCollectorCacheHit(this ITelemetryClient telemetryClient, Repository repository, string recordType, string recordValue, string currentCollectorIdentifier, string cachingCollectorIdentifier, string decision)
        {
            Dictionary<string, string> properties = new Dictionary<string, string>()
            {
                { "Decision", decision },
                { "CurrentCollectorIdentifier", currentCollectorIdentifier },
                { "CachingCollectorIdentifier", cachingCollectorIdentifier },
                { "RecordType", recordType },
                { "RecordValue", recordValue },
                { "RepositoryName", repository.RepositoryName },
                { "RepositoryId", repository.RepositoryId.ToString() },
                { "OrganizationLogin", repository.OrganizationLogin },
                { "OrganizationId", repository.OrganizationId.ToString() },
            };
            telemetryClient.TrackEvent("CollectorCacheHit", properties);
        }

        public static void TrackCollectorCacheMiss(this ITelemetryClient telemetryClient, Repository repository, string recordType, string recordValue)
        {
            Dictionary<string, string> properties = new Dictionary<string, string>()
            {
                { "RecordType", recordType },
                { "RecordValue", recordValue },
                { "RepositoryName", repository.RepositoryName },
                { "RepositoryId", repository.RepositoryId.ToString() },
                { "OrganizationLogin", repository.OrganizationLogin },
                { "OrganizationId", repository.OrganizationId.ToString() },
            };
            telemetryClient.TrackEvent("CollectorCacheMiss", properties);
        }
    }
}
