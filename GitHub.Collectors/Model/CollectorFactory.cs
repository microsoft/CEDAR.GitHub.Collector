// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Context;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Collectors.Telemetry;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using System.Collections.Generic;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class CollectorFactory
    {
        public static CollectorFactory Instance { get; } = new CollectorFactory();

        public ICollector GetCollector(string eventType,
                                       FunctionContext functionContext,
                                       IAuthentication authentication,
                                       GitHubHttpClient httpClient,
                                       List<IRecordWriter> recordWriters,
                                       ICache<RepositoryItemTableEntity> cache,
                                       ITelemetryClient telemetryClient,
                                       string apiDomain)
        {
            return eventType switch
            {
                "push" => new PushCollector(functionContext, authentication, httpClient, recordWriters, cache, telemetryClient, apiDomain),
                _ => new DefaultCollector(functionContext, authentication, httpClient, recordWriters, cache, telemetryClient),
            };
        }
    }
}
