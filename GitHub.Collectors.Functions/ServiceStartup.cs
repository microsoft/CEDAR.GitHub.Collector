// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.Core.Collectors.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.CloudMine.Core.Collectors.IO;
using System;

[assembly: FunctionsStartup(typeof(Microsoft.CloudMine.AzureDevOps.Collectors.Functions.ServiceStartup))]

namespace Microsoft.CloudMine.AzureDevOps.Collectors.Functions
{
    //TODO : add TelemetryClient in ServiceStartup so that it can be utilized before function constructor 
    public class ServiceStartup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            // Write startupcode here.
            builder.Services.AddSingleton<IHttpClient, HttpClientWrapper>();
            builder.Services.AddSingleton<IAdlsClient, AdlsClientWrapper>();
            string settings = null;
            try
            {
                settings = AzureHelpers.GetBlobContentAsync("github-settings", "Settings.json").ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception)
            { 
            }
            builder.Services.AddSingleton(new GitHubConfigManager(settings));
        }
    }
}