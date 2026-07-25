using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Windows;
using DataSense.UI.ViewModels;
using DataSense.Data;

namespace DataSense.UI
{
    public partial class App : Application
    {
        private IHost _host;

        public App()
        {
            // Fix: When launched via Windows startup registry key, the working directory
            // defaults to C:\Windows\system32. Set it to the app folder so all relative
            // paths (logs, assets, etc.) resolve correctly.
            System.IO.Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            // Global unhandled exception handlers — show error before crash
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                MessageBox.Show($"Fatal error:\n\n{ex?.Message}\n\n{ex?.InnerException?.Message}\n\nStack:\n{ex?.StackTrace}",
                    "DataSense - Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, e) =>
            {
                MessageBox.Show($"UI error:\n\n{e.Exception?.Message}\n\n{e.Exception?.InnerException?.Message}\n\nStack:\n{e.Exception?.StackTrace}",
                    "DataSense - UI Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };

            // Use an absolute path for logs so they always write to AppData regardless of
            // the process working directory (important for startup launches).
            var logDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DataSense", "logs");
            System.IO.Directory.CreateDirectory(logDir);
            var logPath = System.IO.Path.Combine(logDir, "datasense-.txt");

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Debug()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                _host = Host.CreateDefaultBuilder()
                    .UseSerilog()
                    .ConfigureServices((context, services) =>
                    {
                        // Register DbContext
                        services.AddDbContext<DataSenseDbContext>();

                        // Register Network Services
                        services.AddSingleton<DataSense.Core.Interfaces.INetworkInterfaceService, DataSense.Infrastructure.Network.PcapNetworkInterfaceService>();
                        services.AddSingleton<DataSense.Core.Interfaces.IPacketCaptureService, DataSense.Infrastructure.Network.PcapPacketCaptureService>();
                        services.AddSingleton<DataSense.Core.Interfaces.IProcessConnectionMapper, DataSense.Infrastructure.Network.WindowsProcessConnectionMapper>();
                        services.AddSingleton<DataSense.Core.Services.NetworkUsageAggregator>();
                        services.AddHostedService(provider => provider.GetRequiredService<DataSense.Core.Services.NetworkUsageAggregator>());
                        services.AddHostedService<DataSense.Core.Services.NetworkMonitorService>();

                        // Register Repositories
                        services.AddScoped<DataSense.Core.Repositories.IUsageRepository, DataSense.Data.Repositories.UsageRepository>();

                        // Register Alert Service
                        services.AddSingleton<DataSense.Core.Services.DataLimitAlertService>();

                        // Register SpeedTest Service
                        services.AddSingleton<DataSense.Core.Services.SpeedTestService>();

                        // Register Export & History
                        services.AddTransient<DataSense.UI.Services.ExportService>();
                        services.AddTransient<DataSense.UI.ViewModels.HistoryViewModel>();
                        services.AddTransient<HistoryWindow>();
                        services.AddSingleton<DataSense.UI.Services.StartupService>();

                        // Register Net Speed Meter overlay service
                        services.AddSingleton<DataSense.UI.Services.NetSpeedMeterService>();

                        // Register ViewModels
                        services.AddTransient<MainViewModel>();

                        // Register Views
                        services.AddTransient<MainWindow>();
                    })
                    .Build();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup configuration error:\n\n{ex.Message}\n\n{ex.InnerException?.Message}",
                    "DataSense - Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private async void OnStartup(object sender, StartupEventArgs e)
        {
            try
            {
                // Subscribe to ProcessExit and SessionEnding for guaranteed synchronous flush on shutdown/restart
                AppDomain.CurrentDomain.ProcessExit += (s, ev) => PerformShutdownFlush();
                if (Current != null)
                {
                    Current.SessionEnding += (s, ev) => PerformShutdownFlush();
                }

                // Initialize Database schema and PRAGMAs once at startup
                using (var scope = _host.Services.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<DataSenseDbContext>();
                    dbContext.Database.EnsureCreated();
                    dbContext.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
                    dbContext.Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");
                    dbContext.Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000;");
                }

                await _host.StartAsync();
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start:\n\n{ex.Message}\n\n{ex.InnerException?.Message}\n\nStack:\n{ex.StackTrace}",
                    "DataSense - Start Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private void PerformShutdownFlush()
        {
            try
            {
                var aggregator = _host?.Services?.GetService<DataSense.Core.Services.NetworkUsageAggregator>();
                aggregator?.FlushSync();
            }
            catch { }
        }

        private async void OnExit(object sender, ExitEventArgs e)
        {
            PerformShutdownFlush();
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
            Log.CloseAndFlush();
        }
    }
}
