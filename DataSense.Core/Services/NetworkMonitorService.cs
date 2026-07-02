using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DataSense.Core.Domain;
using DataSense.Core.Interfaces;

namespace DataSense.Core.Services
{
    public class NetworkMonitorService : IHostedService
    {
        private readonly INetworkInterfaceService _networkInterfaceService;
        private readonly IPacketCaptureService _packetCaptureService;
        private readonly IProcessConnectionMapper _processMapper;
        private readonly NetworkUsageAggregator _aggregator;
        private readonly ILogger<NetworkMonitorService> _logger;

        public NetworkMonitorService(
            INetworkInterfaceService networkInterfaceService,
            IPacketCaptureService packetCaptureService,
            IProcessConnectionMapper processMapper,
            NetworkUsageAggregator aggregator,
            ILogger<NetworkMonitorService> logger)
        {
            _networkInterfaceService = networkInterfaceService;
            _packetCaptureService = packetCaptureService;
            _processMapper = processMapper;
            _aggregator = aggregator;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Network Monitor Service...");
            
            var adapters = _networkInterfaceService.GetAvailableAdapters()
                .Where(a => !a.IsLoopback)
                .ToList();

            if (!adapters.Any())
            {
                _logger.LogWarning("No non-loopback network adapters found.");
                return Task.CompletedTask;
            }

            _packetCaptureService.OnPacketCaptured += PacketCaptureService_OnPacketCaptured;

            try
            {
                var adapterIds = adapters.Select(a => a.Id);
                _packetCaptureService.StartCapture(adapterIds);
                _logger.LogInformation("Packet capture started successfully on {Count} adapter(s).", adapters.Count);
            }
            catch (Exception ex)
            {
                // Capture can fail if Npcap is not installed, or if the user lacks the necessary
                // permissions to open raw sockets. Log the warning and continue — the app will
                // still launch and show historical data; live capture just won't work.
                _logger.LogWarning(ex,
                    "Packet capture could not be started. Live monitoring will be unavailable. " +
                    "Ensure Npcap is installed and the app has sufficient permissions.");
            }

            return Task.CompletedTask;
        }

        private void PacketCaptureService_OnPacketCaptured(object? sender, ParsedPacket packet)
        {
            int localPort = packet.IsUpload ? packet.SourcePort : packet.DestPort;
            int processId = _processMapper.GetProcessId(localPort, packet.Protocol);
            string processName = _processMapper.GetProcessName(processId);
            
            _aggregator.AddPacket(processName, packet.BytesLength, packet.IsUpload);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Network Monitor Service...");
            _packetCaptureService.StopCapture();
            _packetCaptureService.OnPacketCaptured -= PacketCaptureService_OnPacketCaptured;
            return Task.CompletedTask;
        }
    }
}
