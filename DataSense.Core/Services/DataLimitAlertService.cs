using System;
using DataSense.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataSense.Core.Services
{
    public class DataLimitAlertService
    {
        private readonly NetworkUsageAggregator _aggregator;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DataLimitAlertService> _logger;

        private long _monthlyLimitBytes = 100L * 1024 * 1024 * 1024; // Default: 100 GB
        private bool _alert50Sent = false;
        private bool _alert75Sent = false;
        private bool _alert90Sent = false;
        private bool _alert100Sent = false;

        public long MonthlyLimitBytes => _monthlyLimitBytes;

        public event Action<string, string>? AlertTriggered;

        public DataLimitAlertService(
            NetworkUsageAggregator aggregator,
            IServiceScopeFactory scopeFactory,
            ILogger<DataLimitAlertService> logger)
        {
            _aggregator = aggregator;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _aggregator.SpeedUpdated += OnSpeedUpdated;
        }

        public void SetMonthlyLimit(long bytes)
        {
            _monthlyLimitBytes = bytes;
            // Reset alerts when limit changes
            _alert50Sent = _alert75Sent = _alert90Sent = _alert100Sent = false;
        }

        private async void OnSpeedUpdated(long dl, long ul)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IUsageRepository>();
                var now = DateTime.Now;
                var stats = await repo.GetTotalUsageForMonthAsync(now.Year, now.Month);
                long total = stats.BytesDownloaded + stats.BytesUploaded;

                if (_monthlyLimitBytes <= 0) return;

                double pct = (double)total / _monthlyLimitBytes * 100.0;

                if (!_alert50Sent && pct >= 50)
                {
                    _alert50Sent = true;
                    AlertTriggered?.Invoke("Data Usage Alert", $"You've used 50% of your monthly data limit ({FormatBytes(total)} / {FormatBytes(_monthlyLimitBytes)})");
                }
                else if (!_alert75Sent && pct >= 75)
                {
                    _alert75Sent = true;
                    AlertTriggered?.Invoke("Data Usage Warning", $"You've used 75% of your monthly data limit!");
                }
                else if (!_alert90Sent && pct >= 90)
                {
                    _alert90Sent = true;
                    AlertTriggered?.Invoke("Data Usage Critical", $"You've used 90% of your monthly data limit!");
                }
                else if (!_alert100Sent && pct >= 100)
                {
                    _alert100Sent = true;
                    AlertTriggered?.Invoke("Data Limit Reached", $"You've reached your monthly data limit of {FormatBytes(_monthlyLimitBytes)}!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking data limit alerts");
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
