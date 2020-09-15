// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Collectors.Tests.IO;
using Microsoft.CloudMine.Core.Collectors.Tests.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Tests.Web;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Collector;
using Microsoft.CloudMine.GitHub.Collectors.Tests.Helpers;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Tests.Collector
{
    [TestClass]
    public class GitHubCollectorTests
    {
        private FixedHttpClient httpClient;
        private InMemoryRecordWriter recordWriter;
        private GitHubCollector collector;

        [TestInitialize]
        public void Setup()
        {
            this.httpClient = new FixedHttpClient();

            GitHubHttpClient githubHttpClient = new GitHubHttpClient(this.httpClient, new NoopRateLimiter(), new NoopCache<ConditionalRequestTableEntity>(), new NoopTelemetryClient());
            this.recordWriter = new InMemoryRecordWriter();

            IAuthentication authentication = new BasicAuthentication("Identity", "PersonalAccessToken");
            List<IRecordWriter> recordWriters = new List<IRecordWriter>();
            recordWriters.Add(this.recordWriter);
            this.collector = new GitHubCollector(githubHttpClient, authentication, new NoopTelemetryClient(), recordWriters);
        }

        [TestMethod]
        public async Task ProcessAsync_Success_JArray()
        {
            string url = "SomeUrl";
            string response = @"
[
    {
        ""ProjectId"": ""ProjectId1""
    },
    {
        ""ProjectId"": ""ProjectId2""
    },
]
";

            this.httpClient.AddResponse(url, HttpStatusCode.OK, response);

            GitHubCollectionNode collectionNode = new GitHubCollectionNode()
            {
                ApiName = "ApiName",
                RecordType = "RecordType",
                GetInitialUrl = metadata => url,
            };
            await this.collector.ProcessAsync(collectionNode).ConfigureAwait(false);

            List<Tuple<JObject, RecordContext>> records = this.recordWriter.GetRecords();
            Assert.AreEqual(2, records.Count);

            Assert.AreEqual("ProjectId1", records[0].Item1.SelectToken("$.ProjectId").Value<string>());
            RecordContext recordContext1 = records[0].Item2;
            Assert.AreEqual(url, recordContext1.AdditionalMetadata["OriginatingUrl"]);
            Assert.AreEqual("RecordType", recordContext1.RecordType);

            Assert.AreEqual("ProjectId2", records[1].Item1.SelectToken("$.ProjectId").Value<string>());
            RecordContext recordContext2 = records[1].Item2;
            Assert.AreEqual(url, recordContext2.AdditionalMetadata["OriginatingUrl"]);
            Assert.AreEqual("RecordType", recordContext2.RecordType);
        }

        [TestMethod]
        public async Task ProcessAsync_Success_JObject()
        {
            string url = "SomeUrl";
            string response = @"
{
    ""ProjectId"": ""ProjectId1""
}
";

            this.httpClient.AddResponse(url, HttpStatusCode.OK, response);

            GitHubCollectionNode collectionNode = new GitHubCollectionNode()
            {
                ApiName = "ApiName",
                RecordType = "RecordType",
                GetInitialUrl = metadata => url,
                ResponseType = typeof(JObject)
            };
            await this.collector.ProcessAsync(collectionNode).ConfigureAwait(false);

            List<Tuple<JObject, RecordContext>> records = this.recordWriter.GetRecords();
            Assert.AreEqual(1, records.Count);

            Assert.AreEqual("ProjectId1", records[0].Item1.SelectToken("$.ProjectId").Value<string>());
            RecordContext recordContext1 = records[0].Item2;
            Assert.AreEqual(url, recordContext1.AdditionalMetadata["OriginatingUrl"]);
            Assert.AreEqual("RecordType", recordContext1.RecordType);
        }
    }
}
