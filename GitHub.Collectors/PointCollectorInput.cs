using Microsoft.CloudMine.Core.Collectors.Error;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using Newtonsoft.Json.Linq;
using System;

namespace Microsoft.CloudMine.GitHub.Collectors
{
    [Serializable]
    public class PointCollectorInput
    {
        public string Url { get; set; }
        public virtual string RecordType { get; set; }
        public virtual string ApiName { get; set; }
        public JObject Context { get; set; }
        public bool IgnoreCache { get; set; } = false;
        public string[] IgnoreCacheForApis { get; set; } = new string[0];
        // Optional fields to overwrite default authentication
        public string IdentityEnvironmentVariable { get; set; } = "Identity";
        public string PersonalAccessTokenEnvironmentVariable { get; set; } = "PersonalAccessToken";

        public string getOrganizationLogin()
        {
            JToken organizationLoginToken = this.Context.SelectToken("$.OrganizationLogin");
            if (organizationLoginToken == null)
            {
                throw new FatalTerminalException("Invalid request: point collector request must contain the following JSON attribute: $.Request.Context.OrganizationLogin");
            }
            return organizationLoginToken.Value<string>();
        }

        public Repository getRepository()
        {
            JToken organizationLoginToken = this.Context.SelectToken("$.OrganizationLogin");
            JToken organizationIdToken = this.Context.SelectToken("$.OrganizationId");
            JToken repositoryNameToken = this.Context.SelectToken("$.RepositoryId");
            JToken repositoryIdToken = this.Context.SelectToken("$.RepositoryId");

            if (organizationLoginToken == null)
            {
                throw new FatalTerminalException("Invalid request: point collector request must contain the following JSON attribute: $.Request.Context.OrganizationLogin");
            }

            if (organizationIdToken == null)
            {
                throw new FatalTerminalException("Invalid request: point collector request must contain the following JSON attribute: $.Request.Context.OrganizationId");
            }

            long organizationId = organizationIdToken.Value<long>();
            string organizationLogin = organizationLoginToken.Value<string>();
            string repositoryName = string.Empty;
            long repositoryId = 0;

            if (repositoryIdToken != null)
            {
                repositoryId = repositoryIdToken.Value<long>();
            }

            if (repositoryNameToken != null)
            {
                repositoryName = repositoryNameToken.Value<string>();
            }

            return new Repository(organizationId, repositoryId, organizationLogin, repositoryName);
        }
    }
}
