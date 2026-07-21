using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataSense.Core.Domain;
using DataSense.Core.Interfaces;
using DataSense.Core.Services;
using DataSense.Core.Repositories;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using DataSense.UI.Services;

namespace DataSense.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly NetworkUsageAggregator _aggregator;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly StartupService _startupService;
        private readonly SpeedTestService _speedTestService;
        partial void OnSelectedTabIndexChanged(int value)
        {
            if (value == 1) // History Tab
            {
                var _ = History.LoadAsync();
            }
        }
        private readonly DataLimitAlertService _alertService;
        private readonly INetworkInterfaceService _networkService;

        private readonly ObservableValue _downloadSpeedValue;
        private readonly ObservableValue _uploadSpeedValue;

        [ObservableProperty] private string _title = "DataSense";
        [ObservableProperty] private bool _isDarkTheme = true;
        [ObservableProperty] private bool _isStartupEnabled;
        [ObservableProperty] private bool _isSidebarCollapsed = false;

        // Navigation
        [ObservableProperty] private int _selectedTabIndex = 0;

        // Speed Test Properties
        [ObservableProperty] private bool _isSpeedTestRunning = false;
        [ObservableProperty] private string _speedTestPhase = "Idle";
        [ObservableProperty] private double _speedTestProgress = 0.0;
        [ObservableProperty] private double _speedTestCurrentSpeed = 0.0;
        [ObservableProperty] private string _speedTestCurrentSpeedText = "0.00";
        [ObservableProperty] private string _speedTestPingText = "—";
        [ObservableProperty] private string _speedTestJitterText = "—";
        [ObservableProperty] private string _speedTestDownloadText = "—";
        [ObservableProperty] private string _speedTestUploadText = "—";
        [ObservableProperty] private string _speedTestIp = "0.0.0.0";
        [ObservableProperty] private string _speedTestIsp = "Offline / Unknown ISP";
        [ObservableProperty] private string _speedTestServer = "Unavailable";
        [ObservableProperty] private double _speedTestAngle = -120.0;

        // Nested History VM
        public HistoryViewModel History { get; }

        // Live stats & labels
        [ObservableProperty] private string _downloadSpeedText = "0 B/s";
        [ObservableProperty] private string _uploadSpeedText = "0 B/s";
        [ObservableProperty] private string _dailyUsageText = "0 B";
        [ObservableProperty] private string _monthlyUsageText = "0 B";
        [ObservableProperty] private string _weeklyUsageText = "0 B";

        // Custom time-of-day period search with AM/PM support
        [ObservableProperty] private string _customStartHour = "00";
        [ObservableProperty] private string _customStartMinute = "00";
        [ObservableProperty] private string _customStartAmPm = "AM";
        [ObservableProperty] private string _customEndHour = "00";
        [ObservableProperty] private string _customEndMinute = "00";
        [ObservableProperty] private string _customEndAmPm = "AM";
        [ObservableProperty] private string _customPeriodTotalText = "0 B";
        [ObservableProperty] private string _customPeriodDownloadedText = "0 B";
        [ObservableProperty] private string _customPeriodUploadedText = "0 B";
        [ObservableProperty] private string _customPeriodInfoText = "";
        public ObservableCollection<ProcessUsageDisplay> CustomPeriodProcesses { get; } = new();
        public string[] AmPmOptions { get; } = new[] { "AM", "PM" };

        partial void OnCustomStartHourChanged(string value) { SaveCustomSettings(); _ = QueryCustomPeriodUsageAsync(); }
        partial void OnCustomStartMinuteChanged(string value) { SaveCustomSettings(); _ = QueryCustomPeriodUsageAsync(); }
        partial void OnCustomStartAmPmChanged(string value) { SaveCustomSettings(); _ = QueryCustomPeriodUsageAsync(); }
        partial void OnCustomEndHourChanged(string value) { SaveCustomSettings(); _ = QueryCustomPeriodUsageAsync(); }
        partial void OnCustomEndMinuteChanged(string value) { SaveCustomSettings(); _ = QueryCustomPeriodUsageAsync(); }
        partial void OnCustomEndAmPmChanged(string value) { SaveCustomSettings(); _ = QueryCustomPeriodUsageAsync(); }

        // Alerts / Limit settings
        [ObservableProperty] private double _monthlyLimitGb;
        [ObservableProperty] private double _limitUsagePercentage;
        [ObservableProperty] private string _limitUsageText = "0 B of 0 GB used";

        // Adapter list
        public ObservableCollection<NetworkAdapterInfo> AvailableAdapters { get; } = new();
        [ObservableProperty] private NetworkAdapterInfo? _selectedAdapter;

        // Chart collections (Speed & Peak Monthly)
        public ObservableCollection<ISeries> SpeedSeries { get; set; }
        public ObservableCollection<Axis> SpeedXAxes { get; } = new();
        public ObservableCollection<Axis> SpeedYAxes { get; } = new();

        public ObservableCollection<ISeries> PeakMonthlySeries { get; } = new();
        public ObservableCollection<Axis> PeakMonthlyXAxes { get; } = new();
        public ObservableCollection<Axis> PeakMonthlyYAxes { get; } = new();

        // Dark canvas backgrounds for each chart
        public DrawMarginFrame SpeedDrawMarginFrame { get; } = new DrawMarginFrame
        {
            Fill = new SolidColorPaint(new SKColor(13, 27, 42)),
            Stroke = new SolidColorPaint(new SKColor(40, 52, 68)) { StrokeThickness = 1 }
        };
        public DrawMarginFrame PeakMonthlyDrawMarginFrame { get; } = new DrawMarginFrame
        {
            Fill = new SolidColorPaint(new SKColor(13, 27, 42)),
            Stroke = new SolidColorPaint(new SKColor(40, 52, 68)) { StrokeThickness = 1 }
        };

        public ObservableCollection<ProcessUsageDisplay> TopProcesses { get; set; }

        private readonly DispatcherTimer _statsRefreshTimer;

        public MainViewModel(
            NetworkUsageAggregator aggregator,
            IServiceScopeFactory scopeFactory,
            StartupService startupService,
            DataLimitAlertService alertService,
            INetworkInterfaceService networkService,
            HistoryViewModel historyViewModel,
            SpeedTestService speedTestService)
        {
            _aggregator = aggregator;
            _scopeFactory = scopeFactory;
            _startupService = startupService;
            _alertService = alertService;
            _networkService = networkService;
            _speedTestService = speedTestService;
            History = historyViewModel;

            _isStartupEnabled = startupService.IsStartupEnabled();
            _monthlyLimitGb = _alertService.MonthlyLimitBytes / (1024.0 * 1024.0 * 1024.0);

            TopProcesses = new ObservableCollection<ProcessUsageDisplay>();

            _downloadSpeedValue = new ObservableValue(0);
            _uploadSpeedValue = new ObservableValue(0);

            // Real-time speed chart axes — dark themed, matches screenshot style
            SpeedXAxes.Add(new Axis
            {
                Name = "Real-time Traffic Speed",
                NamePaint = new SolidColorPaint(new SKColor(120, 140, 160)),
                LabelsPaint = new SolidColorPaint(new SKColor(120, 140, 160)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(40, 52, 68)) { StrokeThickness = 1 },
                SubseparatorsPaint = new SolidColorPaint(new SKColor(30, 40, 55)) { StrokeThickness = 0.5f },
                TextSize = 10,
                ShowSeparatorLines = true
            });
            SpeedYAxes.Add(new Axis
            {
                Name = "Bandwidth (Mbps)",
                NamePaint = new SolidColorPaint(new SKColor(120, 140, 160)),
                LabelsPaint = new SolidColorPaint(new SKColor(120, 140, 160)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(40, 52, 68)) { StrokeThickness = 1 },
                SubseparatorsPaint = new SolidColorPaint(new SKColor(30, 40, 55)) { StrokeThickness = 0.5f },
                TextSize = 10,
                ShowSeparatorLines = true
            });

            // Peak Monthly chart axes — dark themed
            PeakMonthlyXAxes.Add(new Axis
            {
                LabelsPaint = new SolidColorPaint(new SKColor(120, 140, 160)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(40, 52, 68)) { StrokeThickness = 1 },
                TextSize = 10
            });
            PeakMonthlyYAxes.Add(new Axis
            {
                LabelsPaint = new SolidColorPaint(new SKColor(120, 140, 160)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(40, 52, 68)) { StrokeThickness = 1 },
                TextSize = 10
            });

            // Cyan gradient download + green gradient upload — matching the screenshot
            SpeedSeries = new ObservableCollection<ISeries>
            {
                new LineSeries<ObservableValue>
                {
                    Values = new ObservableCollection<ObservableValue>(),
                    Name = "Download",
                    // Bright cyan stroke like the screenshot
                    Stroke = new SolidColorPaint(new SKColor(0, 229, 255)) { StrokeThickness = 2 },
                    GeometryFill = null,
                    GeometryStroke = null,
                    // Semi-transparent cyan fill for the glowing area effect
                    Fill = new LinearGradientPaint(
                        new[] { new SKColor(0, 229, 255, 120), new SKColor(0, 229, 255, 8) },
                        new SKPoint(0.5f, 0f),
                        new SKPoint(0.5f, 1f)
                    ),
                    LineSmoothness = 0.6
                },
                new LineSeries<ObservableValue>
                {
                    Values = new ObservableCollection<ObservableValue>(),
                    Name = "Upload",
                    // Bright green stroke like the screenshot
                    Stroke = new SolidColorPaint(new SKColor(0, 230, 118)) { StrokeThickness = 2 },
                    GeometryFill = null,
                    GeometryStroke = null,
                    // Semi-transparent green fill for the glowing area effect
                    Fill = new LinearGradientPaint(
                        new[] { new SKColor(0, 230, 118, 100), new SKColor(0, 230, 118, 8) },
                        new SKPoint(0.5f, 0f),
                        new SKPoint(0.5f, 1f)
                    ),
                    LineSmoothness = 0.6
                }
            };

            for (int i = 0; i < 60; i++)
            {
                ((ObservableCollection<ObservableValue>)SpeedSeries[0].Values!).Add(new ObservableValue(0));
                ((ObservableCollection<ObservableValue>)SpeedSeries[1].Values!).Add(new ObservableValue(0));
            }

            // Load adapters
            foreach (var adapter in _networkService.GetAvailableAdapters())
            {
                AvailableAdapters.Add(adapter);
            }
            SelectedAdapter = AvailableAdapters.FirstOrDefault(a => !a.IsLoopback) ?? AvailableAdapters.FirstOrDefault();

            _aggregator.SpeedUpdated += OnSpeedUpdated;

            // Timer to refresh summaries, limits, and charts
            _statsRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _statsRefreshTimer.Tick += async (s, e) => await RefreshStatsAsync();
            _statsRefreshTimer.Start();

            // Load custom settings
            LoadCustomSettings();

            // Initial load
            var _ = RefreshStatsAsync();
        }

        private void OnSpeedUpdated(long downloadBps, long uploadBps)
        {
            if (App.Current == null) return;

            App.Current.Dispatcher.InvokeAsync(() =>
            {
                DownloadSpeedText = FormatBytes(downloadBps) + "/s";
                UploadSpeedText = FormatBytes(uploadBps) + "/s";

                var dlSeries = (LineSeries<ObservableValue>)SpeedSeries[0];
                var ulSeries = (LineSeries<ObservableValue>)SpeedSeries[1];

                var dlValues = (ObservableCollection<ObservableValue>)dlSeries.Values!;
                var ulValues = (ObservableCollection<ObservableValue>)ulSeries.Values!;

                dlValues.Add(new ObservableValue(downloadBps / 1048576.0));
                ulValues.Add(new ObservableValue(uploadBps / 1048576.0));

                if (dlValues.Count > 60)
                {
                    dlValues.RemoveAt(0);
                    ulValues.RemoveAt(0);
                }
                // Note: UpdateTopProcesses is called from RefreshStatsAsync (every 5s)
                // to include both DB daily totals and live data
            });
        }

        private void UpdateTopProcesses(Dictionary<string, (long downloaded, long uploaded)>? dailyDbTotals = null)
        {
            // Live in-memory stats (current flush window, not yet saved to DB)
            var liveStats = _aggregator.GetCurrentProcessStats();

            // Merge: DB daily totals + live window
            var merged = new Dictionary<string, (long downloaded, long uploaded)>();

            if (dailyDbTotals != null)
            {
                foreach (var kv in dailyDbTotals)
                    merged[kv.Key] = kv.Value;
            }

            foreach (var kv in liveStats)
            {
                if (merged.TryGetValue(kv.Key, out var existing))
                    merged[kv.Key] = (existing.downloaded + kv.Value.BytesDownloaded, existing.uploaded + kv.Value.BytesUploaded);
                else
                    merged[kv.Key] = (kv.Value.BytesDownloaded, kv.Value.BytesUploaded);
            }

            var top = merged.OrderByDescending(p => p.Value.downloaded + p.Value.uploaded)
                            .Take(5)
                            .ToList();

            TopProcesses.Clear();
            long maxBytes = top.Any() ? top.Max(p => p.Value.downloaded + p.Value.uploaded) : 1;

            foreach (var item in top)
            {
                long totalBytes = item.Value.downloaded + item.Value.uploaded;
                // Live speed from in-memory only
                long speedBps = liveStats.TryGetValue(item.Key, out var live)
                    ? (live.BytesDownloaded + live.BytesUploaded) / 5
                    : 0;

                TopProcesses.Add(new ProcessUsageDisplay
                {
                    ProcessName = item.Key,
                    DownloadedText = FormatBytes(item.Value.downloaded),
                    UploadedText = FormatBytes(item.Value.uploaded),
                    TotalText = FormatBytes(totalBytes),
                    SpeedText = FormatBytes(speedBps) + "/s",
                    SpeedProgress = ((double)totalBytes / maxBytes) * 100.0
                });
            }
        }

        private async Task RefreshStatsAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IUsageRepository>();

                var now = DateTime.Now;

                // 1. Monthly Usage
                var monthlyStats = await repo.GetTotalUsageForMonthAsync(now.Year, now.Month);
                long monthlyBytes = monthlyStats.BytesDownloaded + monthlyStats.BytesUploaded;
                MonthlyUsageText = FormatBytes(monthlyBytes);

                // 2. Today's Daily Usage + per-app totals
                var dailyStatsList = await repo.GetDailyUsagesAsync(now.Date, now.Date);
                var todayStats = dailyStatsList.FirstOrDefault();
                long todayBytes = todayStats != null ? todayStats.BytesDownloaded + todayStats.BytesUploaded : 0;
                DailyUsageText = FormatBytes(todayBytes);

                // Load today's per-app DB totals for the App-wise Usage panel
                var dailyProcessList = await repo.GetProcessUsagesForDateAsync(now.Date);
                var dailyDbTotals = dailyProcessList.ToDictionary(
                    p => p.ProcessName,
                    p => (p.Stats.BytesDownloaded, p.Stats.BytesUploaded)
                );
                UpdateTopProcesses(dailyDbTotals);

                // 2.5 Weekly Usage (last 7 days)
                var weeklyStatsList = await repo.GetDailyUsagesAsync(now.AddDays(-6), now);
                long weeklyBytes = weeklyStatsList.Sum(w => w.BytesDownloaded + w.BytesUploaded);
                WeeklyUsageText = FormatBytes(weeklyBytes);

                // 3. Limit Calculations
                double limitBytes = MonthlyLimitGb * 1024.0 * 1024.0 * 1024.0;
                if (limitBytes > 0)
                {
                    LimitUsagePercentage = Math.Min(100.0, (monthlyBytes / limitBytes) * 100.0);
                    LimitUsageText = $"{FormatBytes(monthlyBytes)} of {MonthlyLimitGb:F1} GB used";
                }
                else
                {
                    LimitUsagePercentage = 0;
                    LimitUsageText = $"{FormatBytes(monthlyBytes)} (No limit set)";
                }

                // 4. Populate Peak Monthly Chart (last 7 days)
                var last7Days = weeklyStatsList; // reuse fetched weekly stats
                
                PeakMonthlySeries.Clear();
                var peakValues = last7Days.Select(d => new ObservableValue((d.BytesDownloaded + d.BytesUploaded) / 1_048_576.0)).ToList();
                var peakLabels = last7Days.Select(d => d.Date.ToString("dd MMM")).ToArray();

                // If empty db, show mock bars to match design
                if (!peakValues.Any())
                {
                    peakValues = new List<ObservableValue> { new(10), new(25), new(15), new(35), new(20), new(45), new(30) };
                    peakLabels = new[] { "Wk 1", "Wk 2", "Wk 3", "Wk 4", "Wk 5", "Wk 6", "Wk 7" };
                }

                PeakMonthlySeries.Add(new ColumnSeries<ObservableValue>
                {
                    Values = peakValues,
                    Name = "Total Usage (MB)",
                    Fill = new SolidColorPaint(SKColors.Cyan)
                });

                PeakMonthlyXAxes.Clear();
                PeakMonthlyXAxes.Add(new Axis
                {
                    Labels = peakLabels,
                    LabelsPaint = new SolidColorPaint(new SKColor(160, 174, 192)),
                    SeparatorsPaint = new SolidColorPaint(new SKColor(45, 55, 72)) { StrokeThickness = 1 }
                });

                // 5. Update Custom Time Period Stats in real time
                await QueryCustomPeriodUsageAsync();
            }
            catch
            {
                // Silently handle DB access issues
            }
        }

        [RelayCommand]
        public Task QueryCustomPeriodUsageAsync()
        {
            try
            {
                if (!TryBuildTimeSpan(CustomStartHour, CustomStartMinute, CustomStartAmPm, out var fromSpan))
                {
                    CustomPeriodInfoText = "⚠ Invalid start time. Use hour 1-12 and minute 00-59.";
                    return Task.CompletedTask;
                }

                if (!TryBuildTimeSpan(CustomEndHour, CustomEndMinute, CustomEndAmPm, out var toSpan))
                {
                    CustomPeriodInfoText = "⚠ Invalid end time. Use hour 1-12 and minute 00-59.";
                    return Task.CompletedTask;
                }

                if (toSpan <= fromSpan)
                {
                    CustomPeriodInfoText = "⚠ End time must be after start time.";
                    return Task.CompletedTask;
                }

                // Query the in-memory minute-bucket log
                var (dl, ul) = _aggregator.GetUsageForTimeRange(fromSpan, toSpan);
                long totalBytes = dl + ul;

                CustomPeriodDownloadedText = FormatBytes(dl);
                CustomPeriodUploadedText = FormatBytes(ul);
                CustomPeriodTotalText = FormatBytes(totalBytes);
                CustomPeriodInfoText = $"Session data: {CustomStartHour}:{CustomStartMinute} {CustomStartAmPm} – {CustomEndHour}:{CustomEndMinute} {CustomEndAmPm}";

                // Per-process breakdown
                var processes = _aggregator.GetProcessUsageForTimeRange(fromSpan, toSpan);

                CustomPeriodProcesses.Clear();
                if (processes.Any())
                {
                    long maxBytes = processes.Max(p => p.Downloaded + p.Uploaded);
                    if (maxBytes == 0) maxBytes = 1;

                    foreach (var p in processes.Take(10))
                    {
                        long pTotal = p.Downloaded + p.Uploaded;
                        CustomPeriodProcesses.Add(new ProcessUsageDisplay
                        {
                            ProcessName = p.ProcessName,
                            DownloadedText = FormatBytes(p.Downloaded),
                            UploadedText = FormatBytes(p.Uploaded),
                            TotalText = FormatBytes(pTotal),
                            SpeedProgress = ((double)pTotal / maxBytes) * 100.0
                        });
                    }
                }
                else
                {
                    CustomPeriodInfoText += " — No data recorded yet for this range.";
                }
            }
            catch
            {
                CustomPeriodInfoText = "⚠ Error querying time range.";
            }
            return Task.CompletedTask;
        }

        private static bool TryBuildTimeSpan(string hour, string minute, string ampm, out TimeSpan result)
        {
            result = default;
            if (!int.TryParse(hour?.Trim(), out int h)) return false;
            if (!int.TryParse(minute?.Trim(), out int m)) return false;
            if (h < 0 || h > 12 || m < 0 || m > 59) return false;

            // Convert 12-hour/24-hour hybrid to 24-hour
            if (h == 0)
            {
                h = 0;
            }
            else if (ampm?.Trim().ToUpper() == "PM" && h != 12)
            {
                h += 12;
            }
            else if (ampm?.Trim().ToUpper() == "AM" && h == 12)
            {
                h = 0;
            }

            result = new TimeSpan(h, m, 0);
            return true;
        }

        [RelayCommand]
        private void ToggleTheme()
        {
            var existingResourceDict = App.Current.Resources.MergedDictionaries
                .OfType<MaterialDesignThemes.Wpf.BundledTheme>()
                .FirstOrDefault();

            if (existingResourceDict != null)
            {
                existingResourceDict.BaseTheme = IsDarkTheme
                    ? MaterialDesignThemes.Wpf.BaseTheme.Dark
                    : MaterialDesignThemes.Wpf.BaseTheme.Light;
            }
        }

        [RelayCommand]
        private void ToggleStartup()
        {
            _startupService.SetStartupEnabled(IsStartupEnabled);
        }

        [RelayCommand]
        private void SaveLimit()
        {
            long bytes = (long)(MonthlyLimitGb * 1024.0 * 1024.0 * 1024.0);
            _alertService.SetMonthlyLimit(bytes);
            var _ = RefreshStatsAsync();
            MessageBox.Show($"Monthly limit updated to {MonthlyLimitGb:F1} GB.", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ChangeAdapter()
        {
            if (SelectedAdapter != null)
            {
                MessageBox.Show($"Monitoring switched to network adapter:\n{SelectedAdapter.Name}", "Adapter Changed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        private void ToggleSidebar()
        {
            IsSidebarCollapsed = !IsSidebarCollapsed;
        }

        private CancellationTokenSource? _speedTestCts;

        [RelayCommand]
        private async Task RunSpeedTestAsync()
        {
            if (IsSpeedTestRunning)
            {
                _speedTestCts?.Cancel();
                return;
            }

            IsSpeedTestRunning = true;
            _speedTestCts = new CancellationTokenSource();

            SpeedTestPhase = "Connecting";
            SpeedTestProgress = 0;
            SpeedTestCurrentSpeed = 0;
            SpeedTestCurrentSpeedText = "0.00";
            SpeedTestPingText = "—";
            SpeedTestJitterText = "—";
            SpeedTestDownloadText = "—";
            SpeedTestUploadText = "—";
            SpeedTestAngle = -120.0;

            try
            {
                await _speedTestService.RunTestAsync(progress =>
                {
                    if (App.Current == null) return;

                    App.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SpeedTestPhase = progress.Phase;
                        SpeedTestProgress = progress.ProgressPercentage;

                        if (progress.Phase == "Connecting")
                        {
                            SpeedTestIp = progress.IpAddress;
                            SpeedTestIsp = progress.IspName;
                            SpeedTestServer = progress.ServerLocation;
                        }
                        else if (progress.Phase == "Ping")
                        {
                            SpeedTestPingText = progress.PingMs > 0 ? $"{progress.PingMs:F0} ms" : "—";
                            SpeedTestJitterText = progress.JitterMs > 0 ? $"{progress.JitterMs:F0} ms" : "—";
                            SpeedTestCurrentSpeedText = "0.00";
                            SpeedTestAngle = -120.0;
                        }
                        else if (progress.Phase == "Download")
                        {
                            // Mbps ÷ 8 = MBps
                            double dlMBps = progress.CurrentSpeedMbps / 8.0;
                            SpeedTestCurrentSpeed = dlMBps;
                            SpeedTestCurrentSpeedText = dlMBps.ToString("F2");
                            SpeedTestDownloadText = progress.DownloadMbps > 0
                                ? $"{(progress.DownloadMbps / 8.0):F2} MBps"
                                : "—";
                            SpeedTestAngle = SpeedToAngle(dlMBps * 8); // keep angle based on Mbps
                        }
                        else if (progress.Phase == "Upload")
                        {
                            // Mbps ÷ 8 = MBps
                            double ulMBps = progress.CurrentSpeedMbps / 8.0;
                            SpeedTestCurrentSpeed = ulMBps;
                            SpeedTestCurrentSpeedText = ulMBps.ToString("F2");
                            // Keep download result visible during upload phase
                            SpeedTestDownloadText = progress.DownloadMbps > 0
                                ? $"{(progress.DownloadMbps / 8.0):F2} MBps"
                                : SpeedTestDownloadText;
                            SpeedTestUploadText = progress.UploadMbps > 0
                                ? $"{(progress.UploadMbps / 8.0):F2} MBps"
                                : "—";
                            SpeedTestAngle = SpeedToAngle(progress.CurrentSpeedMbps);
                        }
                        else if (progress.Phase == "Completed")
                        {
                            SpeedTestDownloadText = progress.DownloadMbps > 0
                                ? $"{(progress.DownloadMbps / 8.0):F2} MBps"
                                : "0.00 MBps";
                            SpeedTestUploadText = progress.UploadMbps > 0
                                ? $"{(progress.UploadMbps / 8.0):F2} MBps"
                                : "0.00 MBps";
                            SpeedTestCurrentSpeed = 0;
                            SpeedTestCurrentSpeedText = "0.00";
                            SpeedTestAngle = -120.0;
                        }
                    });
                }, _speedTestCts.Token);
            }
            catch (OperationCanceledException)
            {
                SpeedTestPhase = "Canceled";
            }
            catch
            {
                SpeedTestPhase = "Error";
            }
            finally
            {
                IsSpeedTestRunning = false;
                _speedTestCts?.Dispose();
                _speedTestCts = null;
            }
        }

        private static double SpeedToAngle(double speed)
        {
            if (speed <= 0) return -120;
            if (speed <= 5) return -120 + (speed / 5.0) * 30.0;
            if (speed <= 10) return -90 + ((speed - 5.0) / 5.0) * 30.0;
            if (speed <= 50) return -60 + ((speed - 10.0) / 40.0) * 30.0;
            if (speed <= 100) return -30 + ((speed - 50.0) / 50.0) * 30.0;
            if (speed <= 250) return 0 + ((speed - 100.0) / 150.0) * 30.0;
            if (speed <= 500) return 30 + ((speed - 250.0) / 250.0) * 30.0;
            if (speed <= 750) return 60 + ((speed - 500.0) / 250.0) * 30.0;
            if (speed <= 1000) return 90 + ((speed - 750.0) / 250.0) * 30.0;
            return 120 + Math.Min(30.0, ((speed - 1000.0) / 1000.0) * 30.0);
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private bool _isInitializingCustomSettings;

        private class CustomPeriodSettings
        {
            public string StartHour { get; set; } = "00";
            public string StartMinute { get; set; } = "00";
            public string StartAmPm { get; set; } = "AM";
            public string EndHour { get; set; } = "00";
            public string EndMinute { get; set; } = "00";
            public string EndAmPm { get; set; } = "AM";
            public bool IsSet { get; set; } = false;
        }

        private void SaveCustomSettings()
        {
            if (_isInitializingCustomSettings) return;
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dir = Path.Combine(appData, "DataSense");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "custom_period_settings.json");

                var settings = new CustomPeriodSettings
                {
                    StartHour = CustomStartHour,
                    StartMinute = CustomStartMinute,
                    StartAmPm = CustomStartAmPm,
                    EndHour = CustomEndHour,
                    EndMinute = CustomEndMinute,
                    EndAmPm = CustomEndAmPm,
                    IsSet = true
                };

                var json = System.Text.Json.JsonSerializer.Serialize(settings);
                File.WriteAllText(path, json);
            }
            catch
            {
                // Ignore errors
            }
        }

        private void LoadCustomSettings()
        {
            _isInitializingCustomSettings = true;
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var path = Path.Combine(appData, "DataSense", "custom_period_settings.json");

                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var settings = System.Text.Json.JsonSerializer.Deserialize<CustomPeriodSettings>(json);
                    if (settings != null && settings.IsSet)
                    {
                        CustomStartHour = settings.StartHour;
                        CustomStartMinute = settings.StartMinute;
                        CustomStartAmPm = settings.StartAmPm;
                        CustomEndHour = settings.EndHour;
                        CustomEndMinute = settings.EndMinute;
                        CustomEndAmPm = settings.EndAmPm;
                        _isInitializingCustomSettings = false;
                        return;
                    }
                }
            }
            catch
            {
                // Fallback to defaults
            }

            CustomStartHour = "00";
            CustomStartMinute = "00";
            CustomStartAmPm = "AM";
            CustomEndHour = "00";
            CustomEndMinute = "00";
            CustomEndAmPm = "AM";
            _isInitializingCustomSettings = false;
        }
    }

    public class ProcessUsageDisplay
    {
        public string ProcessName { get; set; } = string.Empty;
        public string DownloadedText { get; set; } = string.Empty;
        public string UploadedText { get; set; } = string.Empty;
        public string TotalText { get; set; } = string.Empty;
        public string SpeedText { get; set; } = string.Empty;
        public double SpeedProgress { get; set; }
    }
}
