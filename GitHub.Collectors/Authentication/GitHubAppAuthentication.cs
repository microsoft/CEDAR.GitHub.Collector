// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Identity;
using Microsoft.Cloud.InstrumentationFramework;
using Microsoft.CloudMine.Core.Auditing;
using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Error;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.Core.Telemetry;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Authentication
{
    public class GitHubAppAuthentication : GitHubAppAuthenticationBase, IAuthentication, IGitHubAppAuthentication
    {
        private const string TokenType = "GitHubApp";
        private GitHubHttpClient httpClient;
        private string apiDomain;
        private readonly IAuditLogger auditLogger;
        private readonly ITelemetryClient telemetryClient;

        /// <summary>
        /// Maps an organization name to a Tuple containing an expiry date and the token itself.
        /// </summary>
        private static ConcurrentDictionary<string, Tuple<DateTime, string>> TokenCache = new ConcurrentDictionary<string, Tuple<DateTime, string>>();
        private static ConcurrentDictionary<string, string> OrgNameToInstallationIdMap = new ConcurrentDictionary<string, string>();

        public GitHubAppAuthentication(int appId, GitHubHttpClient httpClient, string organization, string apiDomain, string gitHubAppKeyVaultUri, bool useInteractiveLogin, IAuditLogger auditLogger, ITelemetryClient telemetryClient)
            : base(organization, appId, gitHubAppKeyVaultUri, useInteractiveLogin)
        {
            this.httpClient = httpClient;
            this.apiDomain = apiDomain;
            this.telemetryClient = telemetryClient;
            this.auditLogger = auditLogger;
        }

        public Dictionary<string, string> AdditionalWebRequestHeaders => new Dictionary<string, string>();

        public string Identity { get => this.appId + "-" + this.organization; }

        public string Schema => "Bearer";

        public Task<string> GetAuthorizationHeaderAsync()
        {
            if (this.httpClient == null || this.apiDomain == null)
            {
                throw new FatalTerminalException("dependencies must be set before generating authorization header");
            }

            if (TokenCache.TryGetValue(this.organization, out Tuple<DateTime, string> tokenEpiryAndToken))
            { 
                TimeSpan timeToRefresh = tokenEpiryAndToken.Item1.Subtract(TimeSpan.FromMinutes(15)).Subtract(DateTime.UtcNow);
                if (timeToRefresh.TotalMilliseconds > 0)
                {
                    return Task.FromResult<string>(tokenEpiryAndToken.Item2);
                }
            }

            string jwt = CreateJwt();
            return GetAccessTokenAsync(jwt);
        }

        protected override async Task<string> FindInstallationId()
        {
            if (OrgNameToInstallationIdMap.TryGetValue(this.organization, out string cachedInstallationId))
            {
                return cachedInstallationId;
            }
            else
            {
                List<JObject> installations = await this.GetAppInstallations().ConfigureAwait(false);          
                string targetInstallationId = null;

                foreach (JObject installation in installations)
                {
                    string orgName = installation.SelectToken("$.account.login").Value<string>();
                    string installationId = installation.SelectToken("$.id").Value<string>();
                    OrgNameToInstallationIdMap[orgName] = installationId;

                    if (orgName.Equals(this.organization))
                    {
                        targetInstallationId = installationId;
                    }
                }

                return targetInstallationId ?? throw new FatalException($"Could not find installation id for organization {organization} with AppId {this.appId}");
            }
        }

        public async Task<List<JObject>> GetAppInstallations()
        {
            string jwt = CreateJwt();
            string requestUri = $"https://{this.apiDomain}/app/installations?per_page=100";
            BatchingGitHubHttpRequest batchingHttpClient = new BatchingGitHubHttpRequest(this.httpClient, requestUri, "App.Installations", new List<HttpResponseSignature>());
            IAuthentication jwtAuthentication = new JwtAuthentication(this.Identity + "-jwt", jwt);
            List<JObject> installations = new List<JObject>();
            while (batchingHttpClient.HasNext)
            {
                RequestResult result = await batchingHttpClient.NextResponseAsync(jwtAuthentication).ConfigureAwait(false);
                HttpResponseMessage response = result.response;
                JArray responseBody = await HttpUtility.ParseAsJArrayAsync(response).ConfigureAwait(false);
                foreach (JObject installation in responseBody)
                {
                    installations.Add(installation);
                }
            }

            return installations;
        }

        protected override async Task<string> ObtainPat(string jwt, string installationId)
        {
            string requestUri = $"https://{this.apiDomain}/app/installations/{installationId}/access_tokens";
            IAuthentication jwtAuthentication = new JwtAuthentication(this.Identity + "-jwt", jwt);
            JObject responseBody = await this.httpClient.PostAndParseAsJObjectAsync(requestUri, string.Empty, jwtAuthentication, "App.Installations", new List<HttpResponseSignature>()).ConfigureAwait(false);
            LogTokenGenerationEvent();

            string token = responseBody.SelectToken("$.token").Value<string>();
            DateTime expiresAt = DateTime.Parse(responseBody.SelectToken("$.expires_at").Value<string>());

            TokenCache[this.organization] = new Tuple<DateTime, string>(expiresAt, token);
            return token;
        }

        private void LogTokenGenerationEvent()
        {
            string callerIdentity = this.Identity.Substring(0, 4); // For security reasons, include only the first 4 characters.
            TargetResource[] targetResources = new TargetResource[]
            {
                new TargetResource("Organization", this.organization),
            };
            CallerIdentity[] callerIdentities = new CallerIdentity[]
            {
                new CallerIdentity(CallerIdentityType.ApplicationID, callerIdentity),
            };
            this.auditLogger.LogTokenGenerationAuditEvent(telemetryClient, OperationResult.Success, targetResources, callerIdentities, TokenType);
        }

        /// <summary>
        /// This method uses Azure Keyvault to manually create a JWT based on the JWT spec.
        /// https://tools.ietf.org/html/rfc7519
        /// </summary>
        /// <returns>A string with the JWT required to authenticate with GitHub.</returns>
        private string CreateJwt()
        {
            return CreateJwtBase(this.useInteractiveLogin ? null : new ManagedIdentityCredential());
        }

        private class JwtAuthentication : IAuthentication
        {
            public Dictionary<string, string> AdditionalWebRequestHeaders => new Dictionary<string, string>()
            {
                {  "Accept", "application/vnd.github.machine-man-preview+json"}
            };

            public string Identity { get; }

            public string Schema => "Bearer";

            private string pat;

            public JwtAuthentication(string identity, string pat)
            {
                this.Identity = identity;
                this.pat = pat;
            }

            public Task<string> GetAuthorizationHeaderAsync()
            {
                return Task.FromResult(this.pat);
            }
        }
    }
}
