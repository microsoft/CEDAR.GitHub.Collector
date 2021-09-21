using System;

namespace Microsoft.CloudMine.GitHub.Collectors.Web
{
    public class GitHubRateLimitException : Exception
    {
        private readonly TimeSpan hiddenTime;

        public GitHubRateLimitException(TimeSpan hiddenTime)
            : base("RateLimitRequeue")
        {

            this.hiddenTime = hiddenTime > TimeSpan.FromMilliseconds(0) ? hiddenTime : TimeSpan.FromMilliseconds(0);
        }

        public TimeSpan GetHiddenTime()
        {
            return this.hiddenTime;
        }
    }
}
