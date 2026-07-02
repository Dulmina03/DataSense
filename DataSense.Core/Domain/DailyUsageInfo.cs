using System;

namespace DataSense.Core.Domain
{
    public class DailyUsageInfo
    {
        public DateTime Date { get; set; }
        public long BytesDownloaded { get; set; }
        public long BytesUploaded { get; set; }
        public long TotalBytes => BytesDownloaded + BytesUploaded;
    }
}
