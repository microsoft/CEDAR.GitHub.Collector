﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Telemetry;
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

        public GitHubRateLimiter(string organizationId, ICache<RateLimitTableEntity> rateLimiterCache, IHttpClient httpClient, ITelemetryClient telemetryClient, double maxUsageBeforeDelayStarts, string apiDomain)
            : base(RateLimitTableEntity.GlobalOrganizationId, organizationName: organizationId.Equals(RateLimitTableEntity.GlobalOrganizationId) ? string.Empty : organizationId, rateLimiterCache, telemetryClient, expectRateLimitingHeaders: true)
        {
            this.httpClient = httpClient;
            this.maxUsageBeforeDelayStarts = maxUsageBeforeDelayStarts;
            this.usageCheckUrl = $"https://{apiDomain}/rate_limit";
        }

        protected override async Task WaitIfNeededAsync(IAuthentication authentication, RateLimitTableEntity tableEntity)
        {
            long rateLimitLimit = tableEntity.RateLimitLimit;
            long rateLimitRemaining = tableEntity.RateLimitRemaining;
            double usage = 100.0 - rateLimitRemaining * 100.0 / rateLimitLimit;

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

                    await Task.Delay(delay).ConfigureAwait(false);
                    await this.WaitIfNeededAsync(authentication);

                    return;
                }
            }

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

                await Task.Delay(DelayWhileCheckingUsage).ConfigureAwait(false);

                HttpResponseMessage usageCheckResponse = await this.httpClient.GetAsync(usageCheckUrl, authentication, GitHubHttpClient.GitHubProductInfoHeaderValue).ConfigureAwait(false);
                await this.UpdateStatsAsync(authentication.Identity, requestUrl: usageCheckUrl, usageCheckResponse).ConfigureAwait(false);
                await this.WaitIfNeededAsync(authentication);
            }
        }
    }
}
