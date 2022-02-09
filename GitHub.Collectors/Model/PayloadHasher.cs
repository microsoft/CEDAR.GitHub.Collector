// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Auditing;
using Microsoft.CloudMine.Core.Collectors.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public abstract class PayloadHasher : IHasher
    {
        private readonly ITelemetryClient telemetryClient;

        protected PayloadHasher(ITelemetryClient telemetryClient)
        {
            this.telemetryClient = telemetryClient;
        }

        protected abstract List<string> GetExcludedAttributePaths();

        protected abstract List<string> GetOptionallyExcludedAttributes();

        public string ComputeSha256Hash(JObject record, Repository repository)
        {
            // Before hashing and caching the event, remove these properties such that the hashes are comparable.
            JObject recordClone = (JObject)record.DeepClone();
            foreach (string excludedAttributePath in this.GetExcludedAttributePaths())
            {
                // Cannot use JObject.Remove() since it does not work with Json path. 
                // Also, once we retrieve the token, we need to remove its parent since the token just contains the value.
                JToken token = recordClone.SelectToken(excludedAttributePath);
                if (token == null)
                {
                    // While we are learning more about the GitHub payload and event data shape, log this unexpected JSON shape in telemetry, rather than failing the code.
                    // For most cases, this is legit and the excludedAttribute should have been an optional one to fix it.
                    Dictionary<string, string> properties = new Dictionary<string, string>()
                    {
                        { "AttributePath", excludedAttributePath },
                        { "Required", "true" },
                        { "Exists", "false" },
                    };
                    this.telemetryClient.TrackEvent("ExcludedAttributeDetails", properties);
                }
                else
                {
                    token.Parent.Remove();
                }
            }

            foreach (string excludedAttributePath in this.GetOptionallyExcludedAttributes())
            {
                JToken token = recordClone.SelectToken(excludedAttributePath);

                // We would like to ensure that an optinal attribute path is indeed optional (i.e., there are cases it exists and there are cases it does not).
                // Therefore, log all instances so that we can evaluate in telemetry and make adjustments.
                Dictionary<string, string> properties = new Dictionary<string, string>()
                {
                    { "AttributePath", excludedAttributePath },
                    { "Required", "false" },
                    { "Exists", (token != null).ToString() },
                };
                this.telemetryClient.TrackEvent("ExcludedAttributeDetails", properties);

                if (token != null)
                {
                    token.Parent.Remove();
                }
            }

            string serializedRecordClone = recordClone.ToString(Formatting.None);
            return HashUtility.ComputeSha256(serializedRecordClone);
        }
    }
}
