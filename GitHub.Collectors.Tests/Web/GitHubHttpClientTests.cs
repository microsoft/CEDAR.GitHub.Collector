// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Tests.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Tests.Web;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using Microsoft.CloudMine.GitHub.Collectors.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Web.Tests
{
    [TestClass]
    public class GitHubHttpClientTests
    {
        private GitHubHttpClient gitHubHttpClient;
        private FixedHttpClient httpClient;

        [TestInitialize]
        public void Setup()
        {
            this.httpClient = new FixedHttpClient();
            this.gitHubHttpClient = new GitHubHttpClient(this.httpClient, new NoopRateLimiter(), new NoopCache<ConditionalRequestTableEntity>(), new NoopTelemetryClient());
        }

        [TestMethod]
        public async Task GetAndParseAsJObjectAsync_Success()
        {
            string responseMessage = @$"{{""message"": ""success""}}";
            string requestUrl = "https://api.github.com/rate_limit";

            this.httpClient.AddResponse(requestUrl, HttpStatusCode.OK, responseMessage);

            JObject response = await this.gitHubHttpClient.GetAndParseAsJObjectAsync(requestUrl, new BasicAuthentication("Identity", "PersonalAccessToken"), apiName: string.Empty, whitelistedResponses: new List<HttpResponseSignature>()).ConfigureAwait(false);
            Assert.AreEqual("success", response.SelectToken("$.message").Value<string>());
        }

        [TestMethod]
        public async Task WhitelistedResponse_NoCommitFoundForSha()
        {
            string commitSha = "1234567890123456789012345678901234567890";
            string responseMessage = @$"{{""message"": ""{PushCollector.NoCommitShaFoundResponse(commitSha)}""}}";
            string requestUrl = $"https://api.github.com/repos/OrganizationLogin/RepositoryName/commits/{commitSha}";

            this.httpClient.AddResponse(requestUrl, HttpStatusCode.UnprocessableEntity, responseMessage);

            List<HttpResponseSignature> whitelistedResponses = new List<HttpResponseSignature>()
            {
                new HttpResponseSignature(HttpStatusCode.UnprocessableEntity, PushCollector.NoCommitShaFoundResponse(commitSha)),
            };

            JObject response = await this.gitHubHttpClient.GetAndParseAsJObjectAsync(requestUrl, new BasicAuthentication("Identity", "PersonalAccessToken"), apiName: string.Empty, whitelistedResponses).ConfigureAwait(false);
            Assert.AreEqual(PushCollector.NoCommitShaFoundResponse(commitSha), response.SelectToken("$.message").Value<string>());
        }

        [TestMethod]
        public async Task WhitelistedResponse_GitRepositoryIsEmpty()
        {
            string responseMessage = @$"{{""message"": ""Git Repository is empty.""}}";
            string requestUrl = $"https://api.github.com/repos/OrganizationLogin/RepositoryName/commits?per_page=100";

            this.httpClient.AddResponse(requestUrl, HttpStatusCode.Conflict, responseMessage);

            List<HttpResponseSignature> whitelistedResponses = new List<HttpResponseSignature>()
            {
                new HttpResponseSignature(HttpStatusCode.Conflict, "Git Repository is empty."),
            };

            JObject response = await this.gitHubHttpClient.GetAndParseAsJObjectAsync(requestUrl, new BasicAuthentication("Identity", "PersonalAccessToken"), apiName: string.Empty, whitelistedResponses).ConfigureAwait(false);
            Assert.AreEqual("Git Repository is empty.", response.SelectToken("$.message").Value<string>());
        }
    }
}
