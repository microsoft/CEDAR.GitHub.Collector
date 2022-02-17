// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Web;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Web
{
    public class GitHubRateLimiter : RateLimiter
    {
        public static readonly TimeSpan DelayWhileCheckingUsage = TimeSpan.FromMinutes(1);
        public readonly string usageCheckUrl;

        private readonly IHttpClient httpClient;
        private readonly double maxUsageBeforeDelayStarts;
        private readonly bool throwOnRateLimit;

        /// <summary>
        /// GitHub rate-limiting is very simple (a single-dimensional number) and gets changed immediately after a request is done.
        /// Therefore, lookup the current value before each request.
        /// </summary>
        private static readonly TimeSpan CacheInvalidationFrequency = TimeSpan.FromTicks(0);

        public GitHubRateLimiter(string organizationId, ICache<RateLimitTableEntity> rateLimiterCache, IHttpClient httpClient, ITelemetryClient telemetryClient, double maxUsageBeforeDelayStarts, string apiDomain, bool throwOnRateLimit = false)
            : base(RateLimitTableEntity.GlobalOrganizationId, organizationName: organizationId.Equals(RateLimitTableEntity.GlobalOrganizationId) ? string.Empty : organizationId, rateLimiterCache, telemetryClient, expectRateLimitingHeaders: true, CacheInvalidationFrequency)
        {
            this.httpClient = httpClient;
            this.maxUsageBeforeDelayStarts = maxUsageBeforeDelayStarts;
            this.usageCheckUrl = $"https://{apiDomain}/rate_limit";
            this.throwOnRateLimit = throwOnRateLimit;
        }

        protected override async Task WaitIfNeededAsync(IAuthentication authentication, RateLimitTableEntity tableEntity)
        {
            DateTime? retryAfter = tableEntity.RetryAfter;
            // Honor retry-after if exists.
            if (retryAfter.HasValue)
            {
                TimeSpan delay = retryAfter.Value - DateTime.UtcNow;
                if (delay > TimeSpan.FromMilliseconds(1))
                {
                    Dictionary<string, string> properties = new Dictionary<string, string>()
                    {
                        { "Delay", delay.ToString() },
                        { "Reason", "Honor Retry-After" },
                    };
                    this.TelemetryClient.TrackEvent("RateLimiterDelay", properties);

                    if (this.throwOnRateLimit)
                    {
                        throw new GitHubRateLimitException(delay);
                    }
                    await Task.Delay(delay).ConfigureAwait(false);
                    await this.WaitIfNeededAsync(authentication);

                    return;
                }
            }

            long rateLimitLimit = tableEntity.RateLimitLimit;
            long rateLimitRemaining = tableEntity.RateLimitRemaining;
            double usage = 100.0 - rateLimitRemaining * 100.0 / rateLimitLimit;
            if (usage > this.maxUsageBeforeDelayStarts)
            {
                TimeSpan maxDelay = tableEntity.RateLimitReset.Value.Subtract(DateTime.UtcNow);
                Dictionary<string, string> properties = new Dictionary<string, string>()
                {
                    { "Delay", DelayWhileCheckingUsage.ToString() },
                    { "MaxDelay", maxDelay.ToString() },
                    { "Usage", $"{usage:F2}" },
                    { "Reason", "Usage Threshold" },
                };
                this.TelemetryClient.TrackEvent("RateLimiterDelay", properties);

                if (this.throwOnRateLimit)
                {
                    throw new GitHubRateLimitException(maxDelay);
                }

                await Task.Delay(DelayWhileCheckingUsage).ConfigureAwait(false);

                HttpResponseMessage usageCheckResponse = await this.httpClient.GetAsync(usageCheckUrl, authentication, GitHubHttpClient.GitHubProductInfoHeaderValue).ConfigureAwait(false);
                await this.UpdateStatsAsync(authentication.Identity, requestUrl: usageCheckUrl, usageCheckResponse).ConfigureAwait(false);
                await this.WaitIfNeededAsync(authentication);
            }
        }
    }
}
