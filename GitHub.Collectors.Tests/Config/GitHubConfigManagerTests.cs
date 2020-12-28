// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Config;
using Microsoft.CloudMine.Core.Collectors.Context;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Collectors.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Tests.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Tests.Web;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.GitHub.Collectors.Authentication;
using Microsoft.CloudMine.GitHub.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Model;
using Microsoft.CloudMine.GitHub.Collectors.Tests.Helpers;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Microsoft.CloudMine.Core.Collectors.Tests.Authentication
{
    [TestClass]
    public class GitHubConfigManagerTests
    {
        private GitHubConfigManager configManager;
        private GitHubHttpClient httpClient;
        private ITelemetryClient telemetryClient;

        [TestInitialize]
        public void Setup()
        {
            string jsonInput = @"
            {
                'Authentication' : {
                    'Type' : 'Basic',
                    'Identity' : 'msftgits',
                    'PersonalAccessTokenEnvironmentVariable' : 'PersonalAccessToken'
                },
                'Storage': [
                    {
                        'Type': 'AzureDataLakeStorageV1',
                        'RootFolder': 'GitHub',
                        'Version': 'v1'
                    },
                    {
                        'Type': 'AzureBlob',
                        'RootContainer': 'github',
                        'OutputQueueName': 'github'
                    }
                ],
                'Collectors' : {
                    'Main' : {},
                    'Onboarding' : {
                        'Authentication' : {
                            'Type' : 'GitHubApp',
                            'AppId' : '7',
                            'GitHubAppKeyUri' : 'https://dummyuri.com/'
                        },
                    }
                }
            }";

            this.configManager = new GitHubConfigManager(jsonInput);
            this.telemetryClient = new NoopTelemetryClient();
            this.httpClient = new GitHubHttpClient(new FixedHttpClient(), new NoopRateLimiter(), new NoopCache<ConditionalRequestTableEntity>(), this.telemetryClient);
        }

        [TestMethod]
        public void GetAuthentication()
        {
            Assert.IsTrue(this.configManager.GetAuthentication("Main") is BasicAuthentication);
            
            string organization = "Organization";
            string apiDomain = "ApiDomain";
            Assert.IsTrue(this.configManager.GetAuthentication(CollectorType.Onboarding, this.httpClient, organization, apiDomain) is GitHubAppAuthentication);
        }

        [TestMethod]
        public void GetStorage()
        {
            StorageManager storageManager = this.configManager.GetStorageManager("Main", this.telemetryClient);
            string identifier = "identifier";
            FunctionContext functionContext = new FunctionContext();
            FunctionContextWriter<FunctionContext> contextWriter = new FunctionContextWriter<FunctionContext>();
            AdlsClientWrapper adlsClientWrapper = new AdlsClientWrapper();
            List<IRecordWriter> recordWriters = storageManager.InitializeRecordWriters(identifier, functionContext, contextWriter, adlsClientWrapper.AdlsClient);
            Assert.AreEqual(2, recordWriters.Count);
            Assert.IsTrue(recordWriters[0] is AdlsBulkRecordWriter<FunctionContext>);
            Assert.IsTrue(recordWriters[1] is AzureBlobRecordWriter<FunctionContext>);
        }
    }
}
