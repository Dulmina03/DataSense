using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using DataSense.Core.Domain;
using DataSense.Core.Interfaces;
using DataSense.Core.Services;
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
                    IsLoopback = dev.Description.Contains("Loopback"),
                    NetworkName = GetNetworkProfileName(dev.Name, dev.Description)
                });
            }
            return list;
        }


        private string GetNetworkProfileName(string deviceName, string description)
        {
            bool isWireless = description.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                               description.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                               description.Contains("802.11", StringComparison.OrdinalIgnoreCase) ||
                               description.Contains("WLAN", StringComparison.OrdinalIgnoreCase);

            // For wireless adapters, always return the cached SSID from SsidMonitorService
            // (which already did the netsh call in the background).
            if (isWireless)
                return DataSense.Core.Services.SsidMonitorService.CurrentNetworkName;

            // For Ethernet-type adapters: try NLM COM API for friendly profile name
            try
            {
                string guidStr = "";
                int idx = deviceName.IndexOf('{');
                if (idx >= 0)
                {
                    int endIdx = deviceName.IndexOf('}', idx);
                    if (endIdx > idx)
                        guidStr = deviceName.Substring(idx, endIdx - idx + 1);
                }

                if (!string.IsNullOrEmpty(guidStr))
                {
                    var nlmType = Type.GetTypeFromCLSID(new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B"));
                    if (nlmType != null)
                    {
                        dynamic manager = Activator.CreateInstance(nlmType)!;
                        var connections = manager.GetNetworkConnections();
                        foreach (dynamic conn in connections)
                        {
                            Guid id = conn.GetAdapterId();
                            if (id.ToString("B").Equals(guidStr, StringComparison.OrdinalIgnoreCase) ||
                                id.ToString("D").Equals(guidStr, StringComparison.OrdinalIgnoreCase))
                            {
                                var network = conn.GetNetwork();
                                string name = network.GetName();
                                if (!string.IsNullOrWhiteSpace(name) && !name.Equals("Unknown Network", StringComparison.OrdinalIgnoreCase))
                                    return "Ethernet";  // Ethernet connection — just label it Ethernet
                            }
                        }
                    }
                }
            }
            catch { }

            return DataSense.Core.Services.SsidMonitorService.CurrentNetworkName;
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

        public NetworkConnectionDetails GetConnectionDetails()
        {
            var details = new NetworkConnectionDetails();

            try
            {
                // Find best active non-loopback interface
                var activeInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                                n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .OrderByDescending(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 2 :
                                           n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 1 : 0)
                    .FirstOrDefault();

                if (activeInterface != null)
                {
                    // Network Type
                    if (activeInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                        details.NetworkType = "Wi-Fi";
                    else if (activeInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                        details.NetworkType = "Ethernet";
                    else
                        details.NetworkType = activeInterface.NetworkInterfaceType.ToString();

                    var ipProps = activeInterface.GetIPProperties();

                    // IPv4 address
                    var ipv4 = ipProps.UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    details.IpAddress = ipv4?.Address.ToString() ?? "—";

                    // Gateway
                    var gw = ipProps.GatewayAddresses
                        .FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    details.Gateway = gw?.Address.ToString() ?? "—";

                    // DNS
                    var dns = ipProps.DnsAddresses
                        .FirstOrDefault(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    details.DnsServer = dns?.ToString() ?? "—";
                }
            }
            catch { }

            // Signal Strength (Wi-Fi only via netsh)
            try
            {
                var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(2000);
                    foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var line = rawLine.Trim();
                        if (line.StartsWith("Signal", StringComparison.OrdinalIgnoreCase))
                        {
                            int colonIdx = line.IndexOf(':');
                            if (colonIdx >= 0)
                                details.SignalStrength = line.Substring(colonIdx + 1).Trim();
                        }
                    }
                }
            }
            catch { }

            return details;
        }
    }
}
