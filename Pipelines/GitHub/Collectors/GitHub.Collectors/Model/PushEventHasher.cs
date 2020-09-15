// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class PushEventHasher : IHasher
    {
        public string ComputeSha256Hash(JObject payload, Repository repository)
        {
            string beforeCommitSha = payload.SelectToken("$.before").Value<string>();
            // In events timeline response payload, after commit sha is provided as "head" property.
            string afterCommitSha = payload.SelectToken("$.head").Value<string>();

            Push push = new Push(repository, beforeCommitSha, afterCommitSha);
            string serializedPush = JsonConvert.SerializeObject(push, Formatting.None);
            return HashUtility.ComputeSha256(serializedPush);
        }
    }
}
