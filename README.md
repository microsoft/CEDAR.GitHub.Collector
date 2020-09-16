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

#### Test the Onboarding Collector
To test that your functions are configured correctly you can run them from Visual Studio. 

Select the Debug solution configuration and run the GutHub.Collectors.Functions

After the functions Initialize you will see “Now listening on http://0.0.0.0.7071”

Create a storage queue named “onboarding”. Messages added to this queue will kick off the onboarding Azure Function. 
Test the Onboarding function by adding an onboarding message to the onboarding queue in your storage account.  These messages are in the format: 

```
{
    "OrganizationId": #####,
    "OrganizationLogin": "xxxxx",
    "RepositoryId": #####,
    "RepositoryName": "xxxxx”,
    "OnboardingType": "Repository"
}
```

After the function has completed you should be able to see your collected data under the “github” blob container in your storage account.

To consume the telemetry data from your functions you can visit Application Insights and navigate to the Monitoring -> Logs tab. There you can query the log data from your functions.
Ex: 
```
dependencies
| where operation_Name == "Onboard"
| take 100
```
 
## 7. Make and debug changes
Create and checkout feature branches from your fork on your local machine and make your contributions to the code base.

Test your and debug your changes. Running the GitHub.Collectors.Functions in the Debug Configuration will allow you to use the Visual Studio debugging tools while your functions run. (Breakpoints, Variable Tracking, etc...)

**Note:** CEDAR.GitHub.Collector depend on [CEDAR.Core.Collector](https://github.com/microsoft/CEDAR.Core.Collector) and consumes the latter as a Git submodule. If you are making changes on CEDAR.Core.Collector, you need to first create a PR on that repository (following the same practices mentioned here) have that PR merged and create your PR with the updated submodule SHA in this repository. 

## 8. Write unit tests to cover new code
New code should be covered by comprehensive unit tests using the Microsoft.VisualStudio.TestTools.UnitTesting framework. 

## 9. Commit and Push your changes and make a Pull Request
When your contributions have been tested you can commit them to your remote branch and request that your changes be merged into the CEDAR.GitHub.Collector repository.
