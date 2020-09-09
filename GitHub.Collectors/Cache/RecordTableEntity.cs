// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.CloudMine.GitHub.Collectors.Model;

namespace Microsoft.CloudMine.GitHub.Collectors.Cache
{
    public class RecordTableEntity : RepositoryTableEntity
    {
        public string RecordType { get; set; }
        public string RecordSha { get; set; }
        public string SessionId { get; set; }

        public RecordTableEntity()
        {
        }

        // Used for retrieving elements from cache.
        public RecordTableEntity(Repository repository, string recordSha)
            : this(repository, recordType: "Does not matter", recordSha, sessionId: "Does not matter")
        {
        }

        public RecordTableEntity(Repository repository, string recordType, string recordSha, string sessionId)
            : base(repository)
        {
            this.RowKey = recordSha;

            this.RecordType = recordType;
            this.RecordSha = recordSha;
            this.SessionId = sessionId;
        }
    }
}
