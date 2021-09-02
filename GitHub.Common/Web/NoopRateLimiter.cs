// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Web;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Web
{
    public class NoopRateLimiter : IRateLimiter
    {
        public Task UpdateRetryAfterAsync(string identity, string requestUrl, HttpResponseMessage response)
        {
            // Assume success.
            return Task.CompletedTask;
        }

        public Task UpdateStatsAsync(string identity, string requestUrl, HttpResponseMessage response)
        {
            // Assume success.
            return Task.CompletedTask;
        }

        public Task WaitIfNeededAsync(IAuthentication authentication)
        {
            // Assume success.
            return Task.CompletedTask;
        }
    }
}
