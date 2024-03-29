﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Telemetry;
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
