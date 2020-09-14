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
