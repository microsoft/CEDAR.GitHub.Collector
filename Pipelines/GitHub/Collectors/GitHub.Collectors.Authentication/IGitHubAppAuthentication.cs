using Azure.Core;
using System;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Authentication
{
    /// <summary>
    /// Interface to align GitHub collector and CodeAsData authentication workflow.
    /// CodeAsData will have an implementation with Octokit, while GitHub collector use its own underlying implementation.
    /// </summary>
    public interface IGitHubAppAuthentication
    {
        Task<string> GetAuthorizationHeaderAsync();
    }
}
