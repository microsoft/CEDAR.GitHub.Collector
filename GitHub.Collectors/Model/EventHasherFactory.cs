// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Telemetry;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class EventHasherFactory
    {
        public static EventHasherFactory Instance { get; } = new EventHasherFactory();

        public IHasher GetEventHasher(string eventType, ITelemetryClient telemetryClient)
        {
            return eventType switch
            {
                "IssueCommentEvent" => new IssueCommentEventHasher(telemetryClient),
                "PushEvent" => new PushEventHasher(),
                "PullRequestEvent" => new PullRequestEventHasher(telemetryClient),
                "ForkEvent" => new ForkEventHasher(telemetryClient),
                _ => new EventHasher(),
            };
        }
    }
}
