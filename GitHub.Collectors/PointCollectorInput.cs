using Microsoft.CloudMine.Core.Collectors.Error;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Microsoft.CloudMine.GitHub.Collectors
{
    [Serializable]
    public class PointCollectorInput
    {
        public string Url { get; set; }
        public string RecordType { get; set; }
        public string ApiName { get; set; }
        public Dictionary<string, JToken> Context { get; set; }
        public bool IgnoreCache { get; set; } = false;
        public string[] IgnoreCacheForApis { get; set; } = new string[0];
        // Optional fields to overwrite default authentication
        public string IdentityEnvironmentVariable { get; set; } = "Identity";
        public string PersonalAccessTokenEnvironmentVariable { get; set; } = "PersonalAccessToken";
        public string ResponseType { get; set; } = "Array";

        public Repository GetRepository()
        {
            string organizationLogin = this.Context.TryGetValue("OrganizationLogin", out JToken organizationLoginToken) ? organizationLoginToken.Value<string>() : null;
            long organizationId = this.Context.TryGetValue("OrganizationId", out JToken organizationIdToken) ? organizationIdToken.Value<long>() : -1;
            string repositoryName = this.Context.TryGetValue("RepositoryName", out JToken repositoryNameToken) ? repositoryNameToken.Value<string>() : null;
            long repositoryId = this.Context.TryGetValue("RepositoryId", out JToken repositoryIdToken) ? repositoryIdToken.Value<long>() : 0;

            if (organizationLogin == null)
            {
                throw new FatalTerminalException("Invalid request: point collector request must contain the following JSON attribute: $.Request.Context.OrganizationLogin");
            }

            if (organizationId == -1)
            {
                throw new FatalTerminalException("Invalid request: point collector request must contain the following JSON attribute: $.Request.Context.OrganizationId");
            }

            return new Repository(organizationId, repositoryId, organizationLogin, repositoryName);
        }
    }
}
