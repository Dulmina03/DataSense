using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DataSense.Core.Interfaces;
using DataSense.Infrastructure.Network.IpHelper;
using Microsoft.Extensions.Logging;

namespace DataSense.Infrastructure.Network
{
    public class WindowsProcessConnectionMapper : IProcessConnectionMapper, IDisposable
    {
        private readonly ConcurrentDictionary<string, int> _portProcessCache = new ConcurrentDictionary<string, int>();
        private readonly ConcurrentDictionary<int, string> _processNameCache = new ConcurrentDictionary<int, string>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ILogger<WindowsProcessConnectionMapper> _logger;

        public WindowsProcessConnectionMapper(ILogger<WindowsProcessConnectionMapper> logger)
        {
            _logger = logger;
            Task.Run(() => RefreshLoop(_cts.Token));
        }

        public int GetProcessId(int localPort, string protocol)
        {
            string key = $"{protocol}:{localPort}";
            if (_portProcessCache.TryGetValue(key, out int pid))
            {
                return pid;
            }
            return 0; // Unknown
        }

        public string GetProcessName(int processId)
        {
            if (processId == 0) return "System";
            if (_processNameCache.TryGetValue(processId, out string name))
                return name;

            try
            {
                var process = Process.GetProcessById(processId);
                _processNameCache[processId] = process.ProcessName;
                return process.ProcessName;
            }
            catch
            {
                return "Unknown";
            }
        }

        private async Task RefreshLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    RefreshConnections();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing connections");
                }
                await Task.Delay(2000, token);
            }
        }

        private void RefreshConnections()
        {
            var tcpConnections = IpHelperApi.GetAllTcpConnections();
            foreach (var conn in tcpConnections)
            {
                _portProcessCache[$"TCP:{conn.LocalPort}"] = conn.ProcessId;
            }

            var udpConnections = IpHelperApi.GetAllUdpConnections();
            foreach (var conn in udpConnections)
            {
                _portProcessCache[$"UDP:{conn.LocalPort}"] = conn.ProcessId;
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
        }
    }
}
