using System.Collections.Generic;
using System.Net.NetworkInformation;
using DataSense.Core.Domain;
using DataSense.Core.Interfaces;
using SharpPcap;

namespace DataSense.Infrastructure.Network
{
    public class PcapNetworkInterfaceService : INetworkInterfaceService
    {
        public IEnumerable<NetworkAdapterInfo> GetAvailableAdapters()
        {
            var devices = CaptureDeviceList.Instance;
            var list = new List<NetworkAdapterInfo>();
            
            foreach (var dev in devices)
            {
                list.Add(new NetworkAdapterInfo
                {
                    Id = dev.Name,
                    Name = dev.Description,
                    Description = dev.Description,
                    MacAddress = dev.MacAddress?.ToString() ?? "",
                    IsLoopback = dev.Description.Contains("Loopback")
                });
            }
            return list;
        }

        public IEnumerable<string> GetLocalIpAddresses()
        {
            var ipList = new List<string>();
            foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                var ipProps = netInterface.GetIPProperties();
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ||
                        addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    {
                        ipList.Add(addr.Address.ToString());
                    }
                }
            }
            return ipList;
        }
    }
}
