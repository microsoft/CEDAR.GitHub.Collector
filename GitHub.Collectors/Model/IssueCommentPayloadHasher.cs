// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Telemetry;
using System.Collections.Generic;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class IssueCommentPayloadHasher : RepositoryPayloadHasher
    {
        public IssueCommentPayloadHasher(ITelemetryClient telemetryClient)
            : base(telemetryClient)
        {
        }

        protected override List<string> GetExcludedAttributePaths() => new List<string>(base.GetExcludedAttributePaths())
        {
            "$.issue.comments",
            "$.issue.updated_at",
            "$.issue.state",
            "$.issue.closed_at",
        };
    }
}
