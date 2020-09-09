// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.CloudMine.Core.Collectors.Authentication;
using Microsoft.CloudMine.Core.Collectors.Collector;
using Microsoft.CloudMine.Core.Collectors.IO;
using Microsoft.CloudMine.Core.Collectors.Telemetry;
using Microsoft.CloudMine.Core.Collectors.Web;
using Microsoft.CloudMine.GitHub.Collectors.Web;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Collector
{
    public class GitHubCollector : CollectorBase<GitHubCollectionNode>
    {
        private readonly GitHubHttpClient httpClient;

        public GitHubCollector(GitHubHttpClient httpClient, IAuthentication authentication, ITelemetryClient telemetryClient, List<IRecordWriter> recordWriters)
            : base(authentication, telemetryClient, recordWriters)
        {
            this.httpClient = httpClient;
        }

        protected override async Task<SerializedResponse> ParseResponseAsync(HttpResponseMessage response, GitHubCollectionNode collectionNode)
        {
            List<JObject> records = new List<JObject>();
            JObject responseObject = null;

            if (collectionNode.ResponseType == typeof(JArray))
            {
                JArray responseItems = await HttpUtility.ParseAsJArrayAsync(response).ConfigureAwait(false);
                foreach (JToken responseItem in responseItems)
                {
                    // There are cases where GitHub API returns null values as elements of the response array, which cannot be casted to JObject.
                    // Example: https://api.github.com/repos/microsoft/omi/pulls/comments?per_page=100
                    // Therefore, check for such cases and skip those elements.
                    if (responseItem is JValue && ((JValue)responseItem).Type == JTokenType.Null)
                    {
                        continue;
                    }

                    JObject record = (JObject)responseItem;
                    records.Add(record);
                }
            }
            else
            {
                JObject record = await HttpUtility.ParseAsJObjectAsync(response).ConfigureAwait(false);
                responseObject = record;
                records.Add(record);
            }

            return new SerializedResponse(responseObject, records);
        }

        protected override IBatchingHttpRequest WrapIntoBatchingHttpRequest(GitHubCollectionNode collectionNode)
        {
            string initialUrl = collectionNode.GetInitialUrl(collectionNode.AdditionalMetadata);
            return new BatchingGitHubHttpRequest(this.httpClient, initialUrl, collectionNode.ApiName, collectionNode.WhitelistedResponses);
        }
    }
}
