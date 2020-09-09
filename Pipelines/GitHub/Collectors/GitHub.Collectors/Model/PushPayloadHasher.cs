// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.CloudMine.Core.Collectors.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class PushPayloadHasher : IHasher
    {
        public string ComputeSha256Hash(JObject record, Repository repository)
        {
            string beforeCommitSha = record.SelectToken("$.before").Value<string>();
            string afterCommitSha = record.SelectToken("$.after").Value<string>();

            Push push = new Push(repository, beforeCommitSha, afterCommitSha);
            string serializedPush = JsonConvert.SerializeObject(push, Formatting.None);
            return HashUtility.ComputeSha256(serializedPush);
        }
    }
}
