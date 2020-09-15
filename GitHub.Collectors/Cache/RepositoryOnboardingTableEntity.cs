// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.GitHub.Collectors.Model;
using System;

namespace Microsoft.CloudMine.GitHub.Collectors.Cache
{
    public class OnboardingTableEntity : RepositoryTableEntity
    {
        public string ApiName { get; set; }
        public string BlobPath { get; set; }
        public DateTime OnboardedOn { get; set; }

        // Used for de-serialization.
        public OnboardingTableEntity()
        {
        }

        // Used for retrieving elements from cache.
        public OnboardingTableEntity(Repository repository, string apiName)
            : base(repository)
        {
            this.ApiName = apiName;

            this.RowKey = this.ApiName;

            this.AddContext("ApiName", this.ApiName);
        }

        // Used for inserting elements in the cache.
        public OnboardingTableEntity(Repository repository, string apiName, string blobPath, DateTime onboardedOn)
            : this(repository, apiName)
        {
            this.OnboardedOn = onboardedOn;
            this.BlobPath = blobPath;

            this.AddContext("BlobPath", this.BlobPath);
            this.AddContext("OnboardedOn", $"{this.OnboardedOn:O}");
        }
    }
}
