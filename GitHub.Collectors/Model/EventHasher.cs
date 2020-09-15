// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class EventHasher : IHasher
    {
        public string ComputeSha256Hash(JObject payload, Repository repository)
        {
            string serializedPayloadRecord = payload.ToString(Formatting.None);
            return HashUtility.ComputeSha256(serializedPayloadRecord);
        }
    }
}
