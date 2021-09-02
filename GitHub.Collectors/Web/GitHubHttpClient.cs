// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Web;

namespace Microsoft.CloudMine.GitHub.Collectors.Web
{
    // ToDo: kivancm: further abstract the common parts of AzureDevOpsHttpClient and GitHubHttpClient into the Core library.
    public class GitHubHttpClient : GitHubHttpClientBase
    {
        private readonly ICache<ConditionalRequestTableEntity> requestCache;

        public GitHubHttpClient(IHttpClient httpClient, IRateLimiter rateLimiter, ICache<ConditionalRequestTableEntity> requestCache, ITelemetryClient telemetryClient)
            : base(httpClient, rateLimiter, telemetryClient)
        {
            this.requestCache = requestCache;
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
    }
}
