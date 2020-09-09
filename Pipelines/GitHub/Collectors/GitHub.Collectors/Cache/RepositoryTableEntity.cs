// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Model;

namespace Microsoft.CloudMine.GitHub.Collectors.Cache
{
    public abstract class RepositoryTableEntity : TableEntityWithContext
    {
        public long OrganizationId { get; set; }
        public long RepositoryId { get; set; }
        public string OrganizationLogin { get; set; }
        public string RepositoryName { get; set; }

        public RepositoryTableEntity()
        {
        }

        public RepositoryTableEntity(Repository repository)
        {
            this.PartitionKey = $"{repository.OrganizationId}_{repository.RepositoryId}";
            this.RowKey = string.Empty;

            this.OrganizationId = repository.OrganizationId;
            this.RepositoryId = repository.RepositoryId;
            this.OrganizationLogin = repository.OrganizationLogin;
            this.RepositoryName = repository.RepositoryName;

            this.AddContext("RepositorId", this.RepositoryId.ToString());
            this.AddContext("RepositoryName", this.RepositoryName);
            this.AddContext("OrganizationId", this.OrganizationId.ToString());
            this.AddContext("OrganizationLogin", this.OrganizationLogin);
        }
    }
}
