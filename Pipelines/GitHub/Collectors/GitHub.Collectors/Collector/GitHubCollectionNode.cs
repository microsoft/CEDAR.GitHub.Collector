// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Collector;
using Microsoft.CloudMine.Core.Collectors.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Microsoft.CloudMine.GitHub.Collectors.Collector
{
    public class GitHubCollectionNode : CollectionNode
    {
        public override Type ResponseType { get; set; } = typeof(JArray);

        public override object Clone()
        {
            return new GitHubCollectionNode()
            {
                AdditionalMetadata = new Dictionary<string, JToken>(this.AdditionalMetadata),
                GetInitialUrl = this.GetInitialUrl,
                RequestBody = this.RequestBody,
                RecordType = this.RecordType,
                ApiName = this.ApiName,
                Output = this.Output,
                ResponseType = this.ResponseType,
                ProduceChildrenAsync = this.ProduceChildrenAsync,
                ProduceAdditionalMetadata = this.ProduceAdditionalMetadata,
                ProcessRecordAsync = this.ProcessRecordAsync,
                WhitelistedResponses = new List<HttpResponseSignature>(this.WhitelistedResponses),
                HaltCollection = this.HaltCollection,
                ProduceChildrenFromResponseAsync = this.ProduceChildrenFromResponseAsync,
                PrepareRecordForOutput = this.PrepareRecordForOutput,
            };
        }
    }
}
