// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.CloudMine.GitHub.Collectors.Model;
using System.Threading.Tasks;

namespace Microsoft.CloudMine.GitHub.Collectors.Cache
{
    public interface IEventsBookkeeper
    {
        Task InitializeAsync();
        Task SignalCountAsync(Repository eventStats);
        Task<int> IncrementCountAsync(Repository repositoryDetails);
        Task ResetCountAsync(Repository repositoryDetails);
    }
}
