using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DataSense.Core.Domain;
using DataSense.Core.Repositories;

namespace DataSense.Core.Services
{
    /// <summary>
    /// Aggregates live network packet stats, fires speed updates, and flushes to DB periodically.
    /// Also maintains a per-minute bucket log so callers can query traffic for any time-of-day range.
    /// </summary>
    public class NetworkUsageAggregator : IHostedService
    {
        private ConcurrentDictionary<string, UsageStats> _processStats = new ConcurrentDictionary<string, UsageStats>();
        private long _totalDownloaded = 0;
        private long _totalUploaded = 0;

        private long _bytesDownloadedThisSecond = 0;
        private long _bytesUploadedThisSecond = 0;

        public long CurrentDownloadSpeedBps { get; private set; }
        public long CurrentUploadSpeedBps { get; private set; }
        public event Action<long, long>? SpeedUpdated;

        // Per-minute traffic buckets: key = minute-truncated local DateTime
        // Kept for up to 25 hours; cleaned up hourly.
        private readonly ConcurrentDictionary<DateTime, (long Downloaded, long Uploaded)> _minuteBuckets
            = new ConcurrentDictionary<DateTime, (long Downloaded, long Uploaded)>();

        // Per-minute per-process buckets for app breakdown in time-range queries
        private readonly ConcurrentDictionary<DateTime, ConcurrentDictionary<string, (long Downloaded, long Uploaded)>> _minuteProcessBuckets
            = new ConcurrentDictionary<DateTime, ConcurrentDictionary<string, (long Downloaded, long Uploaded)>>();

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NetworkUsageAggregator> _logger;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public NetworkUsageAggregator(IServiceScopeFactory scopeFactory, ILogger<NetworkUsageAggregator> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public void AddPacket(string processName, long bytes, bool isUpload)
        {
            if (string.IsNullOrWhiteSpace(processName))
                processName = "System";

            if (isUpload)
            {
                Interlocked.Add(ref _totalUploaded, bytes);
                Interlocked.Add(ref _bytesUploadedThisSecond, bytes);
            }
            else
            {
                Interlocked.Add(ref _totalDownloaded, bytes);
                Interlocked.Add(ref _bytesDownloadedThisSecond, bytes);
            }

            var stats = _processStats.GetOrAdd(processName, _ => new UsageStats());
            lock (stats)
            {
                if (isUpload) stats.BytesUploaded += bytes;
                else stats.BytesDownloaded += bytes;
            }

            // Record into the per-minute bucket (local time, truncated to minute)
            var minuteKey = TruncateToMinute(DateTime.Now);
            _minuteBuckets.AddOrUpdate(
                minuteKey,
                isUpload ? (0L, bytes) : (bytes, 0L),
                (_, existing) => isUpload
                    ? (existing.Downloaded, existing.Uploaded + bytes)
                    : (existing.Downloaded + bytes, existing.Uploaded));

            // Record per-process per-minute
            var processBucket = _minuteProcessBuckets.GetOrAdd(
                minuteKey,
                _ => new ConcurrentDictionary<string, (long Downloaded, long Uploaded)>());
            processBucket.AddOrUpdate(
                processName,
                isUpload ? (0L, bytes) : (bytes, 0L),
                (_, existing) => isUpload
                    ? (existing.Downloaded, existing.Uploaded + bytes)
                    : (existing.Downloaded + bytes, existing.Uploaded));
        }

        public Dictionary<string, UsageStats> GetCurrentProcessStats()
        {
            var dict = new Dictionary<string, UsageStats>();
            foreach (var kvp in _processStats)
            {
                lock (kvp.Value)
                {
                    dict[kvp.Key] = new UsageStats
                    {
                        BytesDownloaded = kvp.Value.BytesDownloaded,
                        BytesUploaded = kvp.Value.BytesUploaded
                    };
                }
            }
            return dict;
        }

        /// <summary>
        /// Returns total downloaded/uploaded bytes within [from, to) time-of-day from the minute-bucket log.
        /// Only covers data captured since the app was last started.
        /// </summary>
        public (long Downloaded, long Uploaded) GetUsageForTimeRange(TimeSpan from, TimeSpan to)
        {
            long dl = 0, ul = 0;
            foreach (var kvp in _minuteBuckets)
            {
                var t = kvp.Key.TimeOfDay;
                if (t >= from && t < to)
                {
                    dl += kvp.Value.Downloaded;
                    ul += kvp.Value.Uploaded;
                }
            }
            return (dl, ul);
        }

        /// <summary>
        /// Returns per-process totals for the given time range from the minute-bucket log.
        /// </summary>
        public List<(string ProcessName, long Downloaded, long Uploaded)> GetProcessUsageForTimeRange(TimeSpan from, TimeSpan to)
        {
            var merged = new Dictionary<string, (long Downloaded, long Uploaded)>();
            foreach (var kvp in _minuteProcessBuckets)
            {
                var t = kvp.Key.TimeOfDay;
                if (t >= from && t < to)
                {
                    foreach (var proc in kvp.Value)
                    {
                        if (merged.TryGetValue(proc.Key, out var existing))
                            merged[proc.Key] = (existing.Downloaded + proc.Value.Downloaded, existing.Uploaded + proc.Value.Uploaded);
                        else
                            merged[proc.Key] = proc.Value;
                    }
                }
            }
            return merged
                .OrderByDescending(p => p.Value.Downloaded + p.Value.Uploaded)
                .Select(p => (p.Key, p.Value.Downloaded, p.Value.Uploaded))
                .ToList();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            LoadMinuteBuckets();
            Task.Run(() => FlushLoop(_cts.Token));
            Task.Run(() => SpeedLoop(_cts.Token));
            Task.Run(() => BucketCleanupLoop(_cts.Token));
            return Task.CompletedTask;
        }

        private async Task SpeedLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(1000, token);
                long dl = Interlocked.Exchange(ref _bytesDownloadedThisSecond, 0);
                long ul = Interlocked.Exchange(ref _bytesUploadedThisSecond, 0);
                CurrentDownloadSpeedBps = dl;
                CurrentUploadSpeedBps = ul;
                SpeedUpdated?.Invoke(dl, ul);
            }
        }

        private async Task FlushLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(5000, token);
                await FlushAsync();
            }
        }

        /// <summary>Removes minute buckets older than 25 hours to prevent unbounded memory growth.</summary>
        private async Task BucketCleanupLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromHours(1), token);
                var cutoff = DateTime.Now.AddHours(-25);
                foreach (var key in _minuteBuckets.Keys.Where(k => k < cutoff).ToList())
                {
                    _minuteBuckets.TryRemove(key, out _);
                    _minuteProcessBuckets.TryRemove(key, out _);
                }
            }
        }

        private async Task FlushAsync()
        {
            long downloaded = Interlocked.Exchange(ref _totalDownloaded, 0);
            long uploaded = Interlocked.Exchange(ref _totalUploaded, 0);

            var currentStats = Interlocked.Exchange(ref _processStats, new ConcurrentDictionary<string, UsageStats>());

            if (downloaded == 0 && uploaded == 0 && currentStats.IsEmpty) return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IUsageRepository>();

                var processDict = currentStats.ToDictionary(k => k.Key, v => v.Value);
                await repo.SaveUsageAsync(DateTime.Now, downloaded, uploaded, processDict);
                SaveMinuteBuckets();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving network usage to database");

                // Re-add failed bytes so we don't lose data
                Interlocked.Add(ref _totalDownloaded, downloaded);
                Interlocked.Add(ref _totalUploaded, uploaded);
                foreach (var kvp in currentStats)
                {
                    var stats = _processStats.GetOrAdd(kvp.Key, _ => new UsageStats());
                    lock (stats)
                    {
                        stats.BytesDownloaded += kvp.Value.BytesDownloaded;
                        stats.BytesUploaded += kvp.Value.BytesUploaded;
                    }
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();
            await FlushAsync();
        }

        private static DateTime TruncateToMinute(DateTime dt)
            => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, dt.Kind);

        private void SaveMinuteBuckets()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dir = Path.Combine(appData, "DataSense");
                Directory.CreateDirectory(dir);

                var cutoff = DateTime.Now.AddHours(-25);

                var bucketsToSave = _minuteBuckets
                    .Where(kvp => kvp.Key >= cutoff)
                    .Select(kvp => new PersistedMinuteBucket
                    {
                        Time = kvp.Key.ToString("o"),
                        Downloaded = kvp.Value.Downloaded,
                        Uploaded = kvp.Value.Uploaded
                    }).ToList();

                var processBucketsToSave = _minuteProcessBuckets
                    .Where(kvp => kvp.Key >= cutoff)
                    .Select(kvp => new PersistedProcessBucket
                    {
                        Time = kvp.Key.ToString("o"),
                        Processes = kvp.Value.ToDictionary(
                            p => p.Key,
                            p => new PersistedUsage { Downloaded = p.Value.Downloaded, Uploaded = p.Value.Uploaded }
                        )
                    }).ToList();

                var payload = new PersistedBucketsPayload
                {
                    MinuteBuckets = bucketsToSave,
                    ProcessBuckets = processBucketsToSave
                };

                var path = Path.Combine(dir, "minute_buckets.json");
                var json = JsonSerializer.Serialize(payload);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving minute buckets to file");
            }
        }

        private void LoadMinuteBuckets()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var path = Path.Combine(appData, "DataSense", "minute_buckets.json");

                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var payload = JsonSerializer.Deserialize<PersistedBucketsPayload>(json);
                    if (payload != null)
                    {
                        var cutoff = DateTime.Now.AddHours(-25);

                        if (payload.MinuteBuckets != null)
                        {
                            foreach (var b in payload.MinuteBuckets)
                            {
                                if (DateTime.TryParse(b.Time, out var time) && time >= cutoff)
                                {
                                    _minuteBuckets[time] = (b.Downloaded, b.Uploaded);
                                }
                            }
                        }

                        if (payload.ProcessBuckets != null)
                        {
                            foreach (var pb in payload.ProcessBuckets)
                            {
                                if (DateTime.TryParse(pb.Time, out var time) && time >= cutoff)
                                {
                                    var dict = _minuteProcessBuckets.GetOrAdd(time, _ => new ConcurrentDictionary<string, (long, long)>());
                                    foreach (var proc in pb.Processes)
                                    {
                                        dict[proc.Key] = (proc.Value.Downloaded, proc.Value.Uploaded);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading minute buckets from file");
            }
        }
    }

    public class PersistedUsage
    {
        public long Downloaded { get; set; }
        public long Uploaded { get; set; }
    }

    public class PersistedMinuteBucket
    {
        public string Time { get; set; } = string.Empty;
        public long Downloaded { get; set; }
        public long Uploaded { get; set; }
    }

    public class PersistedProcessBucket
    {
        public string Time { get; set; } = string.Empty;
        public Dictionary<string, PersistedUsage> Processes { get; set; } = new();
    }

    public class PersistedBucketsPayload
    {
        public List<PersistedMinuteBucket> MinuteBuckets { get; set; } = new();
        public List<PersistedProcessBucket> ProcessBuckets { get; set; } = new();
    }
}
