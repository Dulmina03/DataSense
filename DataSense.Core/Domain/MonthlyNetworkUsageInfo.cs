namespace DataSense.Core.Domain
{
    public class MonthlyNetworkUsageInfo
    {
        public string NetworkName { get; set; } = string.Empty;
        public long BytesDownloaded { get; set; }
        public long BytesUploaded { get; set; }
        public long TotalBytes => BytesDownloaded + BytesUploaded;
    }
}
