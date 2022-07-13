// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.CloudMine.Core.Auditing;
using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Collector;
using Microsoft.CloudMine.Core.Collectors.Config;
using Microsoft.CloudMine.Core.Collectors.Context;
using Microsoft.CloudMine.Core.Collectors.Error;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.Core.Telemetry;
using Microsoft.CloudMine.GitHub.Collectors.Authentication;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Collector;
using Microsoft.CloudMine.GitHub.Collectors.Context;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using Microsoft.CloudMine.GitHub.Collectors.Processor;
using Microsoft.CloudMine.GitHub.Collectors.Telemetry;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Functions
{
    public class GitHubFunctions
    {
        private static readonly string FunctionInvocationTraceName = "FunctionInvocation";
        private static readonly TimeSpan StatsTrackerRefreshFrequency = TimeSpan.FromSeconds(10);
        private static readonly TelemetryMetric<long> HeartbeatMetric = new TelemetryMetric<long>("HeartbeatCounter");

        public readonly string apiDomain;
        private readonly TelemetryClient telemetryClient;
        private readonly IHttpClient httpClient;
        private readonly IAdlsClient adlsClient;
        private readonly GitHubConfigManager configManager;
        private readonly IAuditLogger ifxLogger;
        private readonly string storageAccountNameEnvironmentVariable;

        // Using dependency injection will guarantee that you use the same configuration for telemetry collected automatically and manually.
        public GitHubFunctions(TelemetryConfiguration telemetryConfiguration, IHttpClient httpClient, IAdlsClient adlsClient, GitHubConfigManager configManager, IAuditLogger auditLogger)
        {
            this.telemetryClient = new TelemetryClient(telemetryConfiguration);
            this.httpClient = httpClient;
            this.apiDomain = configManager.GetApiDomain();
            this.adlsClient = adlsClient;
            this.configManager = configManager;
            this.configManager.SetTelemetryClient(this.telemetryClient);
            this.ifxLogger = auditLogger;
            this.storageAccountNameEnvironmentVariable = Environment.GetEnvironmentVariable(Utility.Constants.StorageAccountEnvironmentVariable);
            if (this.adlsClient.AdlsClient == null)
            {
                Dictionary<string, string> properties = new Dictionary<string, string>()
                {
                    { "ResourceName", "ADLS Client" },
                };
                this.telemetryClient.TrackEvent("UninitializedResource", properties);
            }
        }

        [FunctionName("ProcessWebHook")]
        public async Task<HttpResponseMessage> ProcessWebHook([HttpTrigger(AuthorizationLevel.Function, "POST")] HttpRequestMessage request,
                                                              [DurableClient] IDurableOrchestrationClient orchestrationClient,
                                                              ExecutionContext executionContext,
                                                              ILogger logger)
        {
            DateTime startTime = DateTime.UtcNow;
            string requestBody = await request.Content.ReadAsStringAsync().ConfigureAwait(false);

            request.Headers.TryGetValues("X-GitHub-Delivery", out IEnumerable<string> deliveryGuidValues);
            if (deliveryGuidValues == null || !deliveryGuidValues.Any())
            {
                string errorMessage = $"WebHookProcessor requires 'X-GitHub-Event' header.";
                this.telemetryClient.TrackTrace(errorMessage, SeverityLevel.Error);
                throw new FatalException(errorMessage);
            }
            string sessionId = deliveryGuidValues.First();

            request.Headers.TryGetValues("X-GitHub-Event", out IEnumerable<string> eventTypeValues);
            if (eventTypeValues == null || !eventTypeValues.Any())
            {
                string errorMessage = $"WebHookProcessor endpoint requires 'X-GitHub-Event' header.";
                this.telemetryClient.TrackTrace(errorMessage, SeverityLevel.Error);
                throw new FatalException(errorMessage);
            }
            string eventType = eventTypeValues.First();
            // Temporarily ignore processing secret_scanning_alert events since our collectors are failing because of the load.
            if (eventType.Equals("secret_scanning_alert"))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            }

            // The request does not have to go through the LogicApp. This might happen e.g., for local testing. Therefore, assume that Logic-App-related headers are optional:
            // X-LogicApp-Timestamp
            // X-Ms-Workflow-Run-Id
            request.Headers.TryGetValues("X-LogicApp-Timestamp", out IEnumerable<string> logicAppStartDateValues);
            string logicAppStartDate = string.Empty;
            if (logicAppStartDateValues != null && logicAppStartDateValues.Any())
            {
                logicAppStartDate = logicAppStartDateValues.First();
            }

            request.Headers.TryGetValues("X-Ms-Workflow-Run-Id", out IEnumerable<string> logicAppRunIdValues);
            string logicAppRunId = string.Empty;
            if (logicAppRunIdValues != null && logicAppRunIdValues.Any())
            {
                logicAppRunId = logicAppRunIdValues.First();
            }

            OrchestrationContext context = new OrchestrationContext()
            {
                RequestBody = requestBody,
                CollectorType = CollectorType.Main.ToString(),
                SessionId = sessionId,
                EventType = eventType,
                FunctionStartDate = startTime,
                LogicAppStartDate = logicAppStartDate,
                LogicAppRunId = logicAppRunId,
                InvocationId = executionContext.InvocationId.ToString(),
            };

            ITelemetryClient telemetryClient = new GitHubApplicationInsightsTelemetryClient(this.telemetryClient, context);
            telemetryClient.TrackEvent("SessionStart", GetMainCollectorSessionStartEventProperties(context, identifier: eventType, logicAppRunId));

            string instanceId = await orchestrationClient.StartNewAsync("ProcessWebHookOrchestration", context).ConfigureAwait(false);
            return orchestrationClient.CreateCheckStatusResponse(request, instanceId, returnInternalServerErrorOnFailure: true);
        }

        [FunctionName("ProcessWebHookOrchestration")]
        public Task ProcessWebHookOrchestration([OrchestrationTrigger] IDurableOrchestrationContext durableContext)
        {
            OrchestrationContext context = durableContext.GetInput<OrchestrationContext>();
            return durableContext.CallActivityAsync("ProcessWebHookActivity", context);
        }

        /// <summary>
        /// HTTP-triggered durable Azure function that processes the GitHub Webhook payload. Also known as the main collector.
        /// </summary>
        [FunctionName("ProcessWebHookActivity")]
        public async Task ProcessWebHookActivity([ActivityTrigger] IDurableActivityContext durableContext, ILogger logger)
        {
            OrchestrationContext context = durableContext.GetInput<OrchestrationContext>();
            using Activity invocationActivity = GetInvocationActivity(context).Start();

            string requestBody = context.RequestBody;
            WebhookProcessorContext functionContext = context.Downgrade();
            functionContext.CollectorIdentity = this.configManager.GetCollectorIdentity();
            WebhookProcessorContextWriter contextWriter = new WebhookProcessorContextWriter();

            JObject record = JObject.Parse(requestBody);
            string organizationName;

            Dictionary<string, string> additionalTelemetryProperties = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> property in GetMainCollectorSessionStartEventProperties(functionContext, identifier: functionContext.EventType, functionContext.LogicAppRunId))
            {
                additionalTelemetryProperties.Add(property.Key, property.Value);
            }
            string outputPaths = string.Empty;
            bool success = false;
            ITelemetryClient telemetryClient = new GitHubApplicationInsightsTelemetryClient(this.telemetryClient, functionContext);
            try
            {
                // Not all payloads have a "repository" attribute e.g., membership, organization, project, project_card, etc. paylods. Look under $organization attribute first, if available.
                JToken organizationNameToken = record.SelectToken("$.organization.login");
                if (organizationNameToken == null)
                {
                    organizationNameToken = record.SelectToken("$.repository.owner.login");
                }
                if (organizationNameToken == null)
                {
                    // NOTE: BELOW RELEVANT IF WE ARE RELYING ON APP WEBHOOKS
                    // In theory, all event payloads will have repository.owner.login (see https://developer.github.com/webhooks/event-payloads/#webhook-payload-object-common-properties)
                    // However, there are certain app-specific webhooks (e.g. new_permissions_accepted, which is not documented/can't be found by a search engine) that lack this.
                    // note: other example app-specific webhooks - app installed into repo, app uninstalled from repo, etc.
                    throw new FatalTerminalException("Could not find organization name in webhook payload.");
                }
                organizationName = organizationNameToken.Value<string>();

                IEventsBookkeeper eventsBookkeeper = new EventsBookkeeper(telemetryClient);
                await eventsBookkeeper.InitializeAsync().ConfigureAwait(false);

                ICache<RecordTableEntity> recordsCache = new AzureTableCache<RecordTableEntity>(telemetryClient, "records");
                await recordsCache.InitializeAsync().ConfigureAwait(false);

                ICache<RepositoryItemTableEntity> collectorCache = new AzureTableCache<RepositoryItemTableEntity>(telemetryClient, "github");
                await collectorCache.InitializeAsync().ConfigureAwait(false);

                ICache<ConditionalRequestTableEntity> requestsCache = new AzureTableCache<ConditionalRequestTableEntity>(telemetryClient, "requests");
                await requestsCache.InitializeAsync().ConfigureAwait(false);

                ICache<PointCollectorTableEntity> pointCollectorCache = new AzureTableCache<PointCollectorTableEntity>(telemetryClient, "point");
                await pointCollectorCache.InitializeAsync().ConfigureAwait(false);

                StorageManager storageManager;
                List<IRecordWriter> recordWriters;
                using (storageManager = this.configManager.GetStorageManager(context.CollectorType, telemetryClient))
                {
                    recordWriters = storageManager.InitializeRecordWriters(identifier: functionContext.EventType, functionContext, contextWriter, this.adlsClient.AdlsClient);
                    WebHookProcessor processor = new WebHookProcessor(requestBody, functionContext, recordWriters, eventsBookkeeper, recordsCache, collectorCache, pointCollectorCache, telemetryClient, this.apiDomain);
                    additionalTelemetryProperties = await processor.ProcessAsync().ConfigureAwait(false);

                    foreach (KeyValuePair<string, string> property in GetMainCollectorSessionStartEventProperties(functionContext, identifier: functionContext.EventType, functionContext.LogicAppRunId))
                    {
                        additionalTelemetryProperties.Add(property.Key, property.Value);
                    }
                }

                await storageManager.FinalizeRecordWritersAsync().ConfigureAwait(false);
                outputPaths = RecordWriterExtensions.GetOutputPaths(recordWriters);
                success = true;
            }
            catch (Exception exception) when (!(exception is FatalException))
            {
                telemetryClient.TrackException(exception, "ProcessWebHookActivity failed.");
                throw exception;
            }
            finally
            {
                SendSessionEndEvent(telemetryClient, functionContext.FunctionStartDate, outputPaths, additionalTelemetryProperties, success);
            }
        }

        private static Dictionary<string, string> GetMainCollectorSessionStartEventProperties(FunctionContext context, string identifier, string logicAppRunId)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(GetCollectorCommonSessionStartEventProperties(context, identifier))
            {
                { "LogicAppRunId", logicAppRunId },
            };

            return result;
        }

        private static Dictionary<string, string> GetCollectorCommonSessionStartEventProperties(FunctionContext context, string identifier)
        {
            // Product version includes verison + commit sha, for example: Mjaor.Minor.Revision+CommitSha
            string commitSha = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion.Split('+').Last();
            return new Dictionary<string, string>()
            {
                // No need to add SessionId since it is already included through the TelemetryClient context.
                { "CollectorType", context.CollectorType },
                { "Identifier", identifier },
                { "FunctionStartDate", $"{context.FunctionStartDate:O}" },
                { "CommitSha", commitSha },
                { "InvocationId", context.InvocationId },
                { "DequeueCount", context.DequeueCount.ToString() },
            };
        }

        private static void SendSessionEndEvent(ITelemetryClient telemetryClient, DateTime functionStartDate, string outputPaths, Dictionary<string, string> sessionStartProperties, bool success)
        {
            DateTime endTime = DateTime.UtcNow;
            TimeSpan sessionDuration = endTime - functionStartDate;
            Dictionary<string, string> sessionEndProperties = new Dictionary<string, string>(sessionStartProperties)
            {
                { "FunctionEndDate", $"{endTime:O}" },
                { "FunctionDuration", sessionDuration.ToString() },
                { "OutputPaths", outputPaths },
                { "Success", success.ToString() },
            };
            telemetryClient.TrackEvent("SessionEnd", sessionEndProperties);
        }

        /// <summary>
        /// Queue triggered Azure function to query the events timeline API to ensure that we are not missing any events from the GitHub Webhooks.
        /// The input (a Repository object as JSON string) defines which repository to query against.
        /// Also known as the delta collector.
        /// </summary>
        [FunctionName("ProcessEventsTimeline")]
        public async Task ProcessEventsTimeline([QueueTrigger("eventstats")] string queueItem, ExecutionContext executionContext, ILogger logger, int dequeueCount)
        {
            using Activity invocationActivity = GetInvocationActivity(executionContext, queueItem, dequeueCount).Start();

            DateTime functionStartDate = DateTime.UtcNow;
            string sessionId = Guid.NewGuid().ToString();

            JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.None
            };
            Repository repositoryDetails = JsonConvert.DeserializeObject<Repository>(queueItem, serializerSettings);
            FunctionContextWriter<FunctionContext> contextWriter = new FunctionContextWriter<FunctionContext>();
            string identifier = $"EventsTimeline";

            FunctionContext context = new FunctionContext()
            {
                CollectorType = CollectorType.Delta.ToString(),
                CollectorIdentity = this.configManager.GetCollectorIdentity(),
                FunctionStartDate = functionStartDate,
                SessionId = sessionId,
                InvocationId = executionContext.InvocationId.ToString(),
            };

            string outputPaths = string.Empty;
            bool success = false;
            ITelemetryClient telemetryClient = new GitHubApplicationInsightsTelemetryClient(this.telemetryClient, context);
            try
            {
                telemetryClient.TrackEvent("SessionStart", GetRepositoryCollectorSessionStartEventProperties(context, identifier, repositoryDetails));

                ICache<RecordTableEntity> recordsCache = new AzureTableCache<RecordTableEntity>(telemetryClient, "records");
                await recordsCache.InitializeAsync().ConfigureAwait(false);

                ICache<EventsTimelineTableEntity> eventsTimelineCache = new AzureTableCache<EventsTimelineTableEntity>(telemetryClient, "eventstimeline");
                await eventsTimelineCache.InitializeAsync().ConfigureAwait(false);

                ICache<RateLimitTableEntity> rateLimiterCache = new AzureTableCache<RateLimitTableEntity>(telemetryClient, "ratelimiter");
                await rateLimiterCache.InitializeAsync().ConfigureAwait(false);
                IRateLimiter rateLimiter = new GitHubRateLimiter(this.configManager.UsesGitHubAuth(context.CollectorType) ? repositoryDetails.OrganizationLogin : RateLimitTableEntity.GlobalOrganizationId, rateLimiterCache, this.httpClient, telemetryClient, maxUsageBeforeDelayStarts: 85.0, this.apiDomain);
                ICache<ConditionalRequestTableEntity> requestsCache = new AzureTableCache<ConditionalRequestTableEntity>(telemetryClient, "requests");
                await requestsCache.InitializeAsync().ConfigureAwait(false);
                GitHubHttpClient httpClient = new GitHubHttpClient(this.httpClient, rateLimiter, requestsCache, telemetryClient);

                IAuthentication authentication = this.configManager.GetAuthentication(CollectorType.Delta, httpClient, repositoryDetails.OrganizationLogin, this.apiDomain, telemetryClient, this.ifxLogger);

                StorageManager storageManager;
                List<IRecordWriter> recordWriters;
                using (storageManager = this.configManager.GetStorageManager(context.CollectorType, telemetryClient))
                {
                    recordWriters = storageManager.InitializeRecordWriters(identifier, context, contextWriter, this.adlsClient.AdlsClient);

                    foreach (IRecordWriter recordWriter in recordWriters)
                    {
                        recordWriter.SetOutputPathPrefix($"{repositoryDetails.OrganizationId}/{repositoryDetails.RepositoryId}");
                    }
                    EventsTimelineProcessor processor = new EventsTimelineProcessor(context, authentication, recordWriters, httpClient, recordsCache, eventsTimelineCache, telemetryClient, this.apiDomain);
                    await processor.ProcessAsync(repositoryDetails).ConfigureAwait(false);
                }

                await storageManager.FinalizeRecordWritersAsync().ConfigureAwait(false);
                outputPaths = RecordWriterExtensions.GetOutputPaths(recordWriters);
                success = true;
            }
            catch (Exception exception) when (!(exception is FatalException))
            {
                telemetryClient.TrackException(exception, "ProcessEventsTimeline failed.");
                throw exception;
            }
            finally
            {
                SendSessionEndEvent(telemetryClient, context.FunctionStartDate, outputPaths, GetRepositoryCollectorSessionStartEventProperties(context, identifier, repositoryDetails), success);
            }
        }

        /// <summary>
        /// Queue triggered Azure function to onboard an organization or a repository. The input (an OnboardingInput as JSON object) decides what to onboard.
        /// Also known as the onboarding collector.
        /// </summary>
        [FunctionName("Onboard")]
        public async Task Onboard([QueueTrigger("onboarding")] string queueItem, ExecutionContext executionContext, ILogger logger, int dequeueCount)
        {
            using Activity invocationActivity = GetInvocationActivity(executionContext, queueItem, dequeueCount).Start();

            DateTime functionStartDate = DateTime.UtcNow;
            string sessionId = Guid.NewGuid().ToString();

            JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.None
            };
            OnboardingInput onboardingInput = JsonConvert.DeserializeObject<OnboardingInput>(queueItem, serializerSettings);
            Repository repositoryDetails = onboardingInput.ToRepository();
            FunctionContextWriter<FunctionContext> contextWriter = new FunctionContextWriter<FunctionContext>();
            string identifier = $"Onboarding";

            FunctionContext context = new FunctionContext()
            {
                CollectorType = CollectorType.Onboarding.ToString(),
                CollectorIdentity = this.configManager.GetCollectorIdentity(),
                FunctionStartDate = functionStartDate,
                SessionId = sessionId,
                InvocationId = executionContext.InvocationId.ToString(),
            };

            StatsTracker statsTracker = null;
            bool success = false;
            List<IRecordWriter> recordWriters = null;
            ITelemetryClient telemetryClient = new GitHubApplicationInsightsTelemetryClient(this.telemetryClient, context);            
            try
            {
                telemetryClient.TrackEvent("SessionStart", GetRepositoryCollectorSessionStartEventProperties(context, identifier, repositoryDetails));
                ICache<OnboardingTableEntity> onboardingCache = new AzureTableCache<OnboardingTableEntity>(telemetryClient, "onboarding");
                await onboardingCache.InitializeAsync().ConfigureAwait(false);
                ICache<RateLimitTableEntity> rateLimiterCache = new AzureTableCache<RateLimitTableEntity>(telemetryClient, "ratelimiter");
                await rateLimiterCache.InitializeAsync().ConfigureAwait(false);
                IRateLimiter rateLimiter = new GitHubRateLimiter(this.configManager.UsesGitHubAuth(context.CollectorType) ? onboardingInput.OrganizationLogin : "*", rateLimiterCache, this.httpClient, telemetryClient, maxUsageBeforeDelayStarts: 50.0, this.apiDomain);
                ICache<ConditionalRequestTableEntity> requestsCache = new AzureTableCache<ConditionalRequestTableEntity>(telemetryClient, "requests");
                await requestsCache.InitializeAsync().ConfigureAwait(false);
                GitHubHttpClient httpClient = new GitHubHttpClient(this.httpClient, rateLimiter, requestsCache, telemetryClient);
                IAuthentication authentication = this.configManager.GetAuthentication(CollectorType.Onboarding, httpClient, onboardingInput.OrganizationLogin, this.apiDomain, telemetryClient, this.ifxLogger);
                StorageManager storageManager;
                using (storageManager = this.configManager.GetStorageManager(context.CollectorType, telemetryClient))
                {
                    recordWriters = storageManager.InitializeRecordWriters(identifier, context, contextWriter, this.adlsClient.AdlsClient);
                    IRecordStatsTracker recordStatsTracker = null;

                    foreach (IRecordWriter recordWriter in recordWriters)
                    {
                        recordWriter.SetOutputPathPrefix($"{repositoryDetails.OrganizationId}/{repositoryDetails.RepositoryId}");
                        if (recordStatsTracker == null)
                        {
                            recordStatsTracker = recordWriter;
                        }
                    }

                    statsTracker = new StatsTracker(telemetryClient, httpClient, recordStatsTracker, StatsTrackerRefreshFrequency);

                    CloudQueueMsiWrapper onboardingMsiWrapper = new CloudQueueMsiWrapper("onboarding", storageAccountNameEnvironmentVariable, telemetryClient);
                    CloudQueue onboardingCloudQueue = await onboardingMsiWrapper.GetValidMsiStorageQueueAsync().ConfigureAwait(false);
                    IQueue onboardingQueue = new CloudQueueWrapper(onboardingCloudQueue);
                    OnboardingProcessor processor = new OnboardingProcessor(authentication, recordWriters, httpClient, onboardingCache, onboardingMsiWrapper, telemetryClient, this.apiDomain);
                    await processor.ProcessAsync(onboardingInput).ConfigureAwait(false);
                }

                await storageManager.FinalizeRecordWritersAsync().ConfigureAwait(false);
                success = true;
            }
            catch (GitHubRateLimitException exception)
            {
                telemetryClient.TrackException(exception, "RateLimiterRequeue");
                CloudQueue onboardingCloudQueue = await AzureHelpers.GetStorageQueueUsingMsiAsync("onboarding", storageAccountNameEnvironmentVariable, telemetryClient).ConfigureAwait(false);
                TimeSpan? initialVisibilityDelay = exception.GetHiddenTime();
                TimeSpan? timeToLive = null;
                await onboardingCloudQueue.AddMessageAsync(new CloudQueueMessage(queueItem), timeToLive, initialVisibilityDelay, new QueueRequestOptions(), new OperationContext()).ConfigureAwait(false);
            }
            catch (Exception exception) when (!(exception is FatalException))
            {
                telemetryClient.TrackException(exception, "Onboard failed.");
                throw exception;
            }
            finally
            {
                string outputPaths = string.Empty;
                if (recordWriters != null)
                {
                    outputPaths = RecordWriterExtensions.GetOutputPaths(recordWriters);
                }
                SendSessionEndEvent(telemetryClient, context.FunctionStartDate, outputPaths, GetRepositoryCollectorSessionStartEventProperties(context, identifier, repositoryDetails), success);
                statsTracker?.Stop();
            }
        }

        /// <summary>
        /// Timer triggered Azure function to queue the list of repositories for collecting the traffic endpoints:
        /// https://developer.github.com/v3/repos/traffic/
        /// Also known as traffic collector trigger.
        /// </summary>
        [FunctionName("TrafficTimer")]
        public Task TrafficTimer([TimerTrigger("0 0 8 * * *" /* run once every day at 00:00:00 PST*/)] TimerInfo timerInfo, ExecutionContext executionContext, ILogger logger)
        {
            return ExecuteTrafficCollector(executionContext, logger, dequeueCount: 0);
        }

        /// <summary>
        /// Queue triggered Azure function to queue the list of repositories for collecting the traffic endpoints:
        /// https://developer.github.com/v3/repos/traffic/
        /// Also known as traffic collector trigger.
        [FunctionName("TrafficCollector")]
        public Task TrafficCollector([QueueTrigger("trafficcollector")] string queueItem, ExecutionContext executionContext, ILogger logger, int dequeueCount)
        {
            return ExecuteTrafficCollector(executionContext, logger, dequeueCount);
        }

        private async Task ExecuteTrafficCollector(ExecutionContext executionContext, ILogger logger, int dequeueCount)
        {
            using Activity invocationActivity = GetInvocationActivity(executionContext, "", dequeueCount).Start();
            DateTime functionStartDate = DateTime.UtcNow;
            string sessionId = Guid.NewGuid().ToString();
            string identifier = "TrafficTimer";
            FunctionContext context = new FunctionContext()
            {
                CollectorType = CollectorType.TrafficTimer.ToString(),
                CollectorIdentity = this.configManager.GetCollectorIdentity(),
                FunctionStartDate = functionStartDate,
                SessionId = sessionId,
                InvocationId = executionContext.InvocationId.ToString(),
                DequeueCount = dequeueCount,
            };
            ITelemetryClient telemetryClient = new GitHubApplicationInsightsTelemetryClient(this.telemetryClient, context);
            IQueue trafficCloudQueue = new CloudQueueMsiWrapper("traffic", storageAccountNameEnvironmentVariable, telemetryClient);
            StatsTracker statsTracker = null;
            bool success = false;            
            try
            {
                telemetryClient.TrackEvent("SessionStart", GetCollectorCommonSessionStartEventProperties(context, identifier));

                ICache<RateLimitTableEntity> rateLimiterCache = new AzureTableCache<RateLimitTableEntity>(telemetryClient, "ratelimiter");
                await rateLimiterCache.InitializeAsync().ConfigureAwait(false);
                ICache<ConditionalRequestTableEntity> requestsCache = new AzureTableCache<ConditionalRequestTableEntity>(telemetryClient, "requests");
                await requestsCache.InitializeAsync().ConfigureAwait(false);

                string organizations = await AzureHelpers.GetBlobContentUsingMsiAsync("github-settings", "organizations.json", storageAccountNameEnvironmentVariable).ConfigureAwait(false);
                JArray organizationsArray = JArray.Parse(organizations);
                foreach (JToken organizationToken in organizationsArray)
                {
                    JObject organization = (JObject)organizationToken;
                    string organizationLogin = organization.SelectToken("$.OrganizationLogin").Value<string>();
                    long organizationId = organization.SelectToken("$.OrganizationId").Value<long>();

                    IRateLimiter rateLimiter = new GitHubRateLimiter(this.configManager.UsesGitHubAuth(context.CollectorType) ? organizationLogin : "*", rateLimiterCache, this.httpClient, telemetryClient, maxUsageBeforeDelayStarts: 85.0, this.apiDomain);
                    GitHubHttpClient httpClient = new GitHubHttpClient(this.httpClient, rateLimiter, requestsCache, telemetryClient);

                    statsTracker = new StatsTracker(telemetryClient, httpClient, StatsTrackerRefreshFrequency);

                    IAuthentication authentication = this.configManager.GetAuthentication(CollectorType.TrafficTimer, httpClient, organizationLogin, this.apiDomain, telemetryClient, this.ifxLogger);
                    CollectorBase<GitHubCollectionNode> collector = new GitHubCollector(httpClient, authentication, telemetryClient, new List<IRecordWriter>());

                    try
                    {
                        GitHubCollectionNode repositoriesNode = new GitHubCollectionNode()
                        {
                            RecordType = DataContract.RepositoryInstanceRecordType,
                            ApiName = DataContract.RepositoriesApiName,
                            GetInitialUrl = additionalMetadata => OnboardingProcessor.InitialRepositoriesUrl(organizationLogin, this.apiDomain),
                            ProcessRecordAsync = async record =>
                            {
                                string repositoryName = record.SelectToken("$.name").Value<string>();
                                long repositoryId = record.SelectToken("$.id").Value<long>();

                                Repository repository = new Repository(organizationId, repositoryId, organizationLogin, repositoryName);
                                await trafficCloudQueue.PutObjectAsJsonStringAsync(repository, TimeSpan.MaxValue).ConfigureAwait(false);
                                return new List<RecordWithContext>();
                            },
                        };

                        await collector.ProcessAsync(repositoriesNode).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // If we fail to do the repos/ call for an organization, don't let this stop collection for the rest.
                        Dictionary<string, string> properties = new Dictionary<string, string>()
                        {
                            { "OrganizationLogin", organizationLogin },
                        };
                        telemetryClient.TrackException(ex, "Cannot initialize traffic collection.", properties);
                    }
                }

                success = true;
            }
            catch (Exception exception) when (!(exception is FatalException))
            {
                telemetryClient.TrackException(exception, "TrafficTimer failed.");
                throw exception;
            }
            finally
            {
                SendSessionEndEvent(telemetryClient, context.FunctionStartDate, outputPaths: string.Empty, GetCollectorCommonSessionStartEventProperties(context, identifier), success);
                statsTracker?.Stop();
            }
        }

        /// <summary>
        /// Queue triggered Azure function to collect "traffic" data for a repository. The input (a Repository as JSON object) decides which repository to collect.
        /// Also known as the traffic collector.
        /// </summary>
        [FunctionName("Traffic")]
        public async Task Traffic([QueueTrigger("traffic")] string queueItem, ExecutionContext executionContext, ILogger logger, int dequeueCount)
        {
            using Activity invocationActivity = GetInvocationActivity(executionContext, queueItem, dequeueCount).Start();

            DateTime functionStartDate = DateTime.UtcNow;
            string sessionId = Guid.NewGuid().ToString();

            JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.None
            };
            Repository repositoryDetails = JsonConvert.DeserializeObject<Repository>(queueItem, serializerSettings);
            string identifier = $"Traffic";

            FunctionContext context = new FunctionContext()
            {
                CollectorType = CollectorType.Traffic.ToString(),
                CollectorIdentity = this.configManager.GetCollectorIdentity(),
                FunctionStartDate = functionStartDate,
                SessionId = sessionId,
                InvocationId = executionContext.InvocationId.ToString(),
                DequeueCount = dequeueCount,
            };

            bool success = false;
            ITelemetryClient telemetryClient = new GitHubApplicationInsightsTelemetryClient(this.telemetryClient, context);
            try
            {
                telemetryClient.TrackEvent("SessionStart", GetRepositoryCollectorSessionStartEventProperties(context, identifier, repositoryDetails));

                ICache<PointCollectorTableEntity> pointCollectorCache = new AzureTableCache<PointCollectorTableEntity>(telemetryClient, "point");
                await pointCollectorCache.InitializeAsync().ConfigureAwait(false);

                TrafficProcessor processor = new TrafficProcessor(telemetryClient, pointCollectorCache, this.apiDomain);
                await processor.ProcessAsync(repositoryDetails).ConfigureAwait(false);

                success = true;
            }
            catch (Exception exception) when (!(exception is FatalException))
            {
                telemetryClient.TrackException(exception, "Traffic failed.");
                throw exception;
            }
            finally
            {
                SendSessionEndEvent(telemetryClient, context.FunctionStartDate, string.Empty, GetRepositoryCollectorSessionStartEventProperties(context, identifier, repositoryDetails), success);
            }
        }

        [FunctionName("PointCollector")]
        public Task PointCollector([QueueTrigger("pointcollector")] string queueItem, ExecutionContext executionContext, ILogger logger, int dequeueCount)
        {
            return this.ExecutePointCollectorAsync(queueItem, executionContext, logger, queueSuffix : string.Empty, dequeueCount);
        }

        [FunctionName("PointCollectorDri")]
        public Task PointCollectorAdHoc([QueueTrigger("pointcollector-dri")] string queueItem, ExecutionContext executionContext, ILogger logger, int dequeueCount)
        {
            return this.ExecutePointCollectorAsync(queueItem, executionContext, logger, queueSuffix : "-dri", dequeueCount);
        }

        public async Task ExecutePointCollectorAsync(string queueItem, ExecutionContext executionContext, ILogger logger, string queueSuffix, int dequeueCount)
        {
            using Activity invocationActivity = GetInvocationActivity(executionContext, queueItem, dequeueCount).Start();

            DateTime functionStartDate = DateTime.UtcNow;
            string sessionId = Guid.NewGuid().ToString();

            JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.None
            };
            PointCollectorInput pointCollectorInput = JsonConvert.DeserializeObject<PointCollectorInput>(queueItem, serializerSettings);

            FunctionContext context = new FunctionContext()
            {
                CollectorType = CollectorType.Point.ToString(),
                CollectorIdentity = this.configManager.GetCollectorIdentity(),
                FunctionStartDate = functionStartDate,
                SessionId = sessionId,
                InvocationId = executionContext.InvocationId.ToString(),
                DequeueCount = dequeueCount,
            };

            ITelemetryClient telemetryClient = new GitHubApplicationInsightsTelemetryClient(this.telemetryClient, context);
            bool success = false;
            string outputPaths = string.Empty;
            StatsTracker statsTracker = null;
            string identifier = "Point";
            try
            {
                telemetryClient.TrackEvent("SessionStart", GetRepositoryCollectorSessionStartEventProperties(context, identifier, pointCollectorInput.Repository));

                ICache<PointCollectorTableEntity> pointCollectorCache = new AzureTableCache<PointCollectorTableEntity>(telemetryClient, "point");
                await pointCollectorCache.InitializeAsync().ConfigureAwait(false);
                Repository repository = pointCollectorInput.Repository;
                ICache<RateLimitTableEntity> rateLimiterCache = new AzureTableCache<RateLimitTableEntity>(telemetryClient, "ratelimiter");
                await rateLimiterCache.InitializeAsync().ConfigureAwait(false);
                IRateLimiter rateLimiter = new GitHubRateLimiter(this.configManager.UsesGitHubAuth(context.CollectorType) ? repository.OrganizationLogin : "*", rateLimiterCache, this.httpClient, telemetryClient, maxUsageBeforeDelayStarts: 80.0, this.apiDomain, throwOnRateLimit: true);
                ICache<ConditionalRequestTableEntity> requestsCache = new AzureTableCache<ConditionalRequestTableEntity>(telemetryClient, "requests");
                await requestsCache.InitializeAsync().ConfigureAwait(false);
                GitHubHttpClient httpClient = new GitHubHttpClient(this.httpClient, rateLimiter, requestsCache, telemetryClient);
                IAuthentication authentication = this.configManager.GetAuthentication(CollectorType.Point, httpClient, repository.OrganizationLogin, this.apiDomain, telemetryClient, this.ifxLogger);

                StorageManager storageManager;
                List<IRecordWriter> recordWriters;
                FunctionContextWriter<FunctionContext> contextWriter = new FunctionContextWriter<FunctionContext>();
                using (storageManager = this.configManager.GetStorageManager(context.CollectorType, telemetryClient))
                {
                    // ToDo : lukegostling 9/10/2021, simplify init record writers (we no longer do ADLS direct ingestion)
                    recordWriters = storageManager.InitializeRecordWriters(identifier, context, contextWriter, this.adlsClient.AdlsClient);
                    IRecordStatsTracker recordStatsTracker = null;

                    foreach (IRecordWriter recordWriter in recordWriters)
                    {
                        recordWriter.SetOutputPathPrefix($"{repository.OrganizationId}/{repository.RepositoryId}");
                        if (recordStatsTracker == null)
                        {
                            recordStatsTracker = recordWriter;
                        }
                    }

                    statsTracker = new StatsTracker(telemetryClient, httpClient, recordStatsTracker, StatsTrackerRefreshFrequency);
                    PointCollector processor = new PointCollector(authentication, recordWriters, httpClient, pointCollectorCache, telemetryClient);
                    await processor.ProcessAsync(pointCollectorInput).ConfigureAwait(false);
                }
                await storageManager.FinalizeRecordWritersAsync().ConfigureAwait(false);
                outputPaths = RecordWriterExtensions.GetOutputPaths(recordWriters);
                success = true;
            }
            catch (GitHubRateLimitException exception)
            {
                telemetryClient.TrackException(exception, "RateLimiterRequeue");
                CloudQueue trafficCloudQueue = await AzureHelpers.GetStorageQueueUsingMsiAsync($"pointcollector{queueSuffix}", storageAccountNameEnvironmentVariable, telemetryClient).ConfigureAwait(false);
                TimeSpan? initialVisibilityDelay = exception.GetHiddenTime();
                TimeSpan? timeToLive = null;
                await trafficCloudQueue.AddMessageAsync(new CloudQueueMessage(queueItem), timeToLive, initialVisibilityDelay, new QueueRequestOptions(), new OperationContext()).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                telemetryClient.TrackException(exception);
                throw;
            }
            finally
            {
                SendSessionEndEvent(telemetryClient, context.FunctionStartDate, outputPaths, GetRepositoryCollectorSessionStartEventProperties(context, identifier, pointCollectorInput.Repository), success);
                statsTracker?.Stop();
            }
        }

        [FunctionName("DiscoverOrganizationsTimer")]
        public Task AutoOnboard([TimerTrigger("0 0 8 * * *") /* execute once per day at midnight (PST)*/] TimerInfo timerInfo, ExecutionContext executionContext, ILogger logger)
        {
            return this.ExecuteAutoOnboardAsync(executionContext, logger, 0);
        }

        [FunctionName("DiscoverOrganizations")]
        public Task AutoOnboardDri([QueueTrigger("discover-organizations")] string queueItem, ExecutionContext executionContext, ILogger logger, int dequeueCount)
        {
            return this.ExecuteAutoOnboardAsync(executionContext, logger, dequeueCount);
        }

        private async Task ExecuteAutoOnboardAsync(ExecutionContext executionContext, ILogger logger, int dequeueCount )
        {
            using Activity invocationActivity = GetInvocationActivity(executionContext, "", dequeueCount).Start();

            DateTime functionStartDate = DateTime.UtcNow;
            string sessionId = Guid.NewGuid().ToString();

            FunctionContext context = new FunctionContext()
            {
                CollectorType = "DiscoverOrganizations",
                CollectorIdentity = this.configManager.GetCollectorIdentity(),
                FunctionStartDate = functionStartDate,
                SessionId = sessionId,
                InvocationId = executionContext.InvocationId.ToString(),
            };

            ITelemetryClient telemetryClient = new GitHubApplicationInsightsTelemetryClient(this.telemetryClient, context);
            try
            {
                ICache<RateLimitTableEntity> rateLimiterCache = new AzureTableCache<RateLimitTableEntity>(telemetryClient, "ratelimiter");
                await rateLimiterCache.InitializeAsync().ConfigureAwait(false);
                IRateLimiter rateLimiter = new GitHubRateLimiter("*", rateLimiterCache, this.httpClient, telemetryClient, maxUsageBeforeDelayStarts: 100, this.apiDomain);
                ICache<ConditionalRequestTableEntity> requestsCache = new AzureTableCache<ConditionalRequestTableEntity>(telemetryClient, "requests");
                await requestsCache.InitializeAsync().ConfigureAwait(false);
                GitHubHttpClient httpClient = new GitHubHttpClient(this.httpClient, rateLimiter, requestsCache, telemetryClient);
                IAuthentication auth = this.configManager.GetAuthentication(CollectorType.DiscoverOrganizations, httpClient, null, this.apiDomain, telemetryClient, this.ifxLogger);

                // get existing organizations
                string existingOrganizations = "[]";
                try
                {
                    existingOrganizations = await AzureHelpers.GetBlobContentUsingMsiAsync("github-settings", "generated-organizations.json", storageAccountNameEnvironmentVariable).ConfigureAwait(false);
                }
                catch (StorageException)
                {
                    existingOrganizations = await AzureHelpers.GetBlobContentUsingMsiAsync("github-settings", "organizations.json", storageAccountNameEnvironmentVariable).ConfigureAwait(false);
                }
                
                // check that collector functions are using github app authentication.
                if (!(auth is GitHubAppAuthentication))
                {
                    Dictionary<string, string> properties = new Dictionary<string, string>()
                    {
                        { "Collector", CollectorType.DiscoverOrganizations.ToString() }
                    };
                    telemetryClient.TrackEvent("Invalid Authentication", properties);
                    return;
                }

                // get all organizations where the GitHub app is installed.
                GitHubAppAuthentication githubAuth = (GitHubAppAuthentication)auth;
                List<JObject> installations = await githubAuth.GetAppInstallations().ConfigureAwait(false);

                // build configuration.json from organizations in JArray.
                JArray formattedInstallations = new JArray();
                Dictionary<int, string> discoveredOrganizationMap = new Dictionary<int, string>();
                foreach (JObject installation in installations)
                {
                    JObject formattedInstallation = new JObject();
                    formattedInstallation.Add("OrganizationLogin", installation.SelectToken("account.login"));
                    formattedInstallation.Add("OrganizationId", installation.SelectToken("account.id"));
                    formattedInstallations.Add(formattedInstallation);

                    int id = installation.SelectToken("account.id").Value<int>();
                    string login = installation.SelectToken("account.login").Value<string>();

                    discoveredOrganizationMap[id] = login;
                }

                formattedInstallations = new JArray(formattedInstallations.OrderBy(formattedInstallation => (string)formattedInstallation["OrganizationLogin"]));
                string configurationString = formattedInstallations.ToString(Formatting.Indented);

                // output to blob.
                 await AzureHelpers.WriteToBlob("github-settings", "generated-organizations.json", configurationString).ConfigureAwait(false);

                // find organization diff.
                
                JArray organizationsArray = JArray.Parse(existingOrganizations);

                foreach (JObject organization in organizationsArray)
                {
                    int id = organization.SelectToken("OrganizationId").Value<int>();
                    if( discoveredOrganizationMap.ContainsKey(id) )
                    {
                        discoveredOrganizationMap.Remove(id);
                    }
                }

                // add messages to auto-onbaording queue (moved manually to onboarding after PR is merged)
                foreach (int id in discoveredOrganizationMap.Keys )
                {
                    OnboardingInput onboardingInput = new OnboardingInput()
                    {
                        OnboardingType = OnboardingType.Organization,
                        OrganizationId = id,
                        OrganizationLogin = discoveredOrganizationMap[id]
                    };

                    IQueue onboardingQueue = new CloudQueueMsiWrapper("onboarding-auto", storageAccountNameEnvironmentVariable, telemetryClient);
                    await onboardingQueue.PutObjectAsJsonStringAsync(onboardingInput, TimeSpan.MaxValue).ConfigureAwait(false);
                }


            }
            catch (Exception exception)
            {
                telemetryClient.TrackException(exception);
                throw;
            }
        }

        [Disable("ALL_FUNCTIONS_DISABLED")]
        [FunctionName("Heartbeat")]
        public void Heartbeat([TimerTrigger("0 * * * * *", RunOnStartup = false)] TimerInfo timerInfo, ExecutionContext executionContext, ILogger logger)
        {
            using Activity SessionActivity = GetInvocationActivity(executionContext, timerInfo).Start();
            // Todo : add monitoring info to heartbeat (poison queue checks, failed session invocation IDs)
            HeartbeatMetric.Add(1);
        }

        private Activity GetInvocationActivity(ExecutionContext context, string queueItem, int dequeueCount)
        {
            Activity trace = OpenTelemetryTracer.GetActivity(FunctionInvocationTraceName);
            trace.AddTag("InvocationId", context.InvocationId.ToString());
            trace.AddTag("FunctionName", context.FunctionName);
            trace.AddTag("DequeueCount", dequeueCount);
            trace.AddTag("QueueItem", queueItem);
            return trace;
        }

        private Activity GetInvocationActivity(ExecutionContext context, TimerInfo timerInfo)
        {
            Activity trace = OpenTelemetryTracer.GetActivity(FunctionInvocationTraceName);
            trace.AddTag("InvocationId", context.InvocationId.ToString());
            trace.AddTag("FunctionName", context.FunctionName);
            trace.AddTag("IsPastDue", timerInfo.IsPastDue);
            return trace;
        }

        private Activity GetInvocationActivity(OrchestrationContext context)
        {
            Activity trace = OpenTelemetryTracer.GetActivity(FunctionInvocationTraceName);
            trace.AddTag("InvocationId", context.InvocationId.ToString());
            trace.AddTag("FunctionName", context.CollectorType);
            return trace;
        }

        private static Dictionary<string, string> GetRepositoryCollectorSessionStartEventProperties(FunctionContext context, string identifier, Repository repository)
        {
            return new Dictionary<string, string>(GetCollectorCommonSessionStartEventProperties(context, identifier))
            {
                { "OrganizationLogin", repository.OrganizationLogin },
                { "OrganizationId", repository.OrganizationId.ToString() },
                { "RepositoryName", repository.RepositoryName },
                { "RepositoryId", repository.RepositoryId.ToString() },
            };
        }
    }
}
