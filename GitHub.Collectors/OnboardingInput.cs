// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.GitHub.Collectors.Model;
using System;

namespace Microsoft.CloudMine.GitHub.Collectors
{
    [Serializable]
    public class OnboardingInput
    {
        public long OrganizationId { get; set; } = 0;
        public string OrganizationLogin { get; set; } = string.Empty;
        public long RepositoryId { get; set; } = 0;
        public string RepositoryName { get; set; } = string.Empty;
        public bool IgnoreCache { get; set; } = false;
        public OnboardingType OnboardingType { get; set; } = OnboardingType.Organization;
        public string[] IgnoreCacheForApis { get; set; } = new string[0];

        // Optional fields to overwrite default authentication
        public string IdentityEnvironmentVariable { get; set; } = "Identity";
        public string PersonalAccessTokenEnvironmentVariable { get; set; } = "PersonalAccessToken";

        public OnboardingInput()
        {
        }

        public Repository ToRepository()
        {
            return new Repository(this.OrganizationId, this.RepositoryId, this.OrganizationLogin, this.RepositoryName);
        }
    }

    public enum OnboardingType
    {
        Organization,
        Repository,
    }
}
