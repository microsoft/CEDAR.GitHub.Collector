// Copyright (c) Microsoft Corporation. All rights reserved.

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
