// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.CloudMine.Core.Collectors.Telemetry;
using System.Collections.Generic;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class CommitCommentPayloadHasher : RepositoryPayloadHasher
    {
        public CommitCommentPayloadHasher(ITelemetryClient telemetryClient)
            : base(telemetryClient)
        {
        }

        protected override List<string> GetExcludedAttributePaths() => new List<string>(base.GetExcludedAttributePaths())
        {
            "$.action",
        };
    }
}
