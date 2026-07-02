using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataSense.Core.Domain;
using DataSense.Core.Repositories;
using DataSense.UI.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace DataSense.UI.ViewModels
{
    public partial class HistoryViewModel : ObservableObject
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ExportService _exportService;

        [ObservableProperty] private int _selectedYear = DateTime.Now.Year;

        // 0-based index into MonthOptions for ComboBox binding
        [ObservableProperty] private int _selectedMonthIndex = DateTime.Now.Month - 1;

        [ObservableProperty] private string _monthlyDownloaded = "—";
        [ObservableProperty] private string _monthlyUploaded = "—";
        [ObservableProperty] private string _monthlyTotal = "—";

        public ObservableCollection<ISeries> DailyChartSeries { get; } = new();
        public ObservableCollection<Axis> XAxes { get; } = new();
        public ObservableCollection<Axis> YAxes { get; } = new();
        public ObservableCollection<DailyUsageDisplay> DailyRows { get; } = new();
        public ObservableCollection<ProcessUsageDisplay> ProcessRows { get; } = new();

        // Dark canvas background for history chart
        public DrawMarginFrame HistoryDrawMarginFrame { get; } = new DrawMarginFrame
        {
            Fill = new SolidColorPaint(new SKColor(13, 27, 42)),
            Stroke = new SolidColorPaint(new SKColor(40, 52, 68)) { StrokeThickness = 1 }
        };

        public int[] YearOptions { get; } = Enumerable.Range(DateTime.Now.Year - 3, 4).Reverse().ToArray();
        public string[] MonthOptions { get; } = new[]
        {
            "January","February","March","April","May","June",
            "July","August","September","October","November","December"
        };

        // Derived 1-based month from index
        private int SelectedMonth => SelectedMonthIndex + 1;

        public HistoryViewModel(IServiceScopeFactory scopeFactory, ExportService exportService)
        {
            _scopeFactory = scopeFactory;
            _exportService = exportService;
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUsageRepository>();

            var from = new DateTime(SelectedYear, SelectedMonth, 1);
            var to = from.AddMonths(1).AddDays(-1);

            var dailyUsages = await repo.GetDailyUsagesAsync(from, to);
            var processUsages = await repo.GetProcessUsagesForMonthAsync(SelectedYear, SelectedMonth);
            var monthly = await repo.GetTotalUsageForMonthAsync(SelectedYear, SelectedMonth);

            MonthlyDownloaded = FormatBytes(monthly.BytesDownloaded);
            MonthlyUploaded = FormatBytes(monthly.BytesUploaded);
            MonthlyTotal = FormatBytes(monthly.BytesDownloaded + monthly.BytesUploaded);

            // Rebuild chart
            DailyChartSeries.Clear();
            XAxes.Clear();
            YAxes.Clear();

            int daysInMonth = DateTime.DaysInMonth(SelectedYear, SelectedMonth);
            var totalValues = new System.Collections.Generic.List<ObservableValue>();
            var labels = new string[daysInMonth];

            for (int day = 1; day <= daysInMonth; day++)
            {
                var usage = dailyUsages.FirstOrDefault(u => u.Date.Day == day);
                double totalGb = 0;
                if (usage != null)
                {
                    totalGb = (usage.BytesDownloaded + usage.BytesUploaded) / 1_073_741_824.0;
                }
                totalValues.Add(new ObservableValue(totalGb));
                labels[day - 1] = day.ToString();
            }

            var gradientPaint = new LinearGradientPaint(
                new[] { new SKColor(52, 211, 153), new SKColor(99, 102, 241) }, // Emerald Green (#34D399) to Indigo (#6366F1)
                new SKPoint(0.5f, 0f), // Start (top)
                new SKPoint(0.5f, 1f)  // End (bottom)
            );

            DailyChartSeries.Add(new ColumnSeries<ObservableValue>
            {
                Values = totalValues,
                Name = "Total Usage (GB)",
                Fill = gradientPaint,
                Rx = 4,
                Ry = 4,
                MaxBarWidth = 18,
                Padding = 2
            });

            XAxes.Add(new Axis 
            { 
                Labels = labels, 
                LabelsPaint = new SolidColorPaint(new SKColor(160, 174, 192)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(45, 55, 72)) { StrokeThickness = 1 },
                TextSize = 9,
                MinStep = 1,
                ForceStepToMin = true
            });

            YAxes.Add(new Axis
            {
                LabelsPaint = new SolidColorPaint(new SKColor(160, 174, 192)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(45, 55, 72)) { StrokeThickness = 1 },
                Labeler = value => value < 1 ? $"{value:F2} " : $"{value:F1} ",
                TextSize = 9,
                MinLimit = 0
            });

            // Daily rows
            DailyRows.Clear();
            foreach (var day in dailyUsages.OrderByDescending(d => d.Date))
            {
                DailyRows.Add(new DailyUsageDisplay
                {
                    Date = day.Date.ToString("dd MMM yyyy"),
                    Downloaded = FormatBytes(day.BytesDownloaded),
                    Uploaded = FormatBytes(day.BytesUploaded),
                    Total = FormatBytes(day.TotalBytes)
                });
            }

            // Process rows
            ProcessRows.Clear();
            foreach (var p in processUsages)
            {
                ProcessRows.Add(new ProcessUsageDisplay
                {
                    ProcessName = p.ProcessName,
                    DownloadedText = FormatBytes(p.Stats.BytesDownloaded),
                    UploadedText = FormatBytes(p.Stats.BytesUploaded)
                });
            }
        }

        [RelayCommand]
        private async Task ExportToCsvAsync()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"DataSense_{SelectedYear}_{SelectedMonth:D2}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            var (daily, procs) = await FetchDataAsync();
            _exportService.ExportToCsv(dlg.FileName, daily, procs);
            MessageBox.Show($"CSV exported to:\n{dlg.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task ExportToPdfAsync()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"DataSense_{SelectedYear}_{SelectedMonth:D2}.pdf"
            };
            if (dlg.ShowDialog() != true) return;

            var (daily, procs) = await FetchDataAsync();
            string title = $"DataSense Network Report — {MonthOptions[SelectedMonthIndex]} {SelectedYear}";
            _exportService.ExportToPdf(dlg.FileName, title, daily, procs);
            MessageBox.Show($"PDF exported to:\n{dlg.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task<(System.Collections.Generic.List<DailyUsageInfo>, System.Collections.Generic.List<ProcessUsageInfo>)> FetchDataAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUsageRepository>();
            var from = new DateTime(SelectedYear, SelectedMonth, 1);
            var to = from.AddMonths(1).AddDays(-1);
            var daily = await repo.GetDailyUsagesAsync(from, to);
            var procs = await repo.GetProcessUsagesForMonthAsync(SelectedYear, SelectedMonth);
            return (daily, procs);
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    public class DailyUsageDisplay
    {
        public string Date { get; set; } = string.Empty;
        public string Downloaded { get; set; } = string.Empty;
        public string Uploaded { get; set; } = string.Empty;
        public string Total { get; set; } = string.Empty;
    }
}
