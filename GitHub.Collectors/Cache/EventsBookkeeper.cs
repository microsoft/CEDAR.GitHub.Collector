// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Telemetry;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using Azure.Storage.Queues;
using Azure.Data.Tables;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;

namespace Microsoft.CloudMine.GitHub.Collectors.Cache
{
    public class EventsBookkeeper : IEventsBookkeeper
    {
        private readonly ITelemetryClient telemetryClient;

        private TableClient table;
        private QueueClient queue;
        private bool initialized;

        public EventsBookkeeper(ITelemetryClient telemetryClient)
        {
            this.telemetryClient = telemetryClient;
            this.initialized = false;
        }

        public async Task InitializeAsync()
        {
            if (this.initialized)
            {
                return;
            }

            this.table = await AzureHelpers.GetStorageTableAsync("eventstats").ConfigureAwait(false);
            this.queue = await AzureHelpers.GetStorageQueueAsync("eventstats").ConfigureAwait(false);

            this.initialized = true;
        }

        public Task SignalCountAsync(Repository eventStats)
        {
            string message = JsonConvert.SerializeObject(eventStats, Formatting.None);
            return this.queue.SendMessageAsync(message);
        }

        public Task ResetCountAsync(Repository repository)
        {
            return this.table.UpsertEntityAsync<EventStatsTableEntity>(new EventStatsTableEntity(repository, 0, new ETag("*")), TableUpdateMode.Replace);
        }

        public async Task<int> IncrementCountAsync(Repository repository)
        {
            string partitionKey = $"{repository.OrganizationId}_{repository.RepositoryId}";
            string rowKey = string.Empty;
            try
            {
                var retrieveResult = await this.table.GetEntityAsync<EventStatsTableEntity>(partitionKey, rowKey).ConfigureAwait(false);
                int retrieveStatusCode = retrieveResult.GetRawResponse().Status;
                if (retrieveStatusCode == 404) // Not found
                {
                    try
                    {
                        await this.table.UpsertEntityAsync<EventStatsTableEntity>(new EventStatsTableEntity(repository, eventCount: 1), TableUpdateMode.Merge).ConfigureAwait(false);
                        return 1;
                    }
                    catch (Exception insertException) when (insertException is RequestFailedException)
                    {
                        RequestFailedException insertStorageException = (RequestFailedException)insertException;
                        int insertStatusCode = insertStorageException.Status;
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
                    EventStatsTableEntity retrieveStats = (EventStatsTableEntity)retrieveResult.Value;
                    ETag retrieveETag = retrieveResult.Value.ETag;

                    int currentEventCount = retrieveStats.EventCount;
                    try
                    {
                        await this.table.UpsertEntityAsync<EventStatsTableEntity>(new EventStatsTableEntity(repository, currentEventCount + 1, retrieveETag), TableUpdateMode.Replace).ConfigureAwait(false);
                        return currentEventCount + 1;
                    }
                    catch (Exception replaceException) when (replaceException is RequestFailedException)
                    {
                        RequestFailedException replaceStorageException = (RequestFailedException)replaceException;
                        int replaceStatusCode = replaceStorageException.Status;
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

        public class EventStatsTableEntity : ITableEntity
        {
            public long OrganizationId { get; set; }
            public long RepositoryId { get; set; }
            public string OrganizationLogin { get; set; }
            public string RepositoryName { get; set; }
            public int EventCount { get; set; }
            public string PartitionKey { get; set; }
            public string RowKey { get; set; }
            public DateTimeOffset? Timestamp { get; set; }
            public ETag ETag { get; set; }

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

            public EventStatsTableEntity(Repository repository, int eventCount, ETag eTag)
                : this (repository, eventCount)
            {
                this.ETag = eTag;
            }
        }
    }
}
