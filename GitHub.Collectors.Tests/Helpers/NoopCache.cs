// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.Core.Collectors.Cache;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Tests.Helpers
{
    public class NoopCache<T> : ICache<T> where T : TableEntityWithContext
    {
        public Task CacheAsync(T tableEntity)
        {
            // Assume success.
            return Task.CompletedTask;
        }

        public Task<bool> CacheAtomicAsync(T currentTableEntity, T newTableEntity)
        {
            // Assume success.
            return Task.FromResult(true);
        }

        public Task<bool> ExistsAsync(T tableEntity)
        {
            // Assume failure.
            return Task.FromResult(false);
        }

        public Task InitializeAsync()
        {
            // Assume success.
            return Task.CompletedTask;
        }

        public Task<T> RetrieveAsync(T tableEntity)
        {
            // Assume failure.
            return Task.FromResult((T)null);
        }
    }
}
