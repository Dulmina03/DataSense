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

        public event EventHandler<ParsedPacket>? OnPacketCaptured;

        public PcapPacketCaptureService(INetworkInterfaceService networkInterfaceService)
        {
            _localIps = new HashSet<string>(networkInterfaceService.GetLocalIpAddresses());
        }

        public void StartCapture(IEnumerable<string> adapterIds)
        {
            var devices = CaptureDeviceList.Instance;
            foreach (var id in adapterIds)
            {
                var dev = devices.FirstOrDefault(d => d.Name == id);
                if (dev != null)
                {
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

                    var parsedInfo = new ParsedPacket
                    {
                        SourceIp = ipPacket.SourceAddress,
                        SourcePort = srcPort,
                        DestIp = ipPacket.DestinationAddress,
                        DestPort = dstPort,
                        Protocol = protocol,
                        BytesLength = rawPacket.Data.Length,
                        IsUpload = isUpload
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
