[![Build Status](https://dev.azure.com/mseng/Domino/_apis/build/status/CloudMine/Pipelines/GitHub/Collectors/CEDAR.GitHub.Collector-PR?branchName=main)](https://dev.azure.com/mseng/Domino/_build/latest?definitionId=10573&branchName=main)

# Introduction


CEDAR.GitHub.Collector is a set of Azure Functions to collect engineering metadata from GitHub. It consists of four collectors:
1. Main: the main collector processes the data coming directly from the GitHub Webhooks
2. Delta: the delta collector makes requests against the [EventsTimeline API](https://developer.github.com/v3/activity/events/#list-repository-events) to ensure that data is not missed through the main collector
3. Onboarding: the onboarding collector collects current state of a given GitHub repository / organization
4. Traffic: the traffic collects [Traffic API](https://docs.github.com/en/rest/reference/repos#traffic) data

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## 1. Download Required Software and Extensions
Developing and debugging the CEDAR.GitHub.Collector is easiest using Visual Studio (2017 or later) with the Azure Functions Tools extensions.

Download Visual Studio Here : https://visualstudio.microsoft.com/downloads/

The Azure Functions Tools extension can be installed during the VS installation process or added after download.

## 2. Fork Repository
Create a fork of this repository and open the GitHub.Collectors.sln solution file in Visual Studio. 


## 3. Create `local.settings.json`
Create a `local.settings.json` file in the under the GitHub.Collectors.Functions project.

Find the local.settings.template.json file and copy its contents into your new `local.settings.json` file.

Add your GitHub account identity under the key “Identity”.

Add a Personal Access Token associated with your GitHub account under the key “PersonalAccessToken”.

## 4. Setup Azure Storage 
In [Azure](https://portal.azure.com/) create an Azure storage account where the data you will be collecting from GitHub will be saved.

Paste the Connection String of this new storage account into your `local.settings.json` file under the key “AzureWebJobsStorage”.

## 5. Setup Application Insights
In [Azure](https://portal.azure.com/) create an Application Insights resource where telemetry from your function executions will be sent.

Add the Instrumentation key from this account into your `local.settings.json` file under the key “APPINSIGHTS_INSTRUMENTATIONKEY”.

## 6. Run the Azure Functions Locally with Visual Studio Code

In VisualStudio, select the Debug solution configuration and run the GutHub.Collectors.Functions

After the functions Initialize you will see “Now listening on http://0.0.0.0.7071”

### Test the Onboarding Collector

Create a storage queue named `onboarding`. Test the Onboarding function by adding the following message to the onboarding queue in your storage account":
```
{
    "OrganizationId": #####,
    "OrganizationLogin": "xxxxx",
    "RepositoryId": #####,
    "RepositoryName": "xxxxx”,
    "OnboardingType": "Repository",
    "IgnoreCache": true
}
```

After the function has completed you should be able to see your collected data under the `github` blob container in your storage account.

### Test the Traffic Collector

Create a storage queue named `traffic`. Test the Traffic function by adding the following message to the traffic queue in your storage account:
```
{
    "OrganizationId": #####,
    "OrganizationLogin": "xxxxx",
    "RepositoryId": #####,
    "RepositoryName": "xxxxx”
}
```

After the function has completed you should be able to see your collected data under the `github` blob container in your storage account.

### Test the Webhook Collector

To test the webhook collector, essentially you want to post a payload that is similar/same to a GitHub webhook payload to your localhost endpoint. Using your favorite program (e.g., [Postman](https://www.postman.com/) post the following (example) message body / headers to `http://localhost:7071/api/ProcessWebHook`:

**Headers:**
```
X-GitHub-Delivery: <any GUID of your choice>
X-GitHub-Event: "<a valid GitHub event, e.g., issue>
```

**Body**:
```
{
  "action": "opened",
  "issue": {
    "url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/issues/5",
    "repository_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector",
    "labels_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/issues/5/labels{/name}",
    "comments_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/issues/5/comments",
    "events_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/issues/5/events",
    "html_url": "https://github.com/microsoft/CEDAR.GitHub.Collector/issues/5",
    "id": 702507654,
    "node_id": "MDU6SXNzdWU3MDI1MDc2NTQ=",
    "number": 5,
    "title": "Expand ReadMe.md with details on how to test the remaining collectors",
    "user": {
      "login": "kivancmuslu",
      "id": 43969379,
      "node_id": "MDQ6VXNlcjQzOTY5Mzc5",
      "avatar_url": "https://avatars1.githubusercontent.com/u/43969379?v=4",
      "gravatar_id": "",
      "url": "https://api.github.com/users/kivancmuslu",
      "html_url": "https://github.com/kivancmuslu",
      "followers_url": "https://api.github.com/users/kivancmuslu/followers",
      "following_url": "https://api.github.com/users/kivancmuslu/following{/other_user}",
      "gists_url": "https://api.github.com/users/kivancmuslu/gists{/gist_id}",
      "starred_url": "https://api.github.com/users/kivancmuslu/starred{/owner}{/repo}",
      "subscriptions_url": "https://api.github.com/users/kivancmuslu/subscriptions",
      "organizations_url": "https://api.github.com/users/kivancmuslu/orgs",
      "repos_url": "https://api.github.com/users/kivancmuslu/repos",
      "events_url": "https://api.github.com/users/kivancmuslu/events{/privacy}",
      "received_events_url": "https://api.github.com/users/kivancmuslu/received_events",
      "type": "User",
      "site_admin": true
    },
    "labels": [],
    "state": "open",
    "locked": false,
    "assignee": null,
    "assignees": [],
    "milestone": null,
    "comments": 0,
    "created_at": "2020-09-16T06:54:49Z",
    "updated_at": "2020-09-16T06:54:49Z",
    "closed_at": null,
    "author_association": "MEMBER",
    "active_lock_reason": null,
    "body": "Currently, it only describes how to test the onboarding collector.",
    "performed_via_github_app": null
  },
  "repository": {
    "id": 282058629,
    "node_id": "MDEwOlJlcG9zaXRvcnkyODIwNTg2Mjk=",
    "name": "CEDAR.GitHub.Collector",
    "full_name": "microsoft/CEDAR.GitHub.Collector",
    "private": false,
    "owner": {
      "login": "microsoft",
      "id": 6154722,
      "node_id": "MDEyOk9yZ2FuaXphdGlvbjYxNTQ3MjI=",
      "avatar_url": "https://avatars2.githubusercontent.com/u/6154722?v=4",
      "gravatar_id": "",
      "url": "https://api.github.com/users/microsoft",
      "html_url": "https://github.com/microsoft",
      "followers_url": "https://api.github.com/users/microsoft/followers",
      "following_url": "https://api.github.com/users/microsoft/following{/other_user}",
      "gists_url": "https://api.github.com/users/microsoft/gists{/gist_id}",
      "starred_url": "https://api.github.com/users/microsoft/starred{/owner}{/repo}",
      "subscriptions_url": "https://api.github.com/users/microsoft/subscriptions",
      "organizations_url": "https://api.github.com/users/microsoft/orgs",
      "repos_url": "https://api.github.com/users/microsoft/repos",
      "events_url": "https://api.github.com/users/microsoft/events{/privacy}",
      "received_events_url": "https://api.github.com/users/microsoft/received_events",
      "type": "Organization",
      "site_admin": false
    },
    "html_url": "https://github.com/microsoft/CEDAR.GitHub.Collector",
    "description": "Data collection pipeline for GitHub",
    "fork": false,
    "url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector",
    "forks_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/forks",
    "keys_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/keys{/key_id}",
    "collaborators_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/collaborators{/collaborator}",
    "teams_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/teams",
    "hooks_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/hooks",
    "issue_events_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/issues/events{/number}",
    "events_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/events",
    "assignees_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/assignees{/user}",
    "branches_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/branches{/branch}",
    "tags_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/tags",
    "blobs_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/git/blobs{/sha}",
    "git_tags_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/git/tags{/sha}",
    "git_refs_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/git/refs{/sha}",
    "trees_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/git/trees{/sha}",
    "statuses_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/statuses/{sha}",
    "languages_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/languages",
    "stargazers_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/stargazers",
    "contributors_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/contributors",
    "subscribers_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/subscribers",
    "subscription_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/subscription",
    "commits_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/commits{/sha}",
    "git_commits_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/git/commits{/sha}",
    "comments_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/comments{/number}",
    "issue_comment_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/issues/comments{/number}",
    "contents_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/contents/{+path}",
    "compare_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/compare/{base}...{head}",
    "merges_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/merges",
    "archive_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/{archive_format}{/ref}",
    "downloads_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/downloads",
    "issues_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/issues{/number}",
    "pulls_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/pulls{/number}",
    "milestones_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/milestones{/number}",
    "notifications_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/notifications{?since,all,participating}",
    "labels_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/labels{/name}",
    "releases_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/releases{/id}",
    "deployments_url": "https://api.github.com/repos/microsoft/CEDAR.GitHub.Collector/deployments",
    "created_at": "2020-07-23T21:26:30Z",
    "updated_at": "2020-09-15T22:06:22Z",
    "pushed_at": "2020-09-16T06:53:28Z",
    "git_url": "git://github.com/microsoft/CEDAR.GitHub.Collector.git",
    "ssh_url": "git@github.com:microsoft/CEDAR.GitHub.Collector.git",
    "clone_url": "https://github.com/microsoft/CEDAR.GitHub.Collector.git",
    "svn_url": "https://github.com/microsoft/CEDAR.GitHub.Collector",
    "homepage": "",
    "size": 74,
    "stargazers_count": 1,
    "watchers_count": 1,
    "language": "C#",
    "has_issues": true,
    "has_projects": true,
    "has_downloads": true,
    "has_wiki": true,
    "has_pages": false,
    "forks_count": 1,
    "mirror_url": null,
    "archived": false,
    "disabled": false,
    "open_issues_count": 2,
    "license": {
      "key": "mit",
      "name": "MIT License",
      "spdx_id": "MIT",
      "url": "https://api.github.com/licenses/mit",
      "node_id": "MDc6TGljZW5zZTEz"
    },
    "forks": 1,
    "open_issues": 2,
    "watchers": 1,
    "default_branch": "main"
  },
  "organization": {
    "login": "microsoft",
    "id": 6154722,
    "node_id": "MDEyOk9yZ2FuaXphdGlvbjYxNTQ3MjI=",
    "url": "https://api.github.com/orgs/microsoft",
    "repos_url": "https://api.github.com/orgs/microsoft/repos",
    "events_url": "https://api.github.com/orgs/microsoft/events",
    "hooks_url": "https://api.github.com/orgs/microsoft/hooks",
    "issues_url": "https://api.github.com/orgs/microsoft/issues",
    "members_url": "https://api.github.com/orgs/microsoft/members{/member}",
    "public_members_url": "https://api.github.com/orgs/microsoft/public_members{/member}",
    "avatar_url": "https://avatars2.githubusercontent.com/u/6154722?v=4",
    "description": "Open source projects and samples from Microsoft"
  },
  "enterprise": {
    "id": 1578,
    "slug": "microsoftopensource",
    "name": "Microsoft Open Source",
    "node_id": "MDEwOkVudGVycHJpc2UxNTc4",
    "avatar_url": "https://avatars0.githubusercontent.com/b/1578?v=4",
    "description": "Microsoft's organizations for open source collaboration",
    "website_url": "https://opensource.microsoft.com",
    "html_url": "https://github.com/enterprises/microsoftopensource",
    "created_at": "2019-12-09T02:41:53Z",
    "updated_at": "2020-05-19T18:21:45Z"
  },
  "sender": {
    "login": "kivancmuslu",
    "id": 43969379,
    "node_id": "MDQ6VXNlcjQzOTY5Mzc5",
    "avatar_url": "https://avatars1.githubusercontent.com/u/43969379?v=4",
    "gravatar_id": "",
    "url": "https://api.github.com/users/kivancmuslu",
    "html_url": "https://github.com/kivancmuslu",
    "followers_url": "https://api.github.com/users/kivancmuslu/followers",
    "following_url": "https://api.github.com/users/kivancmuslu/following{/other_user}",
    "gists_url": "https://api.github.com/users/kivancmuslu/gists{/gist_id}",
    "starred_url": "https://api.github.com/users/kivancmuslu/starred{/owner}{/repo}",
    "subscriptions_url": "https://api.github.com/users/kivancmuslu/subscriptions",
    "organizations_url": "https://api.github.com/users/kivancmuslu/orgs",
    "repos_url": "https://api.github.com/users/kivancmuslu/repos",
    "events_url": "https://api.github.com/users/kivancmuslu/events{/privacy}",
    "received_events_url": "https://api.github.com/users/kivancmuslu/received_events",
    "type": "User",
    "site_admin": true
  }
}
```

After the function has completed you should be able to see your collected data under the `github` blob container in your storage account.

### Investigating collector telemetry

Long running functions (Onboarding and Traffic) print some additional progress stats when theya re executed locally. However, richer telemetry is sent to Application Insights. To consume the telemetry data from your functions you can visit Application Insights and navigate to the Monitoring -> Logs tab. 

#### Retrieve session events

```
customEvents
| where name in ("SessionStart", "SessionEnd")
| extend Context = parse_json(customDimensions)
| extend SessionId = tostring(Context.SessionId),
         CollectorType = tostring(Context.CollectorType),
         Success = tostring(Context.Success)
```

#### Retrieve requests done in a particular session

```
let sessionId = "<session ID>";
dependencies
| extend Context = parse_json(customDimensions)
| extend SessionId = tostring(Context.SessionId),
| where SessionId == sessionId
| order by timestamp desc
```

#### Retrieve exceptions in a particular session

```
let sessionId = "<session ID>";
exceptions
| extend Context = parse_json(customDimensions)
| extend SessionId = tostring(Context.SessionId),
| where SessionId == sessionId
| order by timestamp desc
```

## 7. Make and debug changes
Create and checkout feature branches from your fork on your local machine and make your contributions to the code base.

Test your and debug your changes. Running the GitHub.Collectors.Functions in the Debug Configuration will allow you to use the Visual Studio debugging tools while your functions run. (Breakpoints, Variable Tracking, etc...)

**Note:** CEDAR.GitHub.Collector depend on [CEDAR.Core.Collector](https://github.com/microsoft/CEDAR.Core.Collector) and consumes the latter as a Git submodule. If you are making changes on CEDAR.Core.Collector, you need to first create a PR on that repository (following the same practices mentioned here) have that PR merged and create your PR with the updated submodule SHA in this repository. 

## 8. Write unit tests to cover new code
New code should be covered by comprehensive unit tests using the Microsoft.VisualStudio.TestTools.UnitTesting framework. 

## 9. Commit and Push your changes and make a Pull Request
When your contributions have been tested you can commit them to your remote branch and request that your changes be merged into the CEDAR.GitHub.Collector repository.
