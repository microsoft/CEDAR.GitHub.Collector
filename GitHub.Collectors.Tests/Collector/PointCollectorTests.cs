using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Collectors.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Tests.IO;
using Microsoft.CloudMine.Core.Collectors.Tests.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Tests.Web;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Collector;
using Microsoft.CloudMine.GitHub.Collectors.Model;
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
    public class PointCollectorTests
    {
        [TestMethod]
        public async Task ProcessAsyncTest()
        {
            Repository repository = new Repository(1234, 5678, "organization", "repository");
            PointCollectorInput input = new PointCollectorInput()
            {
                Url = "exampleUrl",
                RecordType = "exampleRecordType",
                ApiName = "exampleApiName",
                Repository = repository,
                ResponseType = "Object"
            };
            ICache<PointCollectorTableEntity> cache = new NoopCache<PointCollectorTableEntity>();
            FixedHttpClient httpClient = new FixedHttpClient();
            string response = "{\"Data\":\"Test\"}";
            httpClient.AddResponse("exampleUrl", HttpStatusCode.OK, response);
            ITelemetryClient telemetryClient = new NoopTelemetryClient();
            GitHubHttpClient githubHttpClient = new GitHubHttpClient(httpClient, new NoopRateLimiter(), new NoopCache<ConditionalRequestTableEntity>(), telemetryClient);
            IAuthentication authentication = new BasicAuthentication("Identity", "PersonalAccessToken");
            List<IRecordWriter> recordWriters = new List<IRecordWriter>();
            InMemoryRecordWriter recordWriter = new InMemoryRecordWriter();
            recordWriters.Add(recordWriter);
            PointCollector pointCollector = new PointCollector(authentication, recordWriters, githubHttpClient, cache, telemetryClient);
            await pointCollector.ProcessAsync(input);

            List<Tuple<JObject, RecordContext>> records = recordWriter.GetRecords();
            Assert.AreEqual(1, records.Count);
            Assert.AreEqual("Test", records[0].Item1.SelectToken("$.Data").Value<string>());
            Assert.AreEqual("exampleUrl", records[0].Item2.AdditionalMetadata["OriginatingUrl"]);
        }
    }
}
