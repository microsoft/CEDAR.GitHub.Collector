// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.GitHub.Collectors.Model;

namespace Microsoft.CloudMine.GitHub.Collectors.Cache
{
    public class RepositoryItemTableEntity : RepositoryTableEntity
    {
        public string RecordType { get; set; }
        public string RecordValue { get; set; }
        public string CollectorIdentifier { get; set; }

        public RepositoryItemTableEntity()
        {
        }

        // Used for retrieving values from cache.
        public RepositoryItemTableEntity(Repository repository, string recordType, string recordValue)
            : base(repository)
        {
            this.RowKey = $"{recordType}_{recordValue}";

            this.RecordType = recordType;
            this.RecordValue = recordValue;

            this.AddContext("RecordType", this.RecordType);
            this.AddContext("RecordValue", this.RecordValue);
        }

        public RepositoryItemTableEntity(Repository repository, string recordType, string recordValue, string collectorIdentifier)
            : this(repository, recordType, recordValue)
        {
            this.CollectorIdentifier = collectorIdentifier;

            this.AddContext("CollectorIdentifier", this.CollectorIdentifier);
        }
    }
}
