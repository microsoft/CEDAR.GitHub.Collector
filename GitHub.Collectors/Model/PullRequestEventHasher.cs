// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Auditing;
using System.Collections.Generic;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class PullRequestEventHasher : PayloadHasher
    {
        public PullRequestEventHasher(ITelemetryClient telemetryClient)
            : base(telemetryClient)
        {
        }

        protected override List<string> GetExcludedAttributePaths() => new List<string>()
        {
            "$.pull_request.rebaseable",
        };

        protected override List<string> GetOptionallyExcludedAttributes() => new List<string>()
        {
            // There are example event and payloads where pull_request.head or pull_request.base are null. Making these optional.
            "$.pull_request.head.repo.pushed_at",
            "$.pull_request.base.repo.pushed_at",
        };
    }
}
