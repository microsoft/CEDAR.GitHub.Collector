// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Telemetry;
using System.Collections.Generic;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class PullRequestPayloadHasher : RepositoryPayloadHasher
    {
        public PullRequestPayloadHasher(ITelemetryClient telemetryClient)
            : base(telemetryClient)
        {
        }

        protected override List<string> GetExcludedAttributePaths() => new List<string>(base.GetExcludedAttributePaths())
        {
            "$.pull_request.rebaseable",
        };

        protected override List<string> GetOptionallyExcludedAttributes() => new List<string>(base.GetOptionallyExcludedAttributes())
        {
            // There are example event and payloads where pull_request.head or pull_request.base are null. Making these optional.
            "$.pull_request.head.repo.pushed_at",
            "$.pull_request.base.repo.pushed_at",
        };
    }
}
