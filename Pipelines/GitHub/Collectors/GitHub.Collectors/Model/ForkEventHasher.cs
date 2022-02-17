// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Telemetry;
using System.Collections.Generic;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class ForkEventHasher : PayloadHasher
    {
        public ForkEventHasher(ITelemetryClient telemetryClient)
            : base(telemetryClient)
        {
        }

        protected override List<string> GetExcludedAttributePaths() => new List<string>()
        {
            "$.forkee.license",
        };

        protected override List<string> GetOptionallyExcludedAttributes() => new List<string>();
    }
}
