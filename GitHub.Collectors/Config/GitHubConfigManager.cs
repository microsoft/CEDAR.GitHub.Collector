// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Auditing;
using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Error;
using Microsoft.CloudMine.Core.Telemetry;
using Microsoft.CloudMine.GitHub.Collectors.Authentication;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Newtonsoft.Json.Linq;
using System;

namespace Microsoft.CloudMine.Core.Collectors.Config
{
    public class GitHubConfigManager : ConfigManager
    {
        private readonly JToken apiDomainToken;

        public GitHubConfigManager(string jsonString, IConfigValueResolver configResolver = null)
            : base(jsonString, configResolver)
        {
            this.apiDomainToken = base.config.SelectToken("ApiDomain");
        }

        public IAuthentication GetAuthentication(CollectorType collectorType, GitHubHttpClient httpClient, string organization, string apiDomain, ITelemetryClient telemetryClient, IAuditLogger auditLogger)
        {
            IAuthentication baseAuth = base.GetAuthentication(collectorType.ToString());
            if (baseAuth != null)
            {
                return baseAuth;
            }

            JToken authenticationToken = this.GetAuthenticationToken(collectorType.ToString());
            AuthenticationType authenticationType = Enum.Parse<AuthenticationType>(authenticationToken.SelectToken("Type").Value<string>());
            switch (authenticationType)
            {
                case AuthenticationType.GitHubApp:
                    JToken appIdToken = authenticationToken.SelectToken("AppId");
                    if (appIdToken == null)
                    {
                        throw new FatalTerminalException($"For '{collectorType}' collector, Settings.json must provide an AppId to use GitHubApp Authentication");
                    }

                    int appId = appIdToken.Value<int>();
                    JToken gitHubAppKeyUriToken = authenticationToken.SelectToken("GitHubAppKeyUri");
                    if (gitHubAppKeyUriToken == null)
                    {
                        throw new FatalTerminalException($"For '{collectorType}' collector, Settings.json must provide a GitHubAppKeyUri to use GutHubApp Authentication");
                    }

                    string gitHubAppKeyUri = gitHubAppKeyUriToken.Value<string>();
                    JToken useInteractiveLoginToken = authenticationToken.SelectToken("UseInteractiveLogin");
                    bool useInteractiveLogin = useInteractiveLoginToken != null && useInteractiveLoginToken.Value<bool>();
                    GitHubAppAuthentication auth = new GitHubAppAuthentication(appId, httpClient, organization, apiDomain, gitHubAppKeyUri, useInteractiveLogin, auditLogger, telemetryClient);
                    return auth;

                default:
                    throw new FatalTerminalException($"For '{collectorType}' collector, Unsupported Authentication Type : {authenticationType}");
            }
        }

        public bool UsesGitHubAuth(string collectorType)
        {
            JToken authenticationToken = this.GetAuthenticationToken(collectorType);
            JToken authenticationTypeToken = authenticationToken.SelectToken("Type");
            return authenticationTypeToken != null && authenticationTypeToken.Value<string>().Equals("GitHubApp");
        }

        public string GetApiDomain()
        {
            ValidateSettingsExist();
            string apiDomain = string.Empty;
            try
            {
                apiDomain = this.apiDomainToken.Value<string>();
            }
            catch (Exception)
            {
                throw new FatalTerminalException($"Invalid URI: The hostname could not be parsed for API domain {apiDomainToken}. The API domain must be provided in Settings.json.");
            }
            return apiDomain;
        }
    }
}

