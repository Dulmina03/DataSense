using System;
using System.Collections.Generic;
using System.Linq;
using DataSense.Core.Domain;
using DataSense.Core.Interfaces;
using SharpPcap;
using PacketDotNet;

namespace DataSense.Infrastructure.Network
{
    public class PcapPacketCaptureService : IPacketCaptureService
    {
        private readonly List<ILiveDevice> _activeDevices = new List<ILiveDevice>();
        private readonly HashSet<string> _localIps;
        private readonly INetworkInterfaceService _networkInterfaceService;
        private readonly Dictionary<string, string> _deviceNetworkNames = new Dictionary<string, string>();
        private readonly Dictionary<string, bool> _deviceIsWifi = new Dictionary<string, bool>();

        public event EventHandler<ParsedPacket>? OnPacketCaptured;

        public PcapPacketCaptureService(INetworkInterfaceService networkInterfaceService)
        {
            _networkInterfaceService = networkInterfaceService;
            _localIps = new HashSet<string>(networkInterfaceService.GetLocalIpAddresses());
        }

        public void StartCapture(IEnumerable<string> adapterIds)
        {
            var adapters = _networkInterfaceService.GetAvailableAdapters().ToDictionary(a => a.Id, a => a.NetworkName);
            var devices = CaptureDeviceList.Instance;
            foreach (var id in adapterIds)
            {
                var dev = devices.FirstOrDefault(d => d.Name == id);
                if (dev != null)
                {
                    bool isWifi = dev.Description.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                                  dev.Description.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                                  dev.Description.Contains("802.11", StringComparison.OrdinalIgnoreCase) ||
                                  dev.Description.Contains("WLAN", StringComparison.OrdinalIgnoreCase);
                    _deviceIsWifi[id] = isWifi;

                    if (adapters.TryGetValue(id, out var netName))
                    {
                        _deviceNetworkNames[id] = netName;
                    }
                    else
                    {
                        _deviceNetworkNames[id] = isWifi ? "Wi-Fi" : "Connected Network";
                    }

                    dev.OnPacketArrival += Device_OnPacketArrival;
                    // Promiscuous mode to capture all packets on the interface
                    dev.Open(DeviceModes.Promiscuous, 1000);
                    dev.StartCapture();
                    _activeDevices.Add(dev);
                }
            }
        }

        private void Device_OnPacketArrival(object sender, PacketCapture e)
        {
            var rawPacket = e.GetPacket();
            var parsedPacket = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
            
            var ipPacket = parsedPacket.Extract<IPPacket>();
            if (ipPacket != null)
            {
                var tcpPacket = parsedPacket.Extract<TcpPacket>();
                var udpPacket = parsedPacket.Extract<UdpPacket>();

                if (tcpPacket != null || udpPacket != null)
                {
                    int srcPort = tcpPacket != null ? tcpPacket.SourcePort : udpPacket!.SourcePort;
                    int dstPort = tcpPacket != null ? tcpPacket.DestinationPort : udpPacket!.DestinationPort;
                    string protocol = tcpPacket != null ? "TCP" : "UDP";

                    string srcIpStr = ipPacket.SourceAddress.ToString();
                    bool isUpload = _localIps.Contains(srcIpStr);

                    string netName = "Connected Network";
                    if (sender is ILiveDevice liveDev)
                    {
                        if (_deviceIsWifi.TryGetValue(liveDev.Name, out bool isWifi) && isWifi)
                        {
                            var activeSsid = DataSense.Core.Services.NetworkUsageAggregator.GetActiveWifiSsid();
                            netName = !string.IsNullOrEmpty(activeSsid) ? activeSsid : "Wi-Fi";
                        }
                        else if (_deviceNetworkNames.TryGetValue(liveDev.Name, out var name))
                        {
                            netName = name;
                        }
                    }

                    var parsedInfo = new ParsedPacket
                    {
                        SourceIp = ipPacket.SourceAddress,
                        SourcePort = srcPort,
                        DestIp = ipPacket.DestinationAddress,
                        DestPort = dstPort,
                        Protocol = protocol,
                        BytesLength = rawPacket.Data.Length,
                        IsUpload = isUpload,
                        NetworkName = netName
                    };

                    OnPacketCaptured?.Invoke(this, parsedInfo);
                }
            }
        }

        public void StopCapture()
        {
            foreach (var dev in _activeDevices)
            {
                try { dev.StopCapture(); } catch { }
                try { dev.Close(); } catch { }
                dev.OnPacketArrival -= Device_OnPacketArrival;
            }
            _activeDevices.Clear();
        }

        public void Dispose()
        {
            StopCapture();
        }
    }
}
