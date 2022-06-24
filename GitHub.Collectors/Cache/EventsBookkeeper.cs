// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Telemetry;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Cache
{
    public class EventsBookkeeper : IEventsBookkeeper
    {
        private readonly ITelemetryClient telemetryClient;
        private readonly string storageAccountNameEnvironmentVariable;
        private CloudTable table;
        private CloudQueue queue;
        private bool initialized;

        public EventsBookkeeper(ITelemetryClient telemetryClient)
        {
            this.telemetryClient = telemetryClient;
            this.initialized = false;
            this.storageAccountNameEnvironmentVariable = Environment.GetEnvironmentVariable("StorageAccountName");
        }

        public async Task InitializeAsync()
        {
            if (this.initialized)
            {
                return;
            }

            this.table = await AzureHelpers.GetStorageTableAsync("eventstats").ConfigureAwait(false);
            this.queue = await AzureHelpers.GetStorageQueueUsingMsiAsync("eventstats", storageAccountNameEnvironmentVariable).ConfigureAwait(false);

            this.initialized = true;
        }

        public Task SignalCountAsync(Repository eventStats)
        {
            string message = JsonConvert.SerializeObject(eventStats, Formatting.None);
            return AddQueueMessageAsync(new CloudQueueMessage(message));
        }

        private async Task AddQueueMessageAsync(CloudQueueMessage message)
        {
            CloudQueue queue = await AzureHelpers.GetStorageQueueUsingMsiAsync("eventstats", storageAccountNameEnvironmentVariable).ConfigureAwait(false);
            await queue.AddMessageAsync(message).ConfigureAwait(false);
        }

        public Task ResetCountAsync(Repository repository)
        {
            TableOperation replaceOperation = TableOperation.Replace(new EventStatsTableEntity(repository, 0, eTag: "*"));
            return this.table.ExecuteAsync(replaceOperation);
        }

        public async Task<int> IncrementCountAsync(Repository repository)
        {
            string partitionKey = $"{repository.OrganizationId}_{repository.RepositoryId}";
            string rowKey = string.Empty;
            TableOperation retrieveOperation = TableOperation.Retrieve<EventStatsTableEntity>(partitionKey, rowKey);
            try
            {
                TableResult retrieveResult = await this.table.ExecuteAsync(retrieveOperation).ConfigureAwait(false);
                int retrieveStatusCode = retrieveResult.HttpStatusCode;
                if (retrieveStatusCode == 404) // Not found
                {
                    TableOperation insertOperation = TableOperation.Insert(new EventStatsTableEntity(repository, eventCount: 1));
                    try
                    {
                        await this.table.ExecuteAsync(insertOperation).ConfigureAwait(false);
                        return 1;
                    }
                    catch (Exception insertException) when (insertException is StorageException)
                    {
                        StorageException insertStorageException = (StorageException)insertException;
                        int insertStatusCode = insertStorageException.RequestInformation.HttpStatusCode;
                        if (insertStatusCode == 409) // Conflict
                        {
                            // We were too late to insert, so someone else did it. Retry.
                            await this.IncrementCountAsync(repository).ConfigureAwait(false);
                        }
                        else
                        {
                            Dictionary<string, string> properties = new Dictionary<string, string>()
                            {
                                { "OrganizationLogin", repository.OrganizationLogin },
                                { "RepositoryName", repository.RepositoryName },
                                { "ErrorMessage", insertStorageException.Message },
                                { "ErrorReturnCode", insertStatusCode.ToString() },
                            };
                            this.telemetryClient.TrackEvent("BookkeepingError", properties);
                        }
                    }
                }
                else if (retrieveStatusCode == 200)
                {
                    EventStatsTableEntity retrieveStats = (EventStatsTableEntity)retrieveResult.Result;
                    string retrieveETag = retrieveResult.Etag;

                    int currentEventCount = retrieveStats.EventCount;
                    TableOperation replaceOperation = TableOperation.Replace(new EventStatsTableEntity(repository, currentEventCount + 1, retrieveETag));
                    try
                    {
                        await this.table.ExecuteAsync(replaceOperation).ConfigureAwait(false);
                        return currentEventCount + 1;
                    }
                    catch (Exception replaceException) when (replaceException is StorageException)
                    {
                        StorageException replaceStorageException = (StorageException)replaceException;
                        int replaceStatusCode = replaceStorageException.RequestInformation.HttpStatusCode;
                        if (replaceStatusCode == 412) // Pre-condition failed
                        {
                            // We were too late to update, so someone else did it. Retry.
                            return await this.IncrementCountAsync(repository).ConfigureAwait(false);
                        }
                        else
                        {
                            Dictionary<string, string> properties = new Dictionary<string, string>()
                            {
                                { "OrganizationLogin", repository.OrganizationLogin },
                                { "RepositoryName", repository.RepositoryName },
                                { "ErrorMessage", replaceException.Message },
                                { "ErrorReturnCode", replaceStatusCode.ToString() },
                            };
                            this.telemetryClient.TrackEvent("BookkeepingError", properties);
                        }
                    }
                }
                else
                {
                    Dictionary<string, string> properties = new Dictionary<string, string>()
                    {
                        { "OrganizationLogin", repository.OrganizationLogin },
                        { "RepositoryName", repository.RepositoryName },
                        { "ErrorReturnCode", retrieveStatusCode.ToString() },
                    };
                    this.telemetryClient.TrackEvent("BookkeepingError", properties);
                }
            }
            catch (Exception exception)
            {
                Dictionary<string, string> properties = new Dictionary<string, string>()
                {
                    { "OrganizationLogin", repository.OrganizationLogin },
                    { "RepositoryName", repository.RepositoryName },
                    { "ErrorReturnCode", exception.ToString() },
                    { "ErrorType", exception.GetType().ToString() },
                };
                this.telemetryClient.TrackEvent("BookkeepingError", properties);
            }

            return 0;
        }

        public class EventStatsTableEntity : TableEntity
        {
            public long OrganizationId { get; set; }
            public long RepositoryId { get; set; }
            public string OrganizationLogin { get; set; }
            public string RepositoryName { get; set; }
            public int EventCount { get; set; }

            public EventStatsTableEntity()
            {
            }

            public EventStatsTableEntity(Repository repository, int eventCount)
            {
                this.PartitionKey = $"{repository.OrganizationId}_{repository.RepositoryId}";
                this.RowKey = string.Empty;

                this.OrganizationId = repository.OrganizationId;
                this.RepositoryId = repository.RepositoryId;
                this.OrganizationLogin = repository.OrganizationLogin;
                this.RepositoryName = repository.RepositoryName;
                this.EventCount = eventCount;
            }

            public EventStatsTableEntity(Repository repository, int eventCount, string eTag)
                : this (repository, eventCount)
            {
                this.ETag = eTag;
            }
        }
    }
}
