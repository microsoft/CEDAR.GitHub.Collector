// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    // Important note: changing anything (ordering, naming, etc.) in this class that would affect its JSON serialization can temporarily break Webhook payload caching for push events.
    [Serializable]
    public class Push
    {
        public Repository Repository { get; set; }
        public string BeforeCommitSha { get; set; }
        public string AfterCommitSha { get; set; }

        public Push()
        {
        }

        public Push(Repository repository, string beforeCommitSha, string afterCommitSha)
        {
            this.Repository = repository;
            this.BeforeCommitSha = beforeCommitSha;
            this.AfterCommitSha = afterCommitSha;
        }
    }
}
