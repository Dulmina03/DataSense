namespace DataSense.Core.Domain
{
    public class NetworkConnectionDetails
    {
        public string NetworkType { get; set; } = "Unknown";
        public string SignalStrength { get; set; } = "—";
        public string IpAddress { get; set; } = "—";
        public string DnsServer { get; set; } = "—";
        public string Gateway { get; set; } = "—";
    }
}
