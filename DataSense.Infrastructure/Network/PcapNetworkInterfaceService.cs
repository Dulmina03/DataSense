using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
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
                    IsLoopback = dev.Description.Contains("Loopback"),
                    NetworkName = GetNetworkProfileName(dev.Name, dev.Description)
                });
            }
            return list;
        }

        public static string? GetActiveWifiSsid()
        {
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
                        if (line.StartsWith("SSID", StringComparison.OrdinalIgnoreCase) && 
                           !line.StartsWith("AP BSSID", StringComparison.OrdinalIgnoreCase) &&
                           !line.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase))
                        {
                            int colonIdx = line.IndexOf(':');
                            if (colonIdx >= 0 && colonIdx < line.Length - 1)
                            {
                                string ssid = line.Substring(colonIdx + 1).Trim();
                                if (!string.IsNullOrEmpty(ssid))
                                {
                                    return ssid;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private string GetNetworkProfileName(string deviceName, string description)
        {
            bool isWireless = description.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                               description.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                               description.Contains("802.11", StringComparison.OrdinalIgnoreCase) ||
                               description.Contains("WLAN", StringComparison.OrdinalIgnoreCase);

            string? wifiSsid = isWireless ? GetActiveWifiSsid() : null;
            if (!string.IsNullOrEmpty(wifiSsid))
            {
                return wifiSsid;
            }

            try
            {
                string guidStr = "";
                int idx = deviceName.IndexOf('{');
                if (idx >= 0)
                {
                    int endIdx = deviceName.IndexOf('}', idx);
                    if (endIdx > idx)
                    {
                        guidStr = deviceName.Substring(idx, endIdx - idx + 1);
                    }
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
                                {
                                    return name;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            try
            {
                string guidStr = "";
                int idx = deviceName.IndexOf('{');
                if (idx >= 0)
                {
                    int endIdx = deviceName.IndexOf('}', idx);
                    if (endIdx > idx)
                    {
                        guidStr = deviceName.Substring(idx + 1, endIdx - idx - 1);
                    }
                }
                if (!string.IsNullOrEmpty(guidStr))
                {
                    foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (netInterface.Id.Equals(guidStr, StringComparison.OrdinalIgnoreCase))
                        {
                            if (netInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                            {
                                var ssid = GetActiveWifiSsid();
                                if (!string.IsNullOrEmpty(ssid)) return ssid;
                            }
                            return netInterface.Name;
                        }
                    }
                }
            }
            catch { }

            // Final fallback
            var activeSsid = GetActiveWifiSsid();
            if (!string.IsNullOrEmpty(activeSsid))
            {
                return activeSsid;
            }

            return "Connected Network";
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
