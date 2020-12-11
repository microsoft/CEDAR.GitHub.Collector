// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.CloudMine.Core.Collectors.Error;
using Azure.Core;
using Azure.Identity;
using System.Text;
using System.Security.Cryptography;
using System.Net.Http;
using Microsoft.CloudMine.Core.Collectors.Web;
using Newtonsoft.Json.Linq;
using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using System.Collections.Concurrent;

namespace Microsoft.CloudMine.GitHub.Collectors.Authentication
{
    public class GitHubAppAuthentication : IAuthentication
    {
        /// <summary>
        /// How long a JWT claim remains valid, in seconds.
        /// </summary>
        private const int JwtExpiry = 60 * 8; // 8 mins

        private string organization;
        private GitHubHttpClient httpClient;
        private string apiDomain;
        private readonly int appId;
        private readonly string gitHubAppKeyVaultUri;
        private readonly bool useInteractiveLogin;


        /// <summary>
        /// Maps an organization name to a Tuple containing an expiry date and the token itself.
        /// </summary>
        private static ConcurrentDictionary<string, Tuple<DateTime, string>> TokenCache = new ConcurrentDictionary<string, Tuple<DateTime, string>>();
        private static ConcurrentDictionary<string, string> OrgNameToInstallationIdMap = new ConcurrentDictionary<string, string>();

        public GitHubAppAuthentication(int appId, GitHubHttpClient httpClient, string organization, string apiDomain, string gitHubAppKeyVaultUri, bool useInteractiveLogin)
        {
            this.appId = appId;
            this.gitHubAppKeyVaultUri = gitHubAppKeyVaultUri;
            this.useInteractiveLogin = useInteractiveLogin;
            this.httpClient = httpClient;
            this.organization = organization;
            this.apiDomain = apiDomain;
        }

        public Dictionary<string, string> AdditionalWebRequestHeaders => new Dictionary<string, string>();

        public string Identity { get => this.appId + "-" + this.organization; }

        public string Schema => "Bearer";

        public async Task<string> GetAuthorizationHeaderAsync()
        {
            if (this.httpClient == null || this.organization == null || this.apiDomain == null)
            {
                throw new FatalTerminalException("dependencies must be set before generating authorization header");
            }

            if (TokenCache.ContainsKey(this.organization))
            {
                TimeSpan timeToRefresh = TokenCache[this.organization].Item1.Subtract(TimeSpan.FromMinutes(15)).Subtract(DateTime.UtcNow);
                if (timeToRefresh.TotalMilliseconds > 0)
                {
                    return TokenCache[this.organization].Item2;
                }
            }

            string jwt = this.CreateJwt();
            string installationId = await this.FindInstallationId(jwt).ConfigureAwait(false);
            string result = await this.ObtainPat(jwt, installationId).ConfigureAwait(false);
            return result;
        }

        private async Task<string> FindInstallationId(string jwt)
        {
            string cachedInstallationId;

            if (OrgNameToInstallationIdMap.TryGetValue(this.organization, out cachedInstallationId))
            {
                return cachedInstallationId;
            }
            else
            {
                string requestUri = $"https://{this.apiDomain}/app/installations?per_page=100";
                BatchingGitHubHttpRequest batchingHttpClient = new BatchingGitHubHttpRequest(httpClient, requestUri, "App.Installations", new List<HttpResponseSignature>());

                IAuthentication jwtAuthentication = new JwtAuthentication(this.Identity + "-jwt", jwt);
                string targetInstallationId = null;

                while (batchingHttpClient.HasNext)
                {
                    HttpResponseMessage response = await batchingHttpClient.NextResponseAsync(jwtAuthentication).ConfigureAwait(false);

                    JArray responseBody = await HttpUtility.ParseAsJArrayAsync(response).ConfigureAwait(false);
                    foreach (JObject responseItem in responseBody)
                    {
                        string orgName = responseItem.SelectToken("$.account.login").Value<string>();
                        string installationId = responseItem.SelectToken("$.id").Value<string>();
                        OrgNameToInstallationIdMap[orgName] = installationId;

                        if (orgName.Equals(this.organization))
                        {
                            targetInstallationId = installationId;
                        }
                    }
                }

                return targetInstallationId ?? throw new FatalException($"Could not find installation id for organization {organization} with AppId {this.appId}");
            }
        }

        private async Task<string> ObtainPat(string jwt, string installationId)
        {
            string requestUri = $"https://{this.apiDomain}/app/installations/{installationId}/access_tokens";
            IAuthentication jwtAuthentication = new JwtAuthentication(this.Identity + "-jwt", jwt);
            JObject responseBody = await this.httpClient.PostAndParseAsJObjectAsync(requestUri, string.Empty, jwtAuthentication, "App.Installations", new List<HttpResponseSignature>()).ConfigureAwait(false);

            string token = responseBody.SelectToken("$.token").Value<string>();
            DateTime expiresAt = DateTime.Parse(responseBody.SelectToken("$.expires_at").Value<string>());

            TokenCache[this.organization] = new Tuple<DateTime, string>(expiresAt, token);
            return token;
        }

        /// <summary>
        /// This method uses Azure Keyvault to manually create a JWT based on the JWT spec.
        /// https://tools.ietf.org/html/rfc7519
        /// </summary>
        /// <returns>A string with the JWT required to authenticate with GitHub.</returns>
        private string CreateJwt()
        {
            TokenCredential credential;

            if (this.useInteractiveLogin)
            {
                credential = new InteractiveBrowserCredential();
            }
            else
            {
                credential = new ManagedIdentityCredential();
            }

            CryptographyClient client = new CryptographyClient(new Uri(this.gitHubAppKeyVaultUri), credential);
            string jwtHeader = @"{""alg"":""RS256"",""typ"":""JWT""}";

            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            long now = (long)(DateTime.UtcNow - epoch).TotalSeconds;

            string payload = @"{""iat"":" + now + @",""exp"":" + (now + JwtExpiry) + @",""iss"":" + this.appId + @"}";

            string encodedHeader = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(jwtHeader));
            string encodedPayload = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
            string signature;

            using (SHA256 sha256 = SHA256.Create())
            {
                string jwtSigningBuffer = $"{encodedHeader}.{encodedPayload}";
                byte[] digestBuffer = sha256.ComputeHash(Encoding.UTF8.GetBytes(jwtSigningBuffer));

                SignResult signingResponse = client.Sign(SignatureAlgorithm.RS256, digestBuffer);
                string base64string = WebEncoders.Base64UrlEncode(signingResponse.Signature);
                signature = base64string;

                return $"{encodedHeader}.{encodedPayload}.{signature}";
            }
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
