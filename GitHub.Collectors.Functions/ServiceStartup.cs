// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.CloudMine.Core.Auditing;
using Microsoft.CloudMine.Core.Collectors.Collector;
using Microsoft.CloudMine.Core.Collectors.Config;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.Core.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

[assembly: FunctionsStartup(typeof(Microsoft.CloudMine.GitHub.Collectors.Functions.ServiceStartup))]

namespace Microsoft.CloudMine.GitHub.Collectors.Functions
{
    //TODO : add TelemetryClient in ServiceStartup so that it can be utilized before function constructor. According to Azure Function discussions, this is not trivial, so keeping this as a TODO for now. 
    public class ServiceStartup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            // Write startupcode here.            
            string settings = null;
            string settingsPath = Environment.GetEnvironmentVariable("SettingsPath");
            string storageAccountNameEnvironmentVariable = Environment.GetEnvironmentVariable("StorageAccountName");
            if (string.IsNullOrWhiteSpace(settingsPath))
            {
                settingsPath = "Settings.json";
            }
            try
            {
                settings = AzureHelpers.GetBlobContentUsingMsiAsync("github-settings", settingsPath, storageAccountNameEnvironmentVariable).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
            }
            builder.Services.AddSingleton(settings);
            builder.Services.AddSingleton<GitHubConfigManager>();
            builder.Services.AddSingleton<IHttpClient, HttpClientWrapper>();
            builder.Services.AddSingleton<IAdlsClient, AdlsClientWrapper>();
            builder.Services.AddSingleton<IQueueProcessorFactory, CustomQueueProcessorFactory>();
            builder.Services.AddSingleton<IAuditLogger, OpenTelemetryAuditLogger>();
            builder.Services.AddSingleton<ILoggerProvider, OpenTelemetryLoggerProvider>();
        }
    }
}
