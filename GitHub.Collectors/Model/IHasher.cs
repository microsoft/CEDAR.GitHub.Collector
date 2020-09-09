// Copyright (c) Microsoft Corporation. All rights reserved.

using Newtonsoft.Json.Linq;

namespace Microsoft.CloudMine.GitHub.Collectors.Model
{
    public interface IHasher
    {
        string ComputeSha256Hash(JObject jsonObject, Repository repository);
    }
}
