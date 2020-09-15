// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.CloudMine.GitHub.Collectors.Context
{
    [Serializable]
    public class OrchestrationContext : WebhookProcessorContext
    {
        public string RequestBody { get; set; }

        public WebhookProcessorContext Downgrade()
        {
            OrchestrationContext result = (OrchestrationContext)this.MemberwiseClone();
            result.RequestBody = null;
            return result;
        }
    }
}
