using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataSense.Core.Services
{
    public class IpInfo
    {
        public string Ip { get; set; } = "0.0.0.0";
        public string Org { get; set; } = "Offline / Unknown ISP";
        public string City { get; set; } = "Unknown City";
        public string Country { get; set; } = "Unknown Country";
    }

    public class SpeedTestProgress
    {
        public string Phase { get; set; } = "Idle"; // "Connecting", "Ping", "Download", "Upload", "Completed"
        public double CurrentSpeedMbps { get; set; }
        public double ProgressPercentage { get; set; }
        public double PingMs { get; set; }
        public double JitterMs { get; set; }
        public double DownloadMbps { get; set; }
        public double UploadMbps { get; set; }
        public string IpAddress { get; set; } = "0.0.0.0";
        public string IspName { get; set; } = "Finding ISP...";
        public string ServerLocation { get; set; } = "Finding Server...";
    }

    public class SpeedTestService
    {
        private readonly HttpClient _httpClient;

        public SpeedTestService()
        {
            _httpClient = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            });
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(45);
        }

        public async Task<IpInfo> GetIpInfoAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // We use ipapi.co to get details in a clean JSON format
                var response = await _httpClient.GetStringAsync("https://ipapi.co/json/", cancellationToken);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                return new IpInfo
                {
                    Ip = root.TryGetProperty("ip", out var ipProp) ? ipProp.GetString() ?? "Unknown IP" : "Unknown IP",
                    Org = root.TryGetProperty("org", out var orgProp) ? orgProp.GetString() ?? "Unknown ISP" : "Unknown ISP",
                    City = root.TryGetProperty("city", out var cityProp) ? cityProp.GetString() ?? "Unknown City" : "Unknown City",
                    Country = root.TryGetProperty("country_name", out var countryProp) ? countryProp.GetString() ?? "Unknown Country" : "Unknown Country"
                };
            }
            catch
            {
                return new IpInfo();
            }
        }

        public async Task RunTestAsync(
            Action<SpeedTestProgress> onProgress,
            CancellationToken cancellationToken = default)
        {
            var progress = new SpeedTestProgress();

            // 1. Fetch IP Info
            progress.Phase = "Connecting";
            onProgress(progress);

            try
            {
                var ipInfo = await GetIpInfoAsync(cancellationToken);
                progress.IpAddress = ipInfo.Ip;
                progress.IspName = ipInfo.Org;
                progress.ServerLocation = $"{ipInfo.City}, {ipInfo.Country}";
            }
            catch
            {
                progress.IpAddress = "Offline";
                progress.IspName = "No Connection";
                progress.ServerLocation = "Unavailable";
            }
            onProgress(progress);

            // 2. Ping Test
            if (cancellationToken.IsCancellationRequested) return;
            progress.Phase = "Ping";
            onProgress(progress);
            await RunPingTestAsync(progress, onProgress, cancellationToken);

            // 3. Download Speed Test
            if (cancellationToken.IsCancellationRequested) return;
            progress.Phase = "Download";
            onProgress(progress);
            await RunDownloadTestAsync(progress, onProgress, cancellationToken);

            // 4. Upload Speed Test
            if (cancellationToken.IsCancellationRequested) return;
            progress.Phase = "Upload";
            onProgress(progress);
            await RunUploadTestAsync(progress, onProgress, cancellationToken);

            // 5. Complete
            progress.Phase = "Completed";
            progress.ProgressPercentage = 100;
            progress.CurrentSpeedMbps = 0;
            onProgress(progress);
        }

        private async Task RunPingTestAsync(SpeedTestProgress progress, Action<SpeedTestProgress> onProgress, CancellationToken cancellationToken)
        {
            using var ping = new Ping();
            int count = 5;
            long totalRoundtrip = 0;
            long lastRoundtrip = -1;
            long sumAbsoluteDiff = 0;
            int successfulPings = 0;

            for (int i = 0; i < count; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    // Ping 1.1.1.1 (Cloudflare DNS)
                    var reply = await ping.SendPingAsync("1.1.1.1", 1000);
                    if (reply.Status == IPStatus.Success)
                    {
                        successfulPings++;
                        long rtt = reply.RoundtripTime;
                        totalRoundtrip += rtt;
                        if (lastRoundtrip != -1)
                        {
                            sumAbsoluteDiff += Math.Abs(rtt - lastRoundtrip);
                        }
                        lastRoundtrip = rtt;

                        progress.PingMs = (double)totalRoundtrip / successfulPings;
                        if (successfulPings > 1)
                        {
                            progress.JitterMs = (double)sumAbsoluteDiff / (successfulPings - 1);
                        }
                    }
                }
                catch
                {
                    // Ignore transient ping failure
                }

                progress.ProgressPercentage = ((double)(i + 1) / count) * 100;
                onProgress(progress);

                await Task.Delay(150, cancellationToken);
            }

            if (successfulPings == 0)
            {
                progress.PingMs = 0;
                progress.JitterMs = 0;
            }
        }

        private async Task RunDownloadTestAsync(SpeedTestProgress progress, Action<SpeedTestProgress> onProgress, CancellationToken cancellationToken)
        {
            // Download 10MB from Cloudflare Speed Test CDN
            string url = "https://speed.cloudflare.com/__down?bytes=10000000";
            var stopwatch = new Stopwatch();

            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 10000000L;
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

                byte[] buffer = new byte[32768]; // 32KB buffer
                long bytesReceived = 0;
                stopwatch.Start();

                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    bytesReceived += read;
                    double elapsedSec = stopwatch.Elapsed.TotalSeconds;

                    if (elapsedSec > 0)
                    {
                        double speedMbps = (bytesReceived * 8.0) / (elapsedSec * 1000000.0);
                        progress.CurrentSpeedMbps = speedMbps;
                        progress.DownloadMbps = speedMbps;
                        progress.ProgressPercentage = Math.Min(100.0, ((double)bytesReceived / totalBytes) * 100);
                        onProgress(progress);
                    }
                }
                stopwatch.Stop();

                if (stopwatch.Elapsed.TotalSeconds > 0)
                {
                    progress.DownloadMbps = (bytesReceived * 8.0) / (stopwatch.Elapsed.TotalSeconds * 1000000.0);
                }
            }
            catch (Exception ex)
            {
                progress.DownloadMbps = 0;
                try
                {
                    File.WriteAllText("C:\\Users\\dulmi\\Documents\\TestProject\\DataSense\\speedtest_error.log", $"[Download Error] {DateTime.Now}: {ex}\n");
                }
                catch {}
            }
        }

        private async Task RunUploadTestAsync(SpeedTestProgress progress, Action<SpeedTestProgress> onProgress, CancellationToken cancellationToken)
        {
            // Upload 8MB of data
            string url = "https://speed.cloudflare.com/__up";
            byte[] data = new byte[8 * 1024 * 1024]; // 8MB
            new Random().NextBytes(data);

            var stopwatch = new Stopwatch();

            try
            {
                var content = new ProgressByteArrayContent(data, (bytesWritten, totalBytes) =>
                {
                    double elapsedSec = stopwatch.Elapsed.TotalSeconds;
                    if (elapsedSec > 0)
                    {
                        double speedMbps = (bytesWritten * 8.0) / (elapsedSec * 1000000.0);
                        progress.CurrentSpeedMbps = speedMbps;
                        progress.UploadMbps = speedMbps;
                        progress.ProgressPercentage = Math.Min(100.0, ((double)bytesWritten / totalBytes) * 100);
                        onProgress(progress);
                    }
                });

                stopwatch.Start();
                using var response = await _httpClient.PostAsync(url, content, cancellationToken);
                response.EnsureSuccessStatusCode();
                stopwatch.Stop();

                if (stopwatch.Elapsed.TotalSeconds > 0)
                {
                    progress.UploadMbps = (data.Length * 8.0) / (stopwatch.Elapsed.TotalSeconds * 1000000.0);
                }
            }
            catch (Exception ex)
            {
                progress.UploadMbps = 0;
                try
                {
                    File.AppendAllText("C:\\Users\\dulmi\\Documents\\TestProject\\DataSense\\speedtest_error.log", $"[Upload Error] {DateTime.Now}: {ex}\n");
                }
                catch {}
            }
        }
    }

    public class ProgressByteArrayContent : HttpContent
    {
        private readonly byte[] _data;
        private readonly Action<long, long> _onProgress;

        public ProgressByteArrayContent(byte[] data, Action<long, long> onProgress)
        {
            _data = data;
            _onProgress = onProgress;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            int chunkSize = 32768; // 32KB chunk
            long bytesWritten = 0;
            int offset = 0;

            while (offset < _data.Length)
            {
                int count = Math.Min(chunkSize, _data.Length - offset);
                await stream.WriteAsync(_data, offset, count);
                offset += count;
                bytesWritten += count;
                _onProgress(bytesWritten, _data.Length);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _data.Length;
            return true;
        }
    }
}
