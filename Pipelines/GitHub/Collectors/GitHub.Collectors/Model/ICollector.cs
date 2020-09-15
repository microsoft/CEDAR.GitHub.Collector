// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.GitHub.Collectors.Processor;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public interface ICollector
    {
        Task ProcessWebhookPayloadAsync(JObject jsonObject, Repository repository);
    }
}
