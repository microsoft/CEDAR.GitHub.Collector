using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Tests.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Tests.Web;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.GitHub.Collectors.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Web.Tests
{

    public class GitHubRateLimiterTestClass : GitHubRateLimiter
    {
        public GitHubRateLimiterTestClass(string organizationId, ICache<RateLimitTableEntity> rateLimiterCache, IHttpClient httpClient, ITelemetryClient telemetryClient, double maxUsageBeforeDelayStarts, string apiDomain, bool throwOnRateLimit = false)
            : base(organizationId, rateLimiterCache, httpClient, telemetryClient, maxUsageBeforeDelayStarts, apiDomain, throwOnRateLimit)
        {
        
        }

        public async Task ExposedWaitIfNeededAsync(IAuthentication authentication, RateLimitTableEntity tableEntity)
        {
            await base.WaitIfNeededAsync(authentication, tableEntity);
        }
    }

    [TestClass]
    public class GitHubRateLimiterTests
    {
        private GitHubRateLimiterTestClass gitHubRateLimiter;

        [TestInitialize]
        public void Setup()
        {
            ICache<RateLimitTableEntity> cache = new NoopCache<RateLimitTableEntity>();
            IHttpClient httpClient = new FixedHttpClient();
            ITelemetryClient telemetryClient = new NoopTelemetryClient();
            this.gitHubRateLimiter = new GitHubRateLimiterTestClass("organizationId", cache, httpClient, telemetryClient, 90, "apidomain", throwOnRateLimit : true);
        }

        [TestMethod]
        public async Task ThrowOnWaitIfNeededTest()
        {
            DateTime rateLimitReset = DateTime.UtcNow.AddSeconds(10);
            RateLimitTableEntity tableEntity = new RateLimitTableEntity("identity", "organizationId", "organizationName", 100, 2, rateLimitReset, null);
            IAuthentication auth = new BasicAuthentication("Identity", "PersonalAccessToken");
            try
            {
                await this.gitHubRateLimiter.ExposedWaitIfNeededAsync(auth, tableEntity);
                Assert.Fail();
            }
            catch (GitHubRateLimitException exception)
            {
                Assert.AreEqual("RateLimitRequeue", exception.Message);
                Assert.IsTrue((TimeSpan) exception.GetHiddenTime() < TimeSpan.FromSeconds(11));
                Assert.IsTrue((TimeSpan) exception.GetHiddenTime() > TimeSpan.FromSeconds(9));
            }

            tableEntity = new RateLimitTableEntity("identity", "organizationId", "organizationName", 100, 90, rateLimitReset, null);
            await this.gitHubRateLimiter.ExposedWaitIfNeededAsync(auth, tableEntity);
        }

        [TestMethod]
        public void TestSetInitialVisibilityDelay()
        {
            TimeSpan ts = TimeSpan.FromMilliseconds(-5);
            GitHubRateLimitException e = new GitHubRateLimitException(ts);
            Assert.AreEqual(TimeSpan.FromMilliseconds(0), e.GetHiddenTime());
        }
    }
}
