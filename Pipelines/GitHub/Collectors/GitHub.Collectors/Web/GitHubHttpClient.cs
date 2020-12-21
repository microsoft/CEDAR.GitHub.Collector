// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Collector;
using Microsoft.CloudMine.Core.Collectors.Error;
using Microsoft.CloudMine.Core.Collectors.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Newtonsoft.Json.Linq;

namespace Microsoft.CloudMine.GitHub.Collectors.Web
{
    // ToDo: kivancm: further abstract the common parts of AzureDevOpsHttpClient and GitHubHttpClient into the Core library.
    public class GitHubHttpClient : IWebRequestStatsTracker
    {
        private readonly IHttpClient httpClient;
        private readonly IRateLimiter rateLimiter;
        private readonly ICache<ConditionalRequestTableEntity> requestCache;
        private readonly ITelemetryClient telemetryClient;

        public static ProductInfoHeaderValue GitHubProductInfoHeaderValue = new ProductInfoHeaderValue("CloudMineGitHubCollector", "1.0.0");

        public const int MaxRequestExceptionCount = 10;
        public static readonly TimeSpan[] DelayBeforeRequestExceptions = Enumerable.Repeat(TimeSpan.FromSeconds(5), MaxRequestExceptionCount).ToArray();

        public int SuccessfulRequestCount => this.successfulRequestCount;
        private volatile int successfulRequestCount;
        public int FailedRequestCount => this.failedRequestCount;
        private volatile int failedRequestCount;

        private readonly RetryRule[] retryRuleCollection;

        public GitHubHttpClient(IHttpClient httpClient, IRateLimiter rateLimiter, ICache<ConditionalRequestTableEntity> requestCache, ITelemetryClient telemetryClient)
        {
            this.httpClient = httpClient;
            this.rateLimiter = rateLimiter;
            this.requestCache = requestCache;
            this.telemetryClient = telemetryClient;

            this.successfulRequestCount = 0;
            this.failedRequestCount = 0;

            this.retryRuleCollection = new RetryRule[]
            {
                RetryRules.GatewayTimeoutRetryRule(),
                RetryRules.BadGatewayRetryRule(),
                RetryRules.InternalServerErrorRetryRule(),
                RetryRules.RateLimiterAbuseRetryRule(),
            };
        }

        public async Task<HttpResponseMessage> GetConditionalViaETagAsync(string requestUrl, string recordType, IAuthentication authentication, List<HttpResponseSignature> allowlistedResponses)
        {
            string eTag = string.Empty;
            ConditionalRequestTableEntity cachedRequest = await this.requestCache.RetrieveAsync(new ConditionalRequestTableEntity(requestUrl)).ConfigureAwait(false);
            if (cachedRequest != null)
            {
                eTag = cachedRequest.GitHubETag;
            }

            HttpResponseMessage result = await this.MakeRequestAsync(requestUrl, authentication, apiName: recordType, eTag, allowlistedResponses, async () => await this.httpClient.GetAsync(requestUrl, authentication, GitHubProductInfoHeaderValue, eTag).ConfigureAwait(false));

            if (result.StatusCode == HttpStatusCode.OK)
            {
                eTag = string.Empty;
                if (result.Headers.TryGetValues("ETag", out IEnumerable<string> eTagValues))
                {
                    eTag = eTagValues.First();
                }

                await this.requestCache.CacheAsync(new ConditionalRequestTableEntity(requestUrl, recordType, eTag)).ConfigureAwait(false);
            }

            return result;
        }

        public Task<HttpResponseMessage> GetAsync(string requestUrl, IAuthentication authentication, string apiName, List<HttpResponseSignature> allowlistedResponses)
        {
            return this.MakeRequestAsync(requestUrl, authentication, apiName, eTag: string.Empty, allowlistedResponses, () => this.httpClient.GetAsync(requestUrl, authentication, GitHubProductInfoHeaderValue, eTag: string.Empty));
        }

        public async Task<HttpResponseMessage> MakeRequestAsync(string requestUrl, IAuthentication authentication, string apiName, string eTag, List<HttpResponseSignature> allowlistedResponses, Func<Task<HttpResponseMessage>> httpMethodCallback)
        {
            // New request, clear retry rules (attempt indices)
            foreach (RetryRule retryRule in this.retryRuleCollection)
            {
                retryRule.Clear();
            }

            HttpResponseMessage response = null;
            int exceptionCount = 0;
            TimeSpan delayBeforeRetry = TimeSpan.Zero;
            bool shallRetry = true;
            while (shallRetry)
            {
                if (delayBeforeRetry != TimeSpan.Zero)
                {
                    await Task.Delay(delayBeforeRetry).ConfigureAwait(false);
                }

                await this.rateLimiter.WaitIfNeededAsync(authentication).ConfigureAwait(false);

                DateTime webRequestDateTime = DateTime.UtcNow;

                try
                {
                    response = await httpMethodCallback();
                    (shallRetry, delayBeforeRetry) = await this.ShallRetryAsync(response, requestUrl, authentication.Identity).ConfigureAwait(false);
                }
                catch (Exception requestException)
                {
                    exceptionCount++;
                    (shallRetry, delayBeforeRetry) = await this.ShallRetryAsync(requestException, requestUrl, exceptionCount, authentication.Identity).ConfigureAwait(false);
                    if (!shallRetry)
                    {
                        throw requestException;
                    }
                }
                TimeSpan elapsed = DateTime.UtcNow - webRequestDateTime;

                this.telemetryClient.TrackRequest(authentication.Identity, apiName, requestUrl, eTag, elapsed, response);

                await this.rateLimiter.UpdateStatsAsync(authentication.Identity, requestUrl, response).ConfigureAwait(false);
            }

            await this.ThrowOnFatalResponseAsync(response, requestUrl, exceptionCount, authentication.Identity, allowlistedResponses).ConfigureAwait(false);
            this.successfulRequestCount++;
            return response;
        }

        private async Task<Tuple<bool, TimeSpan>> ShallRetryAsync(Exception requestException, string requestUrl, int exceptionCount, string identity)
        {
            bool shallRetry = false;
            TimeSpan delayBeforeRetry = TimeSpan.Zero;

            if (requestException is AggregateException aggregateException)
            {
                foreach (Exception innerException in aggregateException.InnerExceptions)
                {
                    (shallRetry, delayBeforeRetry) = await this.ShallRetryAsync(innerException, requestUrl, exceptionCount, identity);
                    if (shallRetry)
                    {
                        // Found a signature match for one of the inner exceptions, retry.
                        return Tuple.Create(shallRetry, delayBeforeRetry);
                    }
                }

                // No match for inner exceptions, return false.
                return Tuple.Create(false, TimeSpan.Zero);
            }

            if (requestException is HttpRequestException && requestException.Message.Equals("Error while copying content to a stream."))
            {
                // Known flaky server issue with GitHub.
                shallRetry = true;
                delayBeforeRetry = DelayBeforeRequestExceptions[exceptionCount - 1];
            }

            if (shallRetry)
            {
                // Track web exception in telemetry. No need to track fatal (non-retried) exceptions since they will be tracked elsewhere.

                Dictionary<string, string> properties = new Dictionary<string, string>()
                {
                    { "Url", requestUrl },
                    { "Identity", identity },
                    { "AttemptIndex", exceptionCount.ToString() },
                    { "Retried", true.ToString() },
                    { "Fatal", false.ToString() },
                    { "DelayBeforeRetry", delayBeforeRetry.ToString() },
                };
                this.telemetryClient.TrackException(requestException, "Web request failed.", properties);
            }

            return Tuple.Create(shallRetry, delayBeforeRetry);
        }

        private async Task<Tuple<bool, TimeSpan>> ShallRetryAsync(HttpResponseMessage response, string requestUrl, string identity)
        {
            bool shallRetry = false;
            TimeSpan delayBeforeRetry = TimeSpan.Zero;

            long attemptIndex = -1;
            foreach (RetryRule retryRule in this.retryRuleCollection)
            {
                TimeSpan[] delayBeforeRetries = retryRule.DelayBeforeRetries;
                if (retryRule.AttemptIndex > delayBeforeRetries.Length)
                {
                    // No more attempts for this retry rule, bail out.
                    continue;
                }

                bool matches = await retryRule.ShallRetryAsync(response).ConfigureAwait(false);
                if (matches)
                {
                    retryRule.Consume();
                    attemptIndex = retryRule.AttemptIndex;
                    shallRetry = true;
                    delayBeforeRetry = delayBeforeRetries[retryRule.AttemptIndex - 1];

                    // If there is a RetryAfter already provided as part of the response, honor that instead of our internal delay.
                    long retryAfter = RateLimiter.GetRetryAfter(response.Headers);
                    if (retryAfter != long.MinValue)
                    {
                        delayBeforeRetry = TimeSpan.FromSeconds(retryAfter);
                        await this.rateLimiter.UpdateRetryAfterAsync(identity, requestUrl, response).ConfigureAwait(false);
                    }

                    break;
                }
            }

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotModified && shallRetry)
            {
                // Track web exception in telemetry. No need to track fatal (non-retried) exceptions since they will be tracked elsewhere.
                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                HttpStatusCode responseStatusCode = response.StatusCode;

                Dictionary<string, string> properties = new Dictionary<string, string>()
                {
                    { "Url", requestUrl },
                    { "Identity", identity },
                    { "ResponseContent", responseContent },
                    { "ResponseStatusCode", responseStatusCode.ToString() },
                    { "AttemptIndex", attemptIndex.ToString() },
                    { "Retried", true.ToString() },
                    { "Fatal", false.ToString() },
                    { "DelayBeforeRetry", delayBeforeRetry.ToString() },
                };
                Exception exception = new Exception($"Request to url '{requestUrl}' failed with status code: '{responseStatusCode}'. Response content: '{responseContent}'.");
                this.telemetryClient.TrackException(exception, "Web request failed.", properties);
            }

            return Tuple.Create(shallRetry, delayBeforeRetry);
        }

        private async Task ThrowOnFatalResponseAsync(HttpResponseMessage response, string requestUrl, int attemptIndex, string identity, List<HttpResponseSignature> allowlistedResponses)
        {
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotModified)
            {
                return;
            }

            string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            HttpStatusCode responseStatusCode = response.StatusCode;
            try
            {
                JObject responseContentObject = JObject.Parse(responseContent);
                string message = responseContentObject.SelectToken("$.message").Value<string>();

                foreach (HttpResponseSignature allowlistedResponse in allowlistedResponses)
                {
                    if (allowlistedResponse.statusCode == responseStatusCode && allowlistedResponse.Matches(responseStatusCode, message))
                    {
                        Dictionary<string, string> allowlistedResponseProperties = new Dictionary<string, string>()
                        {
                            { "RequestUrl", requestUrl },
                            { "ResponseStatusCode", responseStatusCode.ToString() },
                            { "ResponseMessage", message },
                        };
                        this.telemetryClient.TrackEvent("AllowlistedResponse", allowlistedResponseProperties);

                        return;
                    }
                }
            }
            catch (Exception)
            {
                // Done as best effort, ignore since the response content is already logged.
            }

            Exception fatalException = new FatalException($"Request to url '{requestUrl}' failed with status code: '{responseStatusCode}'. Response content: '{responseContent}'.");
            Dictionary<string, string> properties = new Dictionary<string, string>()
            {
                { "Url", requestUrl },
                { "Identity", identity },
                { "ResponseContent", responseContent },
                { "ResponseStatusCode", responseStatusCode.ToString() },
                { "AttemptIndex", attemptIndex.ToString() },
                { "Retried", false.ToString() },
                { "Fatal", true.ToString() },
            };
            this.telemetryClient.TrackException(fatalException, "Web request failed.", properties);
            this.failedRequestCount++;
            throw fatalException;
        }

        public async Task<JObject> GetAndParseAsJObjectAsync(string requestUrl, IAuthentication authentication, string apiName, List<HttpResponseSignature> allowlistedResponses)
        {
            HttpResponseMessage response = await this.GetAsync(requestUrl, authentication, apiName, allowlistedResponses).ConfigureAwait(false);
            return await HttpUtility.ParseAsJObjectAsync(response).ConfigureAwait(false);
        }

        public async Task<JArray> GetAndParseAsJArrayAsync(string requestUrl, IAuthentication authentication, string apiName, List<HttpResponseSignature> allowlistedResponses)
        {
            HttpResponseMessage response = await this.GetAsync(requestUrl, authentication, apiName, allowlistedResponses).ConfigureAwait(false);
            return await HttpUtility.ParseAsJArrayAsync(response).ConfigureAwait(false);
        }

        public async Task<JObject> PostAndParseAsJObjectAsync(string requestUrl, string requestBody, IAuthentication authentication, string apiName, List<HttpResponseSignature> allowlistedResponses)
        {
            HttpResponseMessage response = await this.MakeRequestAsync(requestUrl,
                                                                       authentication,
                                                                       apiName,
                                                                       eTag: String.Empty,
                                                                       allowlistedResponses,
                                                                       () => this.httpClient.PostAsync(requestUrl, requestBody, authentication, GitHubProductInfoHeaderValue)
            ).ConfigureAwait(false);
            return await HttpUtility.ParseAsJObjectAsync(response).ConfigureAwait(false);
        }
    }
}
