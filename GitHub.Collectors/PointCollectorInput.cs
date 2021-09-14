using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.GitHub.Collectors.Model;
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
        public Repository Repository { get; set; }
        public bool IgnoreCache { get; set; } = false;
        public string[] IgnoreCacheForApis { get; set; } = new string[0];
        // Optional fields to overwrite default authentication
        public string IdentityEnvironmentVariable { get; set; } = "Identity";
        public string PersonalAccessTokenEnvironmentVariable { get; set; } = "PersonalAccessToken";
        public string ResponseType { get; set; } = "Array";
        public List<HttpResponseSignature> AllowListedResponses { get; set; } = new List<HttpResponseSignature>();
    }
}
