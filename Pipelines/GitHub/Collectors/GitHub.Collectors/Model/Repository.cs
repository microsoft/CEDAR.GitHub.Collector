// Copyright (c) Microsoft Corporation. All rights reserved.

using System;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    [Serializable]
    public class Repository
    {
        public const long NoRepositoryId = 0;
        public static readonly string NoRepositoryName = string.Empty;

        public long OrganizationId { get; set; }
        public long RepositoryId { get; set; }
        public string OrganizationLogin { get; set; }
        public string RepositoryName { get; set; }

        public Repository()
        {
        }

        public Repository(long organizationId, long repositoryId, string organizationLogin, string repositoryName)
        {
            this.OrganizationId = organizationId;
            this.RepositoryId = repositoryId;
            this.OrganizationLogin = organizationLogin;
            this.RepositoryName = repositoryName;
        }

        public bool IsValid()
        {
            return !(this.RepositoryName.Equals(NoRepositoryName) && this.RepositoryId == NoRepositoryId);
        }
    }
}
