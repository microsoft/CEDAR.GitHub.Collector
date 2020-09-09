// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Error;
using Microsoft.CloudMine.GitHub.Collectors.Authentication;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Newtonsoft.Json.Linq;
using System;

namespace Microsoft.CloudMine.Core.Collectors.Config
{
    public class GitHubConfigManager : ConfigManager
    {
        public GitHubConfigManager(string jsonString, IConfigValueResolver configResolver = null)
            : base(jsonString, configResolver)
        {
        }

        public IAuthentication GetAuthentication(CollectorType collectorType, GitHubHttpClient httpClient, string organization, string apiDomain)
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
                    GitHubAppAuthentication auth = new GitHubAppAuthentication(appId, httpClient, organization, apiDomain, gitHubAppKeyUri, useInteractiveLogin);
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
    }
}

