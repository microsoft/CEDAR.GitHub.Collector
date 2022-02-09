// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Auditing;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Context;
using Microsoft.CloudMine.GitHub.Collectors.Cache;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class CollectorFactory
    {
        public static CollectorFactory Instance { get; } = new CollectorFactory();

        public ICollector GetCollector(string eventType,
                                       FunctionContext functionContext,
                                       ICache<RepositoryItemTableEntity> cache,
                                       ICache<PointCollectorTableEntity> pointCollectorCache,
                                       ITelemetryClient telemetryClient,
                                       string apiDomain)
        {
            return eventType switch
            {
                "push" => new PushCollector(functionContext, cache, pointCollectorCache, telemetryClient, apiDomain),
                _ => new DefaultCollector(pointCollectorCache, telemetryClient),
            };
        }
    }
}
