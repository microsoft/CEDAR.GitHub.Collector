using Azure.Core;
using System;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Authentication
{
    /// <summary>
    /// Adding this abstract class to align GitHub collector and CodeAsData authentication workflow.
    /// CodeAsData will have an implementation with Octokit, while GitHub collector use its own underlying implementation.
    /// </summary>
    public abstract class GitHubAppAuthenticationBase
    {
        protected string organization;
        protected readonly int appId;
        protected readonly string gitHubAppKeyVaultUri;

        public GitHubAppAuthenticationBase(string organization, int appId, string gitHubAppKeyVaultUri)
        {
            this.organization = organization;
            this.appId = appId;
            this.gitHubAppKeyVaultUri = gitHubAppKeyVaultUri; 
        }

        /// <summary>
        /// Main function to get the installation token.
        /// </summary>
        protected async Task<string> GetInstallationTokenAsync(string jwt)
        {
            if (organization == null)
            {
                throw new Exception("Organization must has value to get the installation token.");
            }

            string installationId = await FindInstallationId(jwt).ConfigureAwait(false);
            string result = await ObtainPat(jwt, installationId).ConfigureAwait(false);
            return result;
        }

        public abstract Task<string> GetAuthorizationHeaderAsync();

        protected abstract Task<string> FindInstallationId(string jwt = null);

        protected abstract Task<string> ObtainPat(string jwt, string installationId);

        protected abstract string CreateJwt(TokenCredential credential);
    }
}
