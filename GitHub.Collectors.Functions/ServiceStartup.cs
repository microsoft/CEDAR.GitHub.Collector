// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.Core.Collectors.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.CloudMine.Core.Collectors.IO;
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
            builder.Services.AddSingleton<IHttpClient, HttpClientWrapper>();
            builder.Services.AddSingleton<IAdlsClient, AdlsClientWrapper>();
            
            string settings = null;
            string settingsPath = Environment.GetEnvironmentVariable("SettingsPath");
            if (string.IsNullOrWhiteSpace(settingsPath))
            {
                settingsPath = "Settings.json";
            }
            try
            {
                settings = AzureHelpers.GetBlobContentAsync("github-settings", settingsPath).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception)
            { 
            }
            builder.Services.AddSingleton(new GitHubConfigManager(settings));
        }
    }
}