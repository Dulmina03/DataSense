using System.Net;

namespace DataSense.Core.Domain
{
    public class ParsedPacket
    {
        public IPAddress? SourceIp { get; set; }
        public int SourcePort { get; set; }
        public IPAddress? DestIp { get; set; }
        public int DestPort { get; set; }
        public string Protocol { get; set; } = string.Empty;
        public long BytesLength { get; set; }
        public bool IsUpload { get; set; } // true if sending from local IP
    }
}
