// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.CloudMine.Core.Collectors.Telemetry;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public class PayloadHasherFactory
    {
        public static PayloadHasherFactory Instance { get; } = new PayloadHasherFactory();

        public IHasher GetEventHasher(string eventType, ITelemetryClient telemetryClient)
        {
            switch (eventType)
            {
                case "commit_comment":
                    return new CommitCommentPayloadHasher(telemetryClient);
                case "fork":
                    return new ForkPayloadHasher(telemetryClient);
                case "membership":
                case "organization":
                case "project":
                case "project_card":
                case "project_column":
                case "team":
                    return new OrganizationPayloadHasher(telemetryClient);
                case "issue":
                    return new IssuePayloadHasher(telemetryClient);
                case "issue_comment":
                    return new IssueCommentPayloadHasher(telemetryClient);
                case "pull_request":
                    return new PullRequestPayloadHasher(telemetryClient);
                case "push":
                    return new PushPayloadHasher();
                default:
                    return new RepositoryPayloadHasher(telemetryClient);
            }
        }
    }
}
