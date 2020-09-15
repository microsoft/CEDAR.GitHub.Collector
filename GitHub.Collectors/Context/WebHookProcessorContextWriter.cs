// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Context;
using Newtonsoft.Json.Linq;

namespace Microsoft.CloudMine.GitHub.Collectors.Context
{
    public class WebhookProcessorContextWriter : ContextWriter<WebhookProcessorContext>
    {
        public override void AugmentMetadata(JObject metadata, WebhookProcessorContext functionContext)
        {
            string logicAppStartDate = functionContext.LogicAppStartDate;
            if (!string.IsNullOrEmpty(logicAppStartDate))
            {
                metadata.Add("LogicAppStartDate", logicAppStartDate);
            }

            string logicAppRunId = functionContext.LogicAppRunId;
            if (!string.IsNullOrEmpty(logicAppRunId))
            {
                metadata.Add("LogicAppRunId", logicAppRunId);
            }
        }
    }
}
