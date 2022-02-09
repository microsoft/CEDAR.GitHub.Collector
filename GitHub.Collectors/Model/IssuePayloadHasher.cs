// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Auditing;
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
