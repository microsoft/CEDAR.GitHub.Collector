using Microsoft.CloudMine.Core.Collectors.Cache;
using System.Web;

namespace Microsoft.CloudMine.GitHub.Collectors.Cache
{
    public class PointCollectorTableEntity : TableEntityWithContext
    {
        public string Url { get; set; }
        public PointCollectorTableEntity(string url)
        {
            this.Url = url;
            string escapedUrl = HttpUtility.UrlEncode(url);
            this.PartitionKey = escapedUrl;
            this.RowKey = string.Empty;
        }

        public PointCollectorTableEntity()
        { }
    }
}
