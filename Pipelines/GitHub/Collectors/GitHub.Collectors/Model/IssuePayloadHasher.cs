// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.CloudMine.Core.Collectors.Telemetry;
using System.Collections.Generic;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class IssuePayloadHasher : RepositoryPayloadHasher
    {
        public IssuePayloadHasher(ITelemetryClient telemetryClient)
            : base(telemetryClient)
        {
        }

        protected override List<string> GetOptionallyExcludedAttributes() => new List<string>(base.GetOptionallyExcludedAttributes())
        {
            "$.changes",
        };
    }
}
