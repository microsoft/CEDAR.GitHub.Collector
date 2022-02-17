// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Telemetry;
using System.Collections.Generic;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class OrganizationPayloadHasher : PayloadHasher
    {
        public OrganizationPayloadHasher(ITelemetryClient telemetryClient)
            : base(telemetryClient)
        {
        }

        /*
         * The following properties of the GitHub WebHooks event do not exist when we retrieve the event through the Events Timeline API at organization level:
           - organization
           - sender
        */
        protected override List<string> GetExcludedAttributePaths() => new List<string>
        {
            "$.organization",
            "$.sender",
        };

        /*
         * The following properties of the GitHub WebHooks event do not exist when we retrieve the event through the Events Timeline API at organization level, only for
         * organizations that are in an enterprise:
           - enterprise
        */
        protected override List<string> GetOptionallyExcludedAttributes() => new List<string>
        {
            "$.enterprise",
        };
    }
}
