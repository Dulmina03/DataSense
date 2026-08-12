using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace DataSense.Core.Services
{
    /// <summary>
    /// Background singleton that polls Windows every 2 seconds for the active
    /// network name using the Windows Native Wi-Fi API (wlanapi.dll).
    /// Reads the exact SSID the kernel reports — same value shown in the taskbar flyout.
    /// Falls back to netsh only if the Wlan API fails.
    /// </summary>
    public static class SsidMonitorService
    {
        // ── State ────────────────────────────────────────────────────────────
        private static string _currentNetworkName = "Ethernet";
        private static readonly object _lock = new object();
        private static Timer? _timer;
        private static bool _started = false;

        /// <summary>
        /// The actual connected network name: real SSID (e.g. "Galaxy A04s 53E7")
        /// or "Ethernet". Thread-safe. Never "Wi-Fi" or "Connected Network".
        /// </summary>
        public static string CurrentNetworkName
        {
            get { lock (_lock) { return _currentNetworkName; } }
        }

        /// <summary>Fired when the active network changes (oldName, newName).</summary>
        public static event Action<string, string>? NetworkChanged;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        public static void Start()
        {
            lock (_lock)
            {
                if (_started) return;
                _started = true;

                // Immediate read so value is correct before any packet arrives
                string initial = DetectNetworkName();
                _currentNetworkName = initial;

                WriteDiagnostics("SsidMonitorService started. Initial network: " + initial);

                _timer = new Timer(_ => Poll(), null,
                    dueTime: TimeSpan.FromSeconds(2),
                    period: TimeSpan.FromSeconds(2));
            }
        }

        public static void Stop()
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
                _started = false;
            }
        }

        // ── Polling ───────────────────────────────────────────────────────────
        private static void Poll()
        {
            try
            {
                string detected = DetectNetworkName();
                string old;
                bool changed = false;

                lock (_lock)
                {
                    old = _currentNetworkName;
                    if (!string.Equals(detected, old, StringComparison.OrdinalIgnoreCase))
                    {
                        _currentNetworkName = detected;
                        changed = true;
                    }
                }

                if (changed)
                {
                    WriteDiagnostics($"Network changed: '{old}' → '{detected}'");
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try { NetworkChanged?.Invoke(old, detected); }
                        catch { }
                    });
                }
            }
            catch { }
        }

        // ── Detection pipeline ────────────────────────────────────────────────
        internal static string DetectNetworkName()
        {
            // 1. Windows Native Wi-Fi API — reads from kernel, works regardless of elevation
            string? ssid = ReadWifiSsidViaWlanApi();
            if (!string.IsNullOrEmpty(ssid))
                return ssid;

            // 2. netsh fallback
            ssid = ReadWifiSsidViaNetsh();
            if (!string.IsNullOrEmpty(ssid))
                return ssid;

            // 3. Any active wired interface with a default gateway
            try
            {
                bool hasWired = NetworkInterface.GetAllNetworkInterfaces()
                    .Any(n => n.OperationalStatus == OperationalStatus.Up
                              && !IsVirtual(n.Name, n.Description)
                              && n.GetIPProperties().GatewayAddresses
                                  .Any(g => g.Address.AddressFamily ==
                                            System.Net.Sockets.AddressFamily.InterNetwork));
                if (hasWired) return "Ethernet";
            }
            catch { }

            return "Ethernet";
        }

        /// <summary>One-shot read for callers that need an immediate value.</summary>
        public static string? ReadWifiSsid()
        {
            string? s = ReadWifiSsidViaWlanApi();
            if (!string.IsNullOrEmpty(s)) return s;
            return ReadWifiSsidViaNetsh();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Windows Native Wi-Fi API  (wlanapi.dll)
        //
        //  WLAN_CONNECTION_ATTRIBUTES memory layout (offsets in bytes):
        //    [  0]  isState              DWORD  (4)
        //    [  4]  wlanConnectionMode   DWORD  (4)
        //    [  8]  strProfileName       WCHAR[256] = 512 bytes
        //    [520]  wlanAssociationAttributes:
        //             dot11Ssid.uSSIDLength  DWORD (4)   ← offset 520
        //             dot11Ssid.ucSSID[32]   BYTE[32]    ← offset 524
        // ══════════════════════════════════════════════════════════════════════
        private const int SSID_LEN_OFFSET  = 520;
        private const int SSID_DATA_OFFSET = 524;
        private const int MIN_DATA_SIZE    = 556;   // 524 + 32
        private const int WLAN_INTERFACE_INFO_SIZE = 532; // GUID(16)+WCHAR[256](512)+DWORD(4)

        private static string? ReadWifiSsidViaWlanApi()
        {
            IntPtr client = IntPtr.Zero;
            IntPtr ifList = IntPtr.Zero;

            try
            {
                // Open WLAN client handle (API version 2 = Vista+)
                int hr = WlanOpenHandle(2, IntPtr.Zero, out _, out client);
                if (hr != 0 || client == IntPtr.Zero)
                {
                    WriteDiagnostics($"WlanOpenHandle failed: 0x{hr:X8}");
                    return null;
                }

                // Enumerate WLAN interfaces
                hr = WlanEnumInterfaces(client, IntPtr.Zero, out ifList);
                if (hr != 0 || ifList == IntPtr.Zero)
                {
                    WriteDiagnostics($"WlanEnumInterfaces failed: 0x{hr:X8}");
                    return null;
                }

                // WLAN_INTERFACE_INFO_LIST: dwNumberOfItems[4] + dwIndex[4] + items[]
                int count = Marshal.ReadInt32(ifList, 0);
                WriteDiagnostics($"WlanApi: found {count} WLAN interface(s)");

                IntPtr firstItem = IntPtr.Add(ifList, 8);

                for (int i = 0; i < count; i++)
                {
                    IntPtr itemPtr = IntPtr.Add(firstItem, i * WLAN_INTERFACE_INFO_SIZE);

                    // Read the interface GUID (first 16 bytes)
                    byte[] guidBytes = new byte[16];
                    Marshal.Copy(itemPtr, guidBytes, 0, 16);
                    Guid guid = new Guid(guidBytes);

                    // Read the interface description (WCHAR[256] at offset 16)
                    string desc = Marshal.PtrToStringUni(IntPtr.Add(itemPtr, 16), 256)?.TrimEnd('\0') ?? "";

                    // Read connection state (DWORD at offset 16+512=528)
                    int ifState = Marshal.ReadInt32(itemPtr, 528);
                    WriteDiagnostics($"  Interface[{i}]: '{desc}' state={ifState} guid={guid}");

                    // Query current connection
                    IntPtr data = IntPtr.Zero;
                    try
                    {
                        hr = WlanQueryInterface(client, ref guid,
                            7 /* wlan_intf_opcode_current_connection */,
                            IntPtr.Zero, out uint dataSize, out data, IntPtr.Zero);

                        WriteDiagnostics($"    WlanQueryInterface: hr=0x{hr:X8} dataSize={dataSize}");

                        if (hr == 0 && data != IntPtr.Zero && dataSize >= MIN_DATA_SIZE)
                        {
                            // Read isState from WLAN_CONNECTION_ATTRIBUTES
                            int connState = Marshal.ReadInt32(data, 0);
                            WriteDiagnostics($"    connState={connState}");

                            if (connState == 1) // wlan_interface_state_connected
                            {
                                int ssidLen = Marshal.ReadInt32(data, SSID_LEN_OFFSET);
                                WriteDiagnostics($"    ssidLen={ssidLen}");

                                if (ssidLen > 0 && ssidLen <= 32)
                                {
                                    byte[] ssidBytes = new byte[ssidLen];
                                    Marshal.Copy(IntPtr.Add(data, SSID_DATA_OFFSET), ssidBytes, 0, ssidLen);
                                    string ssid = Encoding.UTF8.GetString(ssidBytes);
                                    WriteDiagnostics($"    SSID (WlanApi) = '{ssid}'");
                                    if (!string.IsNullOrEmpty(ssid))
                                        return ssid;
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (data != IntPtr.Zero) WlanFreeMemory(data);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteDiagnostics("ReadWifiSsidViaWlanApi exception: " + ex.Message);
            }
            finally
            {
                if (ifList   != IntPtr.Zero) WlanFreeMemory(ifList);
                if (client   != IntPtr.Zero) WlanCloseHandle(client, IntPtr.Zero);
            }

            return null;
        }

        // ── netsh fallback ────────────────────────────────────────────────────
        private static string? ReadWifiSsidViaNetsh()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("netsh", "wlan show interfaces")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null) return null;

                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);

                WriteDiagnostics("netsh output (first 500 chars): " +
                    (output.Length > 500 ? output.Substring(0, 500) : output));

                foreach (var raw in output.Split(new[] { '\r', '\n' },
                             StringSplitOptions.RemoveEmptyEntries))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("SSID", StringComparison.OrdinalIgnoreCase)
                        && !line.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase)
                        && !line.StartsWith("AP BSSID", StringComparison.OrdinalIgnoreCase))
                    {
                        int colon = line.IndexOf(':');
                        if (colon >= 0 && colon < line.Length - 1)
                        {
                            string ssid = line.Substring(colon + 1).Trim();
                            WriteDiagnostics("netsh SSID = '" + ssid + "'");
                            if (!string.IsNullOrEmpty(ssid))
                                return ssid;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteDiagnostics("ReadWifiSsidViaNetsh exception: " + ex.Message);
            }

            return null;
        }

        // ── Diagnostics ───────────────────────────────────────────────────────
        private static readonly string _diagPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DataSense", "ssid_diagnostics.log");

        private static void WriteDiagnostics(string message)
        {
            try
            {
                var dir = Path.GetDirectoryName(_diagPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(_diagPath,
                    $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        // Also write NetworkInterface diagnostics on first start
        public static void WriteNetworkAdapterDiagnostics()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== NetworkInterface Adapter Diagnostics ===");
                foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
                {
                    sb.AppendLine($"  FriendlyName : {n.Name}");
                    sb.AppendLine($"  Description  : {n.Description}");
                    sb.AppendLine($"  Type         : {n.NetworkInterfaceType}");
                    sb.AppendLine($"  Status       : {n.OperationalStatus}");
                    sb.AppendLine($"  IsVirtual    : {IsVirtual(n.Name, n.Description)}");
                    try
                    {
                        var gw = n.GetIPProperties().GatewayAddresses
                            .FirstOrDefault(g => g.Address.AddressFamily ==
                                                 System.Net.Sockets.AddressFamily.InterNetwork);
                        sb.AppendLine($"  Gateway      : {gw?.Address?.ToString() ?? "—"}");
                    }
                    catch { sb.AppendLine("  Gateway      : (error)"); }
                    sb.AppendLine();
                }
                WriteDiagnostics(sb.ToString());
            }
            catch { }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static bool IsVirtual(string name, string description)
        {
            string combined = ((name ?? "") + " " + (description ?? "")).ToLowerInvariant();
            return combined.Contains("virtualbox") || combined.Contains("vmware")   ||
                   combined.Contains("hyper-v")    || combined.Contains("vethernet")||
                   combined.Contains("tailscale")  || combined.Contains("wireguard")||
                   combined.Contains("openvpn")    || combined.Contains("bluetooth")||
                   combined.Contains("loopback")   || combined.Contains("tap-")    ||
                   combined.Contains("wsl")         || combined.Contains("docker")  ||
                   combined.Contains("npcap")       || combined.Contains("pcap")    ||
                   combined.Contains("pseudo");
        }

        // ── P/Invoke: wlanapi.dll ─────────────────────────────────────────────
        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern int WlanOpenHandle(
            uint dwClientVersion,
            IntPtr pReserved,
            out uint pdwNegotiatedVersion,
            out IntPtr phClientHandle);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern int WlanCloseHandle(
            IntPtr hClientHandle,
            IntPtr pReserved);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern int WlanEnumInterfaces(
            IntPtr hClientHandle,
            IntPtr pReserved,
            out IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll", SetLastError = true)]
        private static extern int WlanQueryInterface(
            IntPtr hClientHandle,
            ref Guid pInterfaceGuid,
            uint OpCode,
            IntPtr pReserved,
            out uint pdwDataSize,
            out IntPtr ppData,
            IntPtr pWlanOpcodeValueType);

        [DllImport("wlanapi.dll")]
        private static extern void WlanFreeMemory(IntPtr pMemory);
    }
}
