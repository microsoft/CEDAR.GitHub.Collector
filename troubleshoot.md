---
uid: cloudmine_dri_github_collectors
---

# GitHub HowTo and Troubleshooting

> Note: Kivanc (kivancm) is the expert for GitHub collectors. If this TSG and/or alerts are not good enough to cover different scenarios you experienced during your DRI shift, please reach out to them for additional knowledge and investigation. Please contribute back this additional knowledge to the TSGs so that they become more complete in time.

As of this writing (04/08/2021), there are three deployments of the GitHub collectors: GitHub.com (MSFT), GHAE (PME), and GitHub.Private (PME). 

See [CloudMine Collectors](xref:cloudmine_dri_collectors#cloudmine-collectors) for a high-level introduction on CloudMine collectors.

As mentioned above, collector resource (group)s are scattered between MSFT and PME tenants. Most importantly, all telemetry flows into the following log analytics workspace:
* [Telemetry from collectors, including GitHub.com](https://portal.azure.com/#@mspmecloud.onmicrosoft.com/resource/subscriptions/fafe1a04-401e-47f3-afd1-b0ecb56a11cd/resourceGroups/CloudMine-Prod-GitHub-Collector-WUS23/providers/Microsoft.OperationalInsights/workspaces/GitHubCollector/Overview)
* [Telemetry from Logic App (PME Tenant - GHAE and GitHub.Private)](https://portal.azure.com/#@mspmecloud.onmicrosoft.com/resource/subscriptions/fafe1a04-401e-47f3-afd1-b0ecb56a11cd/resourceGroups/CloudMine-Prod-GitHub-Collector-WUS23/providers/Microsoft.OperationalInsights/workspaces/GitHubCollectorLogicApp/Overview)
* [Telemetry from Logic App (MSFT Tenant - GitHub.com)](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/4bea6b95-564c-4ca3-9ff5-bfb4493daf45/resourceGroups/cloudminegithub/providers/Microsoft.OperationsManagement/solutions/LogicAppsManagement(CloudMineGitHubCollector)/Overview)
* [Telemetry from ROS](https://portal.azure.com/#@mspmecloud.onmicrosoft.com/resource/subscriptions/fafe1a04-401e-47f3-afd1-b0ecb56a11cd/resourceGroups/CloudMine-Prod-GitHub-Collector-WUS23/providers/Microsoft.OperationalInsights/workspaces/GitHubCollectorRos/Overview)

## DRI Duties

The DRI has the following duties:
* Monitor the ICM queue: You can use [this query](https://portal.microsofticm.com/imp/v3/incidents/search/advanced?qId=116165) to monitor all non-resolved incidents. This is your primary duty as the DRI since majority of failures create an incident. These incidents should also come with a TSG (linked back to this Wiki).
* Monitor collectors health: this is not very fleshed out at this point, but you can use the following dashboards:
  * [MSFT](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/dashboard/arm/subscriptions/4bea6b95-564c-4ca3-9ff5-bfb4493daf45/resourcegroups/cloudminegithub/providers/microsoft.portal/dashboards/0d7c3e3b-08f1-4e19-9a4b-b423e3049337)
  * [PME](https://portal.azure.com/#@mspmecloud.onmicrosoft.com/dashboard/arm/subscriptions/fafe1a04-401e-47f3-afd1-b0ecb56a11cd/resourcegroups/dashboards/providers/microsoft.portal/dashboards/3b0f4e46-4ca1-4c74-a68e-28622553b074)
* Re-run failed Logic App runs. This is the main task that ensures we don't miss processing a Webhook event. This needs to be done at least once every week. I suggest doing on Monday and Tuesday and Friday. Please see the subsection below.

### Re-running failed Logic App runs

Use the following query to retrieve a list of `Resubmit URL` for the known failing Logic App runs.

```csl
let lookback = 60d; // Adjust lookback accordingly
AzureDiagnostics
| where TimeGenerated >= ago(lookback)
| where OperationName == "Microsoft.Logic/workflows/workflowRunCompleted"
| extend Ring = case(resource_workflowName_s == "GitHubCollectorWUS23", "[Ring3 (Private)]",
                        resource_workflowName_s == "GitHubCollectorWUS22-V2", "[Ring2 (GHAE)]",
                        resource_workflowName_s == "CloudMineGitHubCollector2", "[Ring1]",
                        strcat("Unknown ring for resource: ", resource_workflowName_s))
| project TrackingId = correlation_clientTrackingId_s,
            TimeGenerated,
            Status = status_s,
            RunId = resource_originRunId_s,
            WorkFlowId = workflowId_s,
            Ring
| summarize hint.strategy=shuffle TimeGenerated = argmax(TimeGenerated, *) by TrackingId, Ring
| project-rename RunId = max_TimeGenerated_RunId, Status = max_TimeGenerated_Status, WorkFlowId = max_TimeGenerated_WorkFlowId
| extend ResubmitUrl = strcat("https://management.azure.com", WorkFlowId, "/triggers/manual/histories/", RunId, "/resubmit?api-version=2016-06-01")
| where Status == "Failed"
| join kind=leftouter (
    AzureDiagnostics
    | where TimeGenerated >= ago(lookback)
    | where OperationName == "Microsoft.Logic/workflows/workflowActionCompleted" and Resource == "POST_TO_AZURE_DURABLE_FUNCTION"
    | project OrganizationLogin = trackedProperties_OrganizationLogin_s,
                OrganizationId = trackedProperties_OrganizationId_s,
                RunId = resource_runId_s,
                RepositoryId = trackedProperties_RepositoryId_s,
                RepositoryName = trackedProperties_RepositoryName_s,
                SenderLogin = trackedProperties_SenderLogin_s,
                OrganizationUrl = trackedProperties_OrganizationUrl_s,
                EventType = trackedProperties_EventType_s,
                Status = status_s
) on RunId 
| extend HasTrackedProperties = not(isempty(RunId1) or isnull(RunId1)) | project-away RunId1
| where isempty(OrganizationUrl) or OrganizationUrl == "https://api.github.com/orgs/github"
| order by TimeGenerated asc
| take 30000
| project ResubmitUrl
```

Run this query on the right LogAnalyticsWorkspace:
* [GHAE and GitHub.Private (PME)](https://portal.azure.com/#@mspmecloud.onmicrosoft.com/resource/subscriptions/fafe1a04-401e-47f3-afd1-b0ecb56a11cd/resourceGroups/CloudMine-Prod-GitHub-Collector-WUS23/providers/Microsoft.OperationalInsights/workspaces/GitHubCollectorLogicApp/Overview)
* [GitHub.com (MSFT)](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/4bea6b95-564c-4ca3-9ff5-bfb4493daf45/resourceGroups/cloudminegithub/providers/Microsoft.OperationsManagement/solutions/LogicAppsManagement(CloudMineGitHubCollector)/Overview)

This query gets you the top 30,000 failures because that is the limit that LogAnalyticsWorkspace will let you see / export. Once the query executes, export the results to CSV. Then, you can re-run these failed logic apps by:
* Copy and paste the contents of the downloaded CSV to `FailedLogicApps.txt` located at: `<enlistment root>\Pipelines\GitHub\Tools\`
* Run `Resubmit_LogicAppRuns.ps1` PowerShell script located in the same location.

> Note: This PowerShell script submits requests in parallel (10 default) and is quite CPU extensive. Running on a laptop is not recommended. If you have to, you can either reduce parallelism or run it before leaving work for night so that it does not interrupt your work. Also, before running the script, please login to the right subscription / tenant by execution `az login` on the same PowerShell session.

### Onboarding a new organization

To be writtern later.

## Trouble Shooting Guidelines (TSGs)

All collector ICMs are designed on Azure Alerts. See [Azure Alerts](xref:cloudmine_dri_collectors#azure-alerts) for a high-level introduction to Azure Alerts.

### [GitHub] [`Region`] Too many logic app failures (Threshold = `threshold value`, current = `current value`)

Indicates that there had been too many Logic App failures in the last day. The DRI should check the dashboards to see whether this is something transient or ongoing. If ongoing, the root cause needs to be investigates (exceptions might be helpful here). Once the root cause is identified, [the failed logic apps needs to be re-run](#re-running-failed-logic-app-runs).

### [GitHub] [`Region`] P99 main collector duration higher than threshold (threshold = `threshold value`, current = `current value`)

Indicates that processing GitHub Webhook request processing is backed-up higher than the permitted threshold. This generally happens when (either):
* GitHub starts firing Webhooks at an increased rate.
* There is high throttling on GitHub web requests.

The DRI should check the dashboards to see whether this is something transient or ongoing. If ongoing, the root cause needs to be investigates (exceptions might be helpful here). When not treated, this generally leads to other issues (e.g., Logic Apps failing due to timeout, Azure Functions exhausting available ports and going into bad state) and can escalate bad.

### [GitHub] [`Region`] Traffic failure on `Date` for `Repository` with SessionId: `SessionId`

Indicates that traffic collection for the given repository has failed for the given date. One common scenario for this is because the repository is deleted before we could collect the traffic APIs. To confirm that use the ValidationUrl part of the search query results in the ICM discussions. If that request give you a 404 response, e.g.:

```json
{
  "message": "Not Found",
  "documentation_url": "https://docs.github.com/rest/reference/repos#get-a-repository"
}
```

then it is safe to resolve the incident and delete the corresponding poison message per TSG.

If the repository still exists, then the failure requires further investigation. Exception logs for the given session can be helpful, which can be retrieved via the following query:

```csl
AppExceptions
| extend SessionId = tostring(Properties.SessionId)
| where SessionId == "dce46769-6d32-45fd-851e-fd493512152b" \\ Adjust session ID accordingly
```

### [GitHub] [`Region`] TrafficTimer failure on `Date` with SessionId: `SessionId`

Indicates that the Timer-triggered Azure Function to initiate the Traffic collection on all repositories has failed for the given date. The failure requires investigation via the exception logs, which can be retrieved via the following query:

```csl
AppExceptions
| extend SessionId = tostring(Properties.SessionId)
| where SessionId == "dce46769-6d32-45fd-851e-fd493512152b" \\ Adjust session ID accordingly
```
