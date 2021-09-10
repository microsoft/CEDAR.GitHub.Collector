using Microsoft.CloudMine.Core.Collectors.Cache;
using Microsoft.CloudMine.Core.Collectors.Utility;

namespace Microsoft.CloudMine.GitHub.Collectors.Cache
{
    public class PointCollectorTableEntity : TableEntityWithContext
    {
        public string Url { get; set; }
        public PointCollectorTableEntity(string url)
        {
            this.Url = url;
            string urlHash = HashUtility.ComputeSha256(this.Url);
            this.PartitionKey = urlHash;
            this.RowKey = string.Empty;
            this.AddContext("Url", this.Url);
        }

        public PointCollectorTableEntity()
        {
        }
    }
}
