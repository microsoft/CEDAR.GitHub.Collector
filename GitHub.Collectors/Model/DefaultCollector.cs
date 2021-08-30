// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Context;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Collectors.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Newtonsoft.Json.Linq;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class DefaultCollector : ICollector
    {
        public static readonly HttpResponseSignature UserNotFoundResponse = new HttpResponseSignature(HttpStatusCode.NotFound, "Not Found");
        public static readonly HttpResponseSignature ResourceNotAccessibleByIntegrationResponse = new HttpResponseSignature(HttpStatusCode.Forbidden, "Resource not accessible by integration");

        public const string OrganizationInstanceRecordType = "GitHub.OrgIstance";
        public const string UserInstanceRecordType = "GitHub.UserInstance";

        protected FunctionContext FunctionContext { get; private set; }
        protected GitHubHttpClient HttpClient { get; private set; }
        protected List<IRecordWriter> RecordWriters { get; private set; }
        protected ICache<RepositoryItemTableEntity> Cache { get; private set; }
        protected ITelemetryClient TelemetryClient { get; private set; }
        protected IAuthentication Authentication { get; private set; }

        public DefaultCollector(FunctionContext functionContext,
                                IAuthentication authentication,
                                GitHubHttpClient httpClient,
                                List<IRecordWriter> recordWriters,
                                ICache<RepositoryItemTableEntity> cache,
                                ITelemetryClient telemetryClient)
        {
            this.FunctionContext = functionContext;
            this.HttpClient = httpClient;
            this.RecordWriters = recordWriters;
            this.Cache = cache;
            this.TelemetryClient = telemetryClient;
            this.Authentication = authentication;
        }

        public virtual async Task ProcessWebhookPayloadAsync(JObject jsonObject, Repository repository)
        {
            JToken organizationUrlToken = jsonObject.SelectToken($"$.organization.url");
            if (organizationUrlToken != null)
            {
                string organizationUrl = organizationUrlToken.Value<string>();
                await this.ProcessUrlAsync(organizationUrl, OrganizationInstanceRecordType, allowlistedResponses: new List<HttpResponseSignature>()).ConfigureAwait(false);
            }

            JToken senderUrlToken = jsonObject.SelectToken($"$.sender.url");
            if (senderUrlToken != null)
            {
                string senderUrl = senderUrlToken.Value<string>();
                // There are cases where we receive this payload for removed team members, members no longer exist in GitHub. Therefore, the following call can fail with 404 not found.
                // When using a GitHub app, the same request fails with 403: "Resource not accessible by integration" response.
                List<HttpResponseSignature> allowlistedResponses = new List<HttpResponseSignature>()
                {
                    UserNotFoundResponse,
                    ResourceNotAccessibleByIntegrationResponse,
                };
                await this.ProcessUrlAsync(senderUrl, UserInstanceRecordType, allowlistedResponses).ConfigureAwait(false);
            }
        }

        private async Task ProcessUrlAsync(string requestUrl, string recordType, List<HttpResponseSignature> allowlistedResponses)
        {
            HttpResponseMessage response = await this.HttpClient.GetConditionalViaETagAsync(requestUrl, recordType, this.Authentication, allowlistedResponses).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                // Permitted allowlisted response, but we should not serialize it.
                return;
            }

            JObject record = await HttpUtility.ParseAsJObjectAsync(response).ConfigureAwait(false);
            RecordContext context = new RecordContext()
            {
                RecordType = recordType,
                AdditionalMetadata = new Dictionary<string, JToken>()
                {
                    { "OriginatingUrl", requestUrl },
                },
            };
            foreach (IRecordWriter recordWriter in this.RecordWriters)
            {
                await recordWriter.WriteRecordAsync(record, context).ConfigureAwait(false);
            }
        }
    }
}
