// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json.Linq;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public interface IHasher
    {
        string ComputeSha256Hash(JObject jsonObject, Repository repository);
    }
}
