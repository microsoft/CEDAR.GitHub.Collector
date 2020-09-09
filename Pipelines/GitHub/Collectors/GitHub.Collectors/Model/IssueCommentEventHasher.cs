// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.CloudMine.Core.Collectors.Telemetry;
using System.Collections.Generic;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class IssueCommentEventHasher : PayloadHasher
    {
        public IssueCommentEventHasher(ITelemetryClient telemetryClient)
            : base(telemetryClient)
        {
        }

        protected override List<string> GetExcludedAttributePaths() => new List<string>()
        {
            "$.issue.comments",
            "$.issue.updated_at",
            "$.issue.state",
            "$.issue.closed_at",
        };

        protected override List<string> GetOptionallyExcludedAttributes() => new List<string>();
    }
}
