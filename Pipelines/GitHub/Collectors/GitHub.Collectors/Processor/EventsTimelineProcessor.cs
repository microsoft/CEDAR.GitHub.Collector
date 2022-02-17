// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Context;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Processor
{
    public class EventsTimelineProcessor
    {
        private readonly FunctionContext context;
        private readonly List<IRecordWriter> recordWriters;
        private readonly GitHubHttpClient httpClient;
        private readonly ICache<RecordTableEntity> recordsCache;
        private readonly ICache<EventsTimelineTableEntity> eventsTimelineCache;
        private readonly ITelemetryClient telemetryClient;
        private readonly IAuthentication authentication;
        private readonly string apiDomain;

        public EventsTimelineProcessor(FunctionContext context,
                                       IAuthentication authentication,
                                       List<IRecordWriter> recordWriters,
                                       GitHubHttpClient httpClient,
                                       ICache<RecordTableEntity> recordsCache,
                                       ICache<EventsTimelineTableEntity> eventsTimelineCache,
                                       ITelemetryClient telemetryClient,
                                       string apiDomain)
        {
            this.context = context;
            this.recordWriters = recordWriters;
            this.httpClient = httpClient;
            this.recordsCache = recordsCache;
            this.eventsTimelineCache = eventsTimelineCache;
            this.telemetryClient = telemetryClient;
            this.authentication = authentication;
            this.apiDomain = apiDomain;
        }

        public async Task ProcessAsync(Repository repository)
        {
            string eventsTimelineUrlPrefix = $"https://{this.apiDomain}/repos/{repository.OrganizationLogin}/{repository.RepositoryName}/events";

            EventsTimelineTableEntity currentWatermark = await this.eventsTimelineCache.RetrieveAsync(new EventsTimelineTableEntity(repository, this.context.SessionId, lastSeenEventId: long.MinValue, lastSeenEventDate: DateTime.MinValue)).ConfigureAwait(false);

            long lastSeenEventId = currentWatermark == null ? long.MinValue : currentWatermark.LastSeenEventId;
            long firstProcessedEventId = long.MinValue;

            DateTime lastSeenEventDate = currentWatermark == null ? DateTime.MinValue : currentWatermark.LastSeenEventDate;
            DateTime firstProcessedEventDate = DateTime.MinValue;

            int index = 1;
            bool continueProcessing = true;
            JObject eventObject = null;
            while (index <= 10 && continueProcessing)
            {
                string eventsTimelineUrl = $"{eventsTimelineUrlPrefix}?page={index}";
                try
                {
                    JArray responseArray = await this.httpClient.GetAndParseAsJArrayAsync(eventsTimelineUrl, this.authentication, "GitHub.Repos.Events", allowlistedResponses: new List<HttpResponseSignature>()).ConfigureAwait(false);
                    foreach (JToken responseItem in responseArray)
                    {
                        eventObject = (JObject)responseItem;
                        long eventId = eventObject.SelectToken("$.id").Value<long>();
                        if (firstProcessedEventId == long.MinValue)
                        {
                            firstProcessedEventId = eventId;
                        }

                        if (eventId == lastSeenEventId)
                        {
                            // We reached previous checkpoint. Nothing further to process.
                            continueProcessing = false;
                            break;
                        }

                        DateTime createdAt = eventObject.SelectToken("$.created_at").Value<DateTime>();
                        if (firstProcessedEventDate == DateTime.MinValue)
                        {
                            firstProcessedEventDate = createdAt;
                        }

                        if (lastSeenEventDate > createdAt)
                        {
                            // Multiple instance of this function were running against the same repository and another function processed (and committed) event timeline responses already.
                            // Nothing further to process.
                            this.telemetryClient.LogWarning($"Detected concurrent executions of delta collector against the same repository: {repository.OrganizationLogin}/{repository.RepositoryName}.");
                            continueProcessing = false;
                            break;
                        }

                        await this.ProcessEventAsync(repository, eventObject, eventId, createdAt).ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    Dictionary<string, string> properties = new Dictionary<string, string>()
                    {
                        { "Url", eventsTimelineUrl },
                        { "EventId", eventObject == null ? "N/A" : eventObject.SelectToken("$.id").Value<long>().ToString() },
                        { "Fatal", false.ToString() },
                    };
                    this.telemetryClient.TrackException(exception, properties: properties);
                }

                index++;
            }

            // Condition for overriding the cache entry:
            // 1. first processed (last) event id should be different than the one in the cache
            // 2. first processed (last) event date should be later than the one in the cache
            if (firstProcessedEventId != lastSeenEventId && firstProcessedEventDate > lastSeenEventDate)
            {
                await this.eventsTimelineCache.CacheAsync(new EventsTimelineTableEntity(repository, this.context.SessionId, firstProcessedEventId, firstProcessedEventDate)).ConfigureAwait(false);
            }
        }

        private async Task ProcessEventAsync(Repository repository, JObject eventObject, long eventId, DateTime createdAt)
        {
            string recordType = eventObject.SelectToken("$.type").Value<string>();

            JToken payloadToken = eventObject.SelectToken("$.payload");
            JObject payload = (JObject)payloadToken;
            IHasher eventHasher = EventHasherFactory.Instance.GetEventHasher(recordType, this.telemetryClient);
            string payloadSha = eventHasher.ComputeSha256Hash(payload, repository);

            bool isPayloadProcessed = await this.recordsCache.ExistsAsync(new RecordTableEntity(repository, recordSha: payloadSha)).ConfigureAwait(false);
            if (isPayloadProcessed)
            {
                // The event was successfully processed through the Webhook. Log this in telemetry.
                Dictionary<string, string> properties = new Dictionary<string, string>()
                {
                    { "RecordType", recordType },
                    { "RecordSha", payloadSha },
                    { "EventId", eventId.ToString() },
                    { "CreatedAt", $"{createdAt:O}" },
                };
                this.telemetryClient.TrackEvent("CapturedEvent", properties);
            }
            else
            {
                JToken repoIdToken = eventObject.SelectToken("$.repo.id");
                JToken repoNameToken = eventObject.SelectToken("$.repo.name");
                JToken orgIdToken = eventObject.SelectToken("$.org.id");
                JToken orgLoginToken = eventObject.SelectToken("$.org.login");
                if (repoIdToken == null || repoNameToken == null || orgIdToken == null || orgLoginToken == null)
                {
                    // Sometimes user repositories (forks) show up in the events timeline. Although, we don't know what triggers this, events coming from such repos can miss 
                    // Organization or Repo details that are crucial for downstream processing. Furthermore, these are not interesting for our purposes.
                    // Drop such events and log them in telemetry.

                    Dictionary<string, string> properties = new Dictionary<string, string>()
                    {
                        { "RecordType", recordType },
                        { "RecordSha", payloadSha },
                        { "EventId", eventId.ToString() },
                        { "CreatedAt", $"{createdAt:O}" },
                        { "RepositoryId", repoIdToken == null ? "NULL" : repoIdToken.Value<string>() },
                        { "RepositoryName", repoNameToken == null ? "NULL" : repoNameToken.Value<string>() },
                        { "OrganizationId", orgIdToken == null ? "NULL" : orgIdToken.Value<string>() },
                        { "OrganizationLogin", orgLoginToken == null ? "NULL" : orgLoginToken.Value<string>() },
                    };
                    this.telemetryClient.TrackEvent("UnexpectedEvent", properties);

                    return;
                }

                // For some reason we missed this event, re-ingest and log in telemetry.
                Dictionary<string, string> missedEventsProperties = new Dictionary<string, string>()
                {
                    { "RecordType", recordType },
                    { "RecordSha", payloadSha },
                    { "EventId", eventId.ToString() },
                    { "CreatedAt", $"{createdAt:O}" },
                };
                this.telemetryClient.TrackEvent("MissedEvent", missedEventsProperties);

                RecordContext context = new RecordContext()
                {
                    RecordType = recordType,
                    AdditionalMetadata = new Dictionary<string, JToken>()
                    {
                        { "ActorId", eventObject.SelectToken("$.actor.id") },
                        { "RepositoryId", repoIdToken },
                        { "RepositoryName", repoNameToken },
                        { "OrganizationId", orgIdToken },
                        { "OrganizationLogin", orgLoginToken },
                        { "CreatedAt", createdAt },
                        { "EventId", eventId },
                    },
                };
                foreach(IRecordWriter recordWriter in this.recordWriters)
                {
                    await recordWriter.WriteRecordAsync(payload, context).ConfigureAwait(false);
                }
                await this.recordsCache.CacheAsync(new RecordTableEntity(repository, recordType, payloadSha, this.context.SessionId)).ConfigureAwait(false);
            }
        }
    }
}
