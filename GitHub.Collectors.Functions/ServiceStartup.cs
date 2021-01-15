// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.Core.Collectors.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.CloudMine.Core.Collectors.IO;
using System;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Threading;
using System.Globalization;
using Microsoft.WindowsAzure.Storage;

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
            // The default queue processor in Azure Functions move the poison messages to the poison queue with the default time-to-live, which is 7 days and this is not configurable.
            // We want to have infinite time to live for such messages and therefore implement our own custom queue processor (and queue processor factory)
            builder.Services.AddSingleton<IQueueProcessorFactory, CustomQueueProcessorFactory>();
        }
    }

    public class CustomQueueProcessorFactory : IQueueProcessorFactory
    {
        public QueueProcessor Create(QueueProcessorFactoryContext context)
        {
            return new CustomQueueProcessor(context);
        }
    }

    public class CustomQueueProcessor : QueueProcessor
    {
        private readonly ILogger _logger;

        public CustomQueueProcessor(QueueProcessorFactoryContext context) 
            : base(context)
        {
            _logger = context.Logger;
        }

        /// <summary>
        /// Base implementation is taken from the DefaultQueueProcessor:
        /// https://github.com/Azure/azure-webjobs-sdk/blob/50df9323e730c62207b85273712081cf9803f8c2/src/Microsoft.Azure.WebJobs.Extensions.Storage/Queues/QueueProcessor.cs#L161
        /// </summary>
        protected override async Task CopyMessageToPoisonQueueAsync(CloudQueueMessage message, CloudQueue poisonQueue, CancellationToken cancellationToken)
        {
            string msg = string.Format(CultureInfo.InvariantCulture, "Message has reached MaxDequeueCount of {0}. Moving message to queue '{1}'.", MaxDequeueCount, poisonQueue.Name);
            _logger?.LogWarning(msg);

            try 
            {
                await poisonQueue.CreateIfNotExistsAsync().ConfigureAwait(false);
            }
            catch
            {
                // Do this as a best-effort. This can fail e.g., due to multiple functions trying to do this at the same time.
            }
            await poisonQueue.AddMessageAsync(message, timeToLive: TimeSpan.MaxValue, null, new QueueRequestOptions(), new OperationContext()).ConfigureAwait(false);

            var eventArgs = new PoisonMessageEventArgs(message, poisonQueue);
            OnMessageAddedToPoisonQueue(eventArgs);
        }
    }
}