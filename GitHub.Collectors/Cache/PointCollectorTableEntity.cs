using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.GitHub.Collectors.Model;

namespace Microsoft.CloudMine.GitHub.Collectors.Cache
{
    public class PointCollectorTableEntity : TableEntityWithContext
    {
        public long OrganizationId { get; set; }
        public long RepositoryId { get; set; }
        public string OrganizationLogin { get; set; }
        public string RepositoryName { get; set; }
        public string PointType { get; set; }

        public PointCollectorTableEntity(PointCollectorInput input)
        {
            this.PartitionKey = $"{input.OrganizationId}_{input.RepositoryId}_{input.PointType}";
            this.RowKey = string.Empty;

            this.OrganizationId = input.OrganizationId;
            this.OrganizationLogin = input.OrganizationLogin;
            this.RepositoryId = input.RepositoryId;
            this.RepositoryName = input.RepositoryName;
            this.PointType = input.PointType.ToString();

            this.AddContext("OrganizationId", this.OrganizationId.ToString());
            this.AddContext("OrganizationLogin", this.OrganizationLogin);
            this.AddContext("RepositoryId", this.RepositoryId.ToString());
            this.AddContext("RepositoryName", this.RepositoryName);
            this.AddContext("PointType", this.PointType);
        }

    }
}
