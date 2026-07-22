namespace DataSense.Core.Domain
{
    public class NetworkAdapterInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public bool IsLoopback { get; set; }
        public string NetworkName { get; set; } = string.Empty;
    }
}
