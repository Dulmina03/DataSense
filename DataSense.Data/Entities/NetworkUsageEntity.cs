using System;
using System.ComponentModel.DataAnnotations;

namespace DataSense.Data.Entities
{
    public class NetworkUsageEntity
    {
        [Key]
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string NetworkName { get; set; } = string.Empty;
        public long BytesDownloaded { get; set; }
        public long BytesUploaded { get; set; }
    }
}
