using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CloudMine.GitHub.Collectors.Web
{
    public class GitHubRateLimitException : Exception
    {
        private readonly TimeSpan hiddenTime;

        public GitHubRateLimitException(TimeSpan hiddenTime)
            : base("RateLimitRequeue")
        {
            this.hiddenTime = hiddenTime;
        }

        public TimeSpan getHiddenTime()
        {
            return this.hiddenTime;
        }
    }
}
