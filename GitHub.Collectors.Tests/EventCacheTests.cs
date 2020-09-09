// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.CloudMine.Core.Collectors.Tests.Telemetry;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Microsoft.CloudMine.GitHub.Collectors.Tests
{
    [TestClass]
    public class EventCacheTests
    {
        [TestMethod]
        public void CompareGenericPayload_Success()
        {
            this.CompareGenericPayload(expectSuccess: true);
        }

        [TestMethod]
        public void CompareGenericPayload_Failure()
        {
            this.CompareGenericPayload(expectSuccess: false);
        }

        private void CompareGenericPayload(bool expectSuccess)
        {
            string payloadType = "event_type";
            string eventPayload = $@"""{payloadType}"": {{ ""event_property"": ""event property value"" }}";
            string webhookPayload = $@"
{{
    {eventPayload},
    ""repository"": {{}},
    ""organization"": {{}},
    ""enterprise"": {{}},
    ""sender"": {{}}
}}";
            if (!expectSuccess)
            {
                webhookPayload = webhookPayload.Replace("event property value", "another event property value");
            }

            string eventPayloadObject = @$"{{{eventPayload}}}";

            Repository repository = new Repository(organizationId: 1, repositoryId: 1, "OrganizationLogin", "RepositoryName");
            JObject payload = JObject.Parse(eventPayloadObject);
            JObject record = JObject.Parse(webhookPayload);

            string eventType = "EventType";
            IHasher eventHasher = EventHasherFactory.Instance.GetEventHasher(eventType, new NoopTelemetryClient());
            IHasher payloadHasher = PayloadHasherFactory.Instance.GetEventHasher(payloadType, new NoopTelemetryClient());

            if (expectSuccess)
            {
                Assert.AreEqual(eventHasher.ComputeSha256Hash(payload, repository), payloadHasher.ComputeSha256Hash(record, repository));
            }
            else
            {
                Assert.AreNotEqual(eventHasher.ComputeSha256Hash(payload, repository), payloadHasher.ComputeSha256Hash(record, repository));
            }
        }

        [TestMethod]
        public void CompareIssuePayload_Success()
        {
            this.CompareIssuePayload(expectSuccess: true);
        }

        [TestMethod]
        public void CompareIssuePayload_Failure()
        {
            this.CompareIssuePayload(expectSuccess: false);
        }

        private void CompareIssuePayload(bool expectSuccess)
        {
            string payloadType = "issue";
            string eventPayloadForWebhook = $@"""{payloadType}"": {{ ""event_property"": ""event property value"" }}, ""changes"": ""Some Value""";
            string webhookPayload = $@"
{{
    {eventPayloadForWebhook},
    ""repository"": {{}},
    ""organization"": {{}},
    ""enterprise"": {{}},
    ""sender"": {{}}
}}";
            if (!expectSuccess)
            {
                webhookPayload = webhookPayload.Replace("event property value", "another event property value");
            }

            string eventPayload = $@"""{payloadType}"": {{ ""event_property"": ""event property value"" }}";
            string eventPayloadObject = @$"{{{eventPayload}}}";

            Repository repository = new Repository(organizationId: 1, repositoryId: 1, "OrganizationLogin", "RepositoryName");
            JObject payload = JObject.Parse(eventPayloadObject);
            JObject record = JObject.Parse(webhookPayload);

            string eventType = "IssueEvent";
            IHasher eventHasher = EventHasherFactory.Instance.GetEventHasher(eventType, new NoopTelemetryClient());
            IHasher payloadHasher = PayloadHasherFactory.Instance.GetEventHasher(payloadType, new NoopTelemetryClient());

            if (expectSuccess)
            {
                Assert.AreEqual(eventHasher.ComputeSha256Hash(payload, repository), payloadHasher.ComputeSha256Hash(record, repository));
            }
            else
            {
                Assert.AreNotEqual(eventHasher.ComputeSha256Hash(payload, repository), payloadHasher.ComputeSha256Hash(record, repository));
            }
        }

        [TestMethod]
        public void CompareIssueCommentPayload_Success()
        {
            this.CompareIssueCommentPayload(expectSuccess: true);
        }

        [TestMethod]
        public void CompareIssueCommentPayload_Failure()
        {
            this.CompareIssueCommentPayload(expectSuccess: false);
        }

        private void CompareIssueCommentPayload(bool expectSuccess)
        {
            string payloadType = "issue_comment";
            string eventPayloadForWebhook = $@"""{payloadType}"": {{ ""event_property"": ""event property value"" }}, ""issue"": {{ ""comments"": 1, ""updated_at"": ""Some Date"", ""state"": ""open"", ""closed_at"": null }}";
            string webhookPayload = $@"
{{
    {eventPayloadForWebhook},
    ""repository"": {{}},
    ""organization"": {{}},
    ""enterprise"": {{}},
    ""sender"": {{}}
}}";
            if (!expectSuccess)
            {
                webhookPayload = webhookPayload.Replace("event property value", "another event property value");
            }

            string eventPayload = $@"""{payloadType}"": {{ ""event_property"": ""event property value"" }}, ""issue"": {{ ""comments"": 0, ""updated_at"": ""Some Other Date"", ""state"": ""closed"", ""closed_at"": ""Some Date"" }}";
            string eventPayloadObject = @$"{{{eventPayload}}}";

            Repository repository = new Repository(organizationId: 1, repositoryId: 1, "OrganizationLogin", "RepositoryName");
            JObject payload = JObject.Parse(eventPayloadObject);
            JObject record = JObject.Parse(webhookPayload);

            string eventType = "IssueCommentEvent";
            IHasher eventHasher = EventHasherFactory.Instance.GetEventHasher(eventType, new NoopTelemetryClient());
            IHasher payloadHasher = PayloadHasherFactory.Instance.GetEventHasher(payloadType, new NoopTelemetryClient());

            if (expectSuccess)
            {
                Assert.AreEqual(eventHasher.ComputeSha256Hash(payload, repository), payloadHasher.ComputeSha256Hash(record, repository));
            }
            else
            {
                Assert.AreNotEqual(eventHasher.ComputeSha256Hash(payload, repository), payloadHasher.ComputeSha256Hash(record, repository));
            }
        }

        [TestMethod]
        public void ComparePushPayload_Success()
        {
            this.ComparePushPayload(expectSuccess: true);
        }

        [TestMethod]
        public void ComparePushPayload_Failure()
        {
            this.ComparePushPayload(expectSuccess: false);
        }

        public void ComparePushPayload(bool expectSuccess)
        {
            string beforeSha = "BeforeSha";
            string afterSha = "AfterSha";
            string webhookPayload = $@"
{{
    ""after"": ""{afterSha}"",
    ""before"": ""{beforeSha}"",
    ""other_property"": ""Other Property Value"",
    ""repository"": {{}},
    ""organization"": {{}},
    ""enterprise"": {{}},
    ""sender"": {{}}
}}";

            string eventPayloadObject = @$"
{{
    ""head"": ""{afterSha}"",
    ""before"": ""{beforeSha}""
}}";

            if (!expectSuccess)
            {
                webhookPayload = webhookPayload.Replace("BeforeSha", "Not so BeforeSha");
            }

            Repository repository = new Repository(organizationId: 1, repositoryId: 1, "OrganizationLogin", "RepositoryName");
            JObject payload = JObject.Parse(eventPayloadObject);
            JObject record = JObject.Parse(webhookPayload);

            string eventType = "PushEvent";
            IHasher eventHasher = EventHasherFactory.Instance.GetEventHasher(eventType, new NoopTelemetryClient());
            IHasher payloadHasher = PayloadHasherFactory.Instance.GetEventHasher("push", new NoopTelemetryClient());
            if (expectSuccess)
            {
                Assert.AreEqual(eventHasher.ComputeSha256Hash(payload, repository), payloadHasher.ComputeSha256Hash(record, repository));
            }
            else
            {
                Assert.AreNotEqual(eventHasher.ComputeSha256Hash(payload, repository), payloadHasher.ComputeSha256Hash(record, repository));
            }
        }

        [TestMethod]
        public void ComparePullRequestPayload_Success()
        {
            this.ComparePullRequestPayload(expectSuccess: true);
        }

        [TestMethod]
        public void ComparePullRequestPayload_Failure()
        {
            this.ComparePullRequestPayload(expectSuccess: false);
        }

        public void ComparePullRequestPayload(bool expectSuccess)
        {
            string payloadType = "pull_request";
            string payloadBaseObject = $@"""base"": {{ ""repo"": {{ ""pushed_at"": ""Some Date"", ""property"": ""property value"" }}, ""property"": ""property value"" }}";
            string payloadHeadObject = $@"""head"": {{ ""repo"": {{ ""pushed_at"": ""Some Other Date"", ""property"": ""property value"" }}, ""property"": ""property value"" }}";
            string payloadForWebhook = $@"""{payloadType}"": {{ ""event_property"": ""event property value"", ""draft"": ""true"", ""rebaseable"": false, {payloadBaseObject}, {payloadHeadObject} }}";

            string eventBaseObject = $@"""base"": {{ ""repo"": {{ ""pushed_at"": ""Some Date but earlier"", ""property"": ""property value"" }}, ""property"": ""property value"" }}";
            string eventHeadObject = $@"""head"": {{ ""repo"": {{ ""pushed_at"": ""Some Other Date but earlier"", ""property"": ""property value"" }}, ""property"": ""property value"" }}";
            string payloadForEvent = $@"""{payloadType}"": {{ ""event_property"": ""event property value"", ""draft"": ""true"", {eventBaseObject}, {eventHeadObject}, ""rebaseable"": true }}";
            string webhookPayload = $@"
{{
    {payloadForWebhook},
    ""repository"": {{}},
    ""organization"": {{}},
    ""enterprise"": {{}},
    ""sender"": {{}}
}}";

            if (!expectSuccess)
            {
                webhookPayload = webhookPayload.Replace("event property value", "another event property value");
            }

            string eventPayloadObject = @$"{{{payloadForEvent}}}";

            Repository repository = new Repository(organizationId: 1, repositoryId: 1, "OrganizationLogin", "RepositoryName");
            JObject payload = JObject.Parse(eventPayloadObject);
            JObject record = JObject.Parse(webhookPayload);

            string eventType = "PullRequestEvent";
            IHasher eventHasher = EventHasherFactory.Instance.GetEventHasher(eventType, new NoopTelemetryClient());
            IHasher payloadHasher = PayloadHasherFactory.Instance.GetEventHasher(payloadType, new NoopTelemetryClient());
            if (expectSuccess)
            {
                Assert.AreEqual(eventHasher.ComputeSha256Hash(payload, repository), payloadHasher.ComputeSha256Hash(record, repository));
            }
            else
            {
                Assert.AreNotEqual(eventHasher.ComputeSha256Hash(payload, repository), payloadHasher.ComputeSha256Hash(record, repository));
            }
        }

        [TestMethod]
        public void CompareCommitCommentPayload_Success()
        {
            this.CompareCommitCommentPayload(expectSuccess: true);
        }

        [TestMethod]
        public void CompareCommitCommentPayload_Failure()
        {
            this.CompareCommitCommentPayload(expectSuccess: false);
        }

        public void CompareCommitCommentPayload(bool expectSuccess)
        {
            string payloadType = "commit_comment";
            string payloadForWebhook = $@"""{payloadType}"": {{ ""event_property"": ""event property value"" }}, ""action"": ""created""";

            string payloadForEvent = $@"""{payloadType}"": {{ ""event_property"": ""event property value"" }}";
            string webhookPayload = $@"
{{
    {payloadForWebhook},
    ""repository"": {{}},
    ""organization"": {{}},
    ""enterprise"": {{}},
    ""sender"": {{}}
}}";

            if (!expectSuccess)
            {
                webhookPayload = webhookPayload.Replace("event property value", "another event property value");
            }

            string eventPayloadObject = @$"{{{payloadForEvent}}}";

            Repository repository = new Repository(organizationId: 1, repositoryId: 1, "OrganizationLogin", "RepositoryName");
            JObject payload = JObject.Parse(eventPayloadObject);
            JObject record = JObject.Parse(webhookPayload);

            string eventType = "CommitCommentEvent";
            IHasher eventHasher = EventHasherFactory.Instance.GetEventHasher(eventType, new NoopTelemetryClient());
            IHasher payloadHasher = PayloadHasherFactory.Instance.GetEventHasher(payloadType, new NoopTelemetryClient());
            if (expectSuccess)
            {
                Assert.AreEqual(eventHasher.ComputeSha256Hash(payload, repository), payloadHasher.ComputeSha256Hash(record, repository));
            }
            else
            {
                Assert.AreNotEqual(eventHasher.ComputeSha256Hash(payload, repository), payloadHasher.ComputeSha256Hash(record, repository));
            }
        }

        [TestMethod]
        public void CompareForkPayload_Success()
        {
            this.CompareForkPayload(expectSuccess: true);
        }

        [TestMethod]
        public void CompareForkPayload_Failure()
        {
            this.CompareForkPayload(expectSuccess: false);
        }

        public void CompareForkPayload(bool expectSuccess)
        {
            string payloadType = "forkee";
            string payloadForWebhook = $@"""{payloadType}"": {{ ""event_property"": ""event property value"", ""license"": ""Some License"" }}";

            string payloadForEvent = $@"""{payloadType}"": {{ ""event_property"": ""event property value"", ""license"": null }}";
            string webhookPayload = $@"
{{
    {payloadForWebhook},
    ""repository"": {{}},
    ""organization"": {{}},
    ""enterprise"": {{}},
    ""sender"": {{}}
}}";

            if (!expectSuccess)
            {
                webhookPayload = webhookPayload.Replace("event property value", "another event property value");
            }

            string eventPayloadObject = @$"{{{payloadForEvent}}}";

            Repository repository = new Repository(organizationId: 1, repositoryId: 1, "OrganizationLogin", "RepositoryName");
            JObject payload = JObject.Parse(eventPayloadObject);
            JObject record = JObject.Parse(webhookPayload);

            string eventType = "ForkEvent";
            IHasher eventHasher = EventHasherFactory.Instance.GetEventHasher(eventType, new NoopTelemetryClient());
            IHasher payloadHasher = PayloadHasherFactory.Instance.GetEventHasher("fork", new NoopTelemetryClient());
            if (expectSuccess)
            {
                Assert.AreEqual(eventHasher.ComputeSha256Hash(payload, repository), payloadHasher.ComputeSha256Hash(record, repository));
            }
            else
            {
                Assert.AreNotEqual(eventHasher.ComputeSha256Hash(payload, repository), payloadHasher.ComputeSha256Hash(record, repository));
            }
        }
    }
}
