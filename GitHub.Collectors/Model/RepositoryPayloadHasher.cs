// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.CloudMine.Core.Collectors.Telemetry;
using System.Collections.Generic;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class RepositoryPayloadHasher : OrganizationPayloadHasher
    {
        public RepositoryPayloadHasher(ITelemetryClient telemetryClient)
            : base(telemetryClient)
        {
        }

        /*
         * The following properties of the GitHub WebHooks event do not exist when we retrieve the event through the Events Timeline API at repository level:
           - repository
           - organization
           - enterprise
           - sender
        */
        protected override List<string> GetExcludedAttributePaths() => new List<string>(base.GetExcludedAttributePaths())
        {
            "$.repository",
        };
    }
}
