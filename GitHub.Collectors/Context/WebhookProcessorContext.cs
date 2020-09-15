// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Context;
using System;

namespace Microsoft.CloudMine.GitHub.Collectors.Context
{
    [Serializable]
    public class WebhookProcessorContext : FunctionContext
    {
        public string LogicAppStartDate { get; set; }

        public string LogicAppRunId { get; set; }

        public string EventType { get; set; }
    }
}
