using System;
using System.ComponentModel.DataAnnotations;

namespace DataSense.Data.Entities
{
    public class ProcessUsageEntity
    {
        [Key]
        public int Id { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public long BytesDownloaded { get; set; }
        public long BytesUploaded { get; set; }
    }
}
