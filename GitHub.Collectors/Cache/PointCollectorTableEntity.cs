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
        public string RecordType { get; set; }

        public PointCollectorTableEntity(PointCollectorInput input)
        {
            Repository repository = input.getRepository();

            this.OrganizationId = repository.OrganizationId;
            this.OrganizationLogin = repository.OrganizationLogin;
            this.RepositoryId = repository.RepositoryId;
            this.RepositoryName = repository.RepositoryName;
            this.RecordType = input.RecordType;
            this.PartitionKey = $"{repository.OrganizationId}_{repository.RepositoryId}_{input.RecordType}";
            this.RowKey = string.Empty;

            this.AddContext("OrganizationId", this.OrganizationId.ToString());
            this.AddContext("OrganizationLogin", this.OrganizationLogin);
            this.AddContext("RepositoryId", this.RepositoryId.ToString());
            this.AddContext("RepositoryName", this.RepositoryName);
            this.AddContext("RecordType", this.RecordType);
        }

    }
}
