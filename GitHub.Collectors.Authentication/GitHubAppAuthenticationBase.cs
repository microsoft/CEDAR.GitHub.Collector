using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Authentication
{
    /// <summary>
    /// Abstract class to align GitHub collector and CodeAsData authentication workflow.
    /// CodeAsData will have an implementation with Octokit, while GitHub collector use its own underlying implementation.
    /// </summary>
    public abstract class GitHubAppAuthenticationBase
    {
        protected string organization;
        protected readonly int appId;
        protected readonly string gitHubAppKeyVaultUri;
        protected readonly bool useInteractiveLogin;

        /// <summary>
        /// How long a JWT claim remains valid, in seconds.
        /// </summary>
        private const int JwtExpiry = 60 * 8; // 8 mins


        public GitHubAppAuthenticationBase(string organization, int appId, string gitHubAppKeyVaultUri, bool useInteractiveLogin)
        {
            this.organization = organization;
            this.appId = appId;
            this.gitHubAppKeyVaultUri = gitHubAppKeyVaultUri;
            this.useInteractiveLogin = useInteractiveLogin;
        }

        /// <summary>
        /// Main function to get the installation access token.
        /// </summary>
        protected async Task<string> GetAccessTokenAsync(string jwt)
        {
            if (organization == null)
            {
                throw new Exception("Organization must has value to get the installation access token.");
            }

            string installationId = await this.FindInstallationId().ConfigureAwait(false);
            return await this.ObtainPat(jwt, installationId).ConfigureAwait(false);
        }

        /// <summary>
        /// This method uses Azure Keyvault to manually create a JWT based on the JWT spec.
        /// https://tools.ietf.org/html/rfc7519
        /// </summary>
        /// <returns>A string with the JWT required to authenticate with GitHub.</returns>
        protected string CreateJwtBase(TokenCredential credential)
        {
            TokenCredential cred = credential ?? new InteractiveBrowserCredential();

            CryptographyClient client = new CryptographyClient(new Uri(this.gitHubAppKeyVaultUri), cred);
            string jwtHeader = @"{""alg"":""RS256"",""typ"":""JWT""}";

            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            long now = (long)(DateTime.UtcNow - epoch).TotalSeconds;

            string payload = @"{""iat"":" + now + @",""exp"":" + (now + JwtExpiry) + @",""iss"":" + this.appId + @"}";

            string encodedHeader = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(jwtHeader));
            string encodedPayload = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload));

            using (SHA256 sha256 = SHA256.Create())
            {
                string jwtSigningBuffer = $"{encodedHeader}.{encodedPayload}";
                byte[] digestBuffer = sha256.ComputeHash(Encoding.UTF8.GetBytes(jwtSigningBuffer));

                SignResult signingResponse = client.Sign(SignatureAlgorithm.RS256, digestBuffer);
                string signature = WebEncoders.Base64UrlEncode(signingResponse.Signature);

                return $"{encodedHeader}.{encodedPayload}.{signature}";
            }
        }

        protected abstract Task<string> FindInstallationId();

        protected abstract Task<string> ObtainPat(string jwt, string installationId);
    }
}
