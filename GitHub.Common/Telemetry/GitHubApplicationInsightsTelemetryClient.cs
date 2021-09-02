// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ApplicationInsights;
using Microsoft.CloudMine.Core.Collectors.Context;
using Microsoft.CloudMine.Core.Collectors.Telemetry;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace Microsoft.CloudMine.GitHub.Telemetry
{
    public class GitHubApplicationInsightsTelemetryClient : ApplicationInsightsTelemetryClient
    {
        public GitHubApplicationInsightsTelemetryClient(TelemetryClient telemetryClient, FunctionContext context, ILogger logger = null)
            : base(telemetryClient, context, logger)
        {
        }

        public override void TrackRequest(string identity, string apiName, string requestUrl, string eTag, TimeSpan duration, HttpResponseMessage responseMessage)
        {
            base.TrackRequest(identity, apiName, requestUrl, eTag, duration, responseMessage);

            // ToDo: kivancm: consider changing these into metrics.
            responseMessage.Headers.TryGetValues("X-RateLimit-Limit", out IEnumerable<string> rateLimitLimitValues);
            string rateLimitLimit = rateLimitLimitValues != null && rateLimitLimitValues.Any() ? rateLimitLimitValues.First() : null;

            responseMessage.Headers.TryGetValues("X-RateLimit-Remaining", out IEnumerable<string> rateLimitRemainingValues);
            string rateLimitRemaining = rateLimitRemainingValues != null && rateLimitRemainingValues.Any() ? rateLimitRemainingValues.First() : null;

            // Note: the following should always exist in a GitHub request. However, even if they don't it is not (and should not be) fatal to the execution.
            if (rateLimitRemaining != null && rateLimitLimit != null)
            {
                // Track rate-limiting details as a custom event
                Dictionary<string, string> properties = new Dictionary<string, string>()
                {
                    { "Identity", identity },
                    { "RateLimitLimit", rateLimitLimit },
                    { "RateLimitRemaining", rateLimitRemaining },
                };
                this.TrackEvent("RateLimiter", properties);
            }
        }
    }
}
