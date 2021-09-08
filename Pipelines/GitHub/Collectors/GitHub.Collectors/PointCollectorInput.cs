using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CloudMine.GitHub.Collectors
{
    [Serializable]
    public class PointCollectorInput
    {
        public long OrganizationId { get; set; } = 0;
        public string OrganizationLogin { get; set; } = string.Empty;
        public long RepositoryId { get; set; } = 0;
        public string RepositoryName { get; set; } = string.Empty;
        public bool IgnoreCache { get; set; } = false;
        public string Url { get; set; }
        public string[] IgnoreCacheForApis { get; set; } = new string[0];

        // Optional fields to overwrite default authentication
        public string IdentityEnvironmentVariable { get; set; } = "Identity";
        public string PersonalAccessTokenEnvironmentVariable { get; set; } = "PersonalAccessToken";
    }
}
