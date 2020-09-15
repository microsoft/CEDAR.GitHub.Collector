// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Web
{
    public static class RetryRules
    {
        private static readonly TimeSpan[] LinearFastRetryStrategyDelays = Enumerable.Repeat(TimeSpan.FromSeconds(5), 15).ToArray();
        private static readonly TimeSpan[] ExponentialBackoffFastRetryStrategyDelays = new TimeSpan[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5) };
        private static readonly TimeSpan[] ExponentialBackoffSlowRetryStrategyDelays = new TimeSpan[] { TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5) };

        public static readonly RetryRule GatewayTimeoutRetryRule = new RetryRule()
        {
            ShallRetryAsync = response => Task.FromResult(response.StatusCode == HttpStatusCode.GatewayTimeout),
            DelayBeforeRetries = ExponentialBackoffFastRetryStrategyDelays,
        };

        public static readonly RetryRule BadGatewayRetryRule = new RetryRule()
        {
            ShallRetryAsync = response => Task.FromResult(response.StatusCode == HttpStatusCode.BadGateway),
            // Uses fast linear retry for BadGateway errors. The GitHub API will sometimes return this error transiently for several requests in a row.
            DelayBeforeRetries = LinearFastRetryStrategyDelays,
        };

        public static readonly RetryRule InternalServerErrorRetryRule = new RetryRule()
        {
            ShallRetryAsync = response => Task.FromResult(response.StatusCode == HttpStatusCode.InternalServerError),
            DelayBeforeRetries = ExponentialBackoffFastRetryStrategyDelays,
        };

        public static readonly RetryRule RateLimiterAbuseRetryRule = new RetryRule()
        {
            ShallRetryAsync = async response =>
            {
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    try
                    {
                        JObject responseObject = await HttpUtility.ParseAsJObjectAsync(response).ConfigureAwait(false);
                        string responseMessage = responseObject.SelectToken("$.message").Value<string>();
                        return responseMessage.Equals("You have triggered an abuse detection mechanism. Please wait a few minutes before you try again.");
                    }
                    catch (Exception)
                    {
                        // Done as best effort, ignore the exception since the error will be logged anyways.
                    }
                }

                return false;
            },
            DelayBeforeRetries = ExponentialBackoffSlowRetryStrategyDelays,
        };
    }
}
