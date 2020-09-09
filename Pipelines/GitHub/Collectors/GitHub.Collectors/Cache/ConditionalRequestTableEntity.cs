// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Utility;

namespace Microsoft.CloudMine.GitHub.Collectors.Cache
{
    public class ConditionalRequestTableEntity : TableEntityWithContext
    {
        public string Url { get; set; }
        public string RecordType { get; set; }
        public string GitHubETag { get; set; }

        // Used for de-serialization
        public ConditionalRequestTableEntity()
        {
        }

        // Used for retrieving values.
        public ConditionalRequestTableEntity(string url)
        {
            string urlHash = HashUtility.ComputeSha256(url);
            this.PartitionKey = urlHash;
            this.RowKey = string.Empty;

            this.Url = url;

            this.AddContext("Url", this.Url);
        }

        // Used for caching new values.
        public ConditionalRequestTableEntity(string url, string recordType, string gitHubETag)
            : this(url)
        {
            this.RecordType = recordType;
            this.GitHubETag = gitHubETag;

            this.AddContext("RecordType", this.RecordType);
            this.AddContext("GitHubETag", this.GitHubETag);
        }
    }
}
