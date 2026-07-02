using System;
using System.ComponentModel.DataAnnotations;

namespace DataSense.Data.Entities
{
    public class DailyUsageEntity
    {
        [Key]
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public long BytesDownloaded { get; set; }
        public long BytesUploaded { get; set; }
    }
}
