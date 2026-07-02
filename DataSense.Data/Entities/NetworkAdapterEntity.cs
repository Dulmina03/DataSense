using System.ComponentModel.DataAnnotations;

namespace DataSense.Data.Entities
{
    public class NetworkAdapterEntity
    {
        [Key]
        public string Id { get; set; } = string.Empty; // e.g. GUID string
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
    }
}
