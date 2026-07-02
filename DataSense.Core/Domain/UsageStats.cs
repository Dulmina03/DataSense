using System;

namespace DataSense.Core.Domain
{
    public class UsageStats
    {
        public long BytesDownloaded { get; set; }
        public long BytesUploaded { get; set; }

        public void Add(long downloaded, long uploaded)
        {
            BytesDownloaded += downloaded;
            BytesUploaded += uploaded;
        }
    }

    public class ProcessUsageInfo
    {
        public string ProcessName { get; set; } = string.Empty;
        public UsageStats Stats { get; set; } = new UsageStats();
    }
}
