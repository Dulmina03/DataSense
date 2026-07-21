using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using DataSense.Core.Services;

namespace DataSense.UI.Services
{
    /// <summary>
    /// Manages the floating net speed meter overlay window.
    /// Subscribes to NetworkUsageAggregator speed events and routes
    /// them to the overlay window. Persists enabled and pinned states across restarts.
    /// </summary>
    public class NetSpeedMeterService : IDisposable, INotifyPropertyChanged
    {
        private readonly NetworkUsageAggregator _aggregator;
        private NetSpeedMeterWindow? _meterWindow;
        private bool _isEnabled;
        private bool _isPinnedToTaskbar;
        private bool _isDarkTheme = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DataSense", "speedmeter.json");

        public bool IsEnabled => _isEnabled;

        public bool IsPinnedToTaskbar
        {
            get => _isPinnedToTaskbar;
            set
            {
                if (_isPinnedToTaskbar != value)
                {
                    _isPinnedToTaskbar = value;
                    OnPropertyChanged();
                    SaveState();
                    UpdateWindowPosition();
                }
            }
        }

        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                if (_isDarkTheme != value)
                {
                    _isDarkTheme = value;
                    OnPropertyChanged();
                }
            }
        }

        public NetSpeedMeterService(NetworkUsageAggregator aggregator)
        {
            _aggregator = aggregator;
            _aggregator.SpeedUpdated += OnSpeedUpdated;
            LoadState();
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            SaveState();

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_isEnabled)
                {
                    if (_meterWindow == null)
                    {
                        _meterWindow = new NetSpeedMeterWindow();
                        _meterWindow.DataContext = this;
                    }
                    _meterWindow.Show();
                    UpdateWindowPosition();
                }
                else
                {
                    _meterWindow?.Hide();
                }
            });
        }

        public void SetDarkTheme(bool isDark)
        {
            IsDarkTheme = isDark;
        }

        public void LoadState()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var doc  = JsonDocument.Parse(json);
                    
                    if (doc.RootElement.TryGetProperty("enabled", out var enabledProp))
                        _isEnabled = enabledProp.GetBoolean();
                        
                    if (doc.RootElement.TryGetProperty("isPinned", out var pinnedProp))
                        _isPinnedToTaskbar = pinnedProp.GetBoolean();
                }
            }
            catch { }
        }

        private void SaveState()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                var options = new JsonSerializerOptions { WriteIndented = true };
                var state = new
                {
                    enabled = _isEnabled,
                    isPinned = _isPinnedToTaskbar
                };
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(state, options));
            }
            catch { }
        }

        public void UpdateWindowPosition()
        {
            if (_meterWindow == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_isPinnedToTaskbar)
                {
                    _meterWindow.Topmost = true;

                    double screenWidth = SystemParameters.PrimaryScreenWidth;
                    double screenHeight = SystemParameters.PrimaryScreenHeight;
                    double workAreaLeft = SystemParameters.WorkArea.Left;
                    double workAreaTop = SystemParameters.WorkArea.Top;
                    double workAreaRight = SystemParameters.WorkArea.Right;
                    double workAreaBottom = SystemParameters.WorkArea.Bottom;

                    double windowWidth = _meterWindow.Width;
                    double windowHeight = _meterWindow.Height;

                    // If width or height is NaN (not loaded yet), use default dimensions
                    if (double.IsNaN(windowWidth) || windowWidth == 0) windowWidth = 90;
                    if (double.IsNaN(windowHeight) || windowHeight == 0) windowHeight = 36;

                    // Position inside the taskbar (clock / system tray is on the far right)
                    if (workAreaBottom < screenHeight) // Bottom Taskbar
                    {
                        double taskbarHeight = screenHeight - workAreaBottom;
                        _meterWindow.Left = workAreaRight - windowWidth - 250; 
                        _meterWindow.Top = workAreaBottom + (taskbarHeight - windowHeight) / 2;
                    }
                    else if (workAreaTop > 0) // Top Taskbar
                    {
                        double taskbarHeight = workAreaTop;
                        _meterWindow.Left = workAreaRight - windowWidth - 250;
                        _meterWindow.Top = (taskbarHeight - windowHeight) / 2;
                    }
                    else if (workAreaLeft > 0) // Left Taskbar
                    {
                        double taskbarWidth = workAreaLeft;
                        _meterWindow.Left = (taskbarWidth - windowWidth) / 2;
                        _meterWindow.Top = workAreaBottom - windowHeight - 10;
                    }
                    else if (workAreaRight < screenWidth) // Right Taskbar
                    {
                        double taskbarWidth = screenWidth - workAreaRight;
                        _meterWindow.Left = workAreaRight + (taskbarWidth - windowWidth) / 2;
                        _meterWindow.Top = workAreaBottom - windowHeight - 10;
                    }
                    else
                    {
                        _meterWindow.Left = workAreaRight - windowWidth - 10;
                        _meterWindow.Top = workAreaBottom - windowHeight - 10;
                    }
                }
                else
                {
                    // Floating above taskbar
                    double windowWidth = _meterWindow.Width;
                    double windowHeight = _meterWindow.Height;
                    if (double.IsNaN(windowWidth) || windowWidth == 0) windowWidth = 90;
                    if (double.IsNaN(windowHeight) || windowHeight == 0) windowHeight = 36;

                    _meterWindow.Left = SystemParameters.WorkArea.Right - windowWidth - 20;
                    _meterWindow.Top = SystemParameters.WorkArea.Bottom - windowHeight - 20;
                }
            });
        }

        private void OnSpeedUpdated(long downloadBps, long uploadBps)
        {
            if (_meterWindow == null || !_isEnabled) return;
            _meterWindow.UpdateSpeeds(
                FormatSpeed(downloadBps),
                FormatSpeed(uploadBps));
        }

        private static string FormatSpeed(long bps)
        {
            if (bps >= 1_000_000_000) return $"{bps / 1_000_000_000.0:F1} GB/s";
            if (bps >= 1_000_000)     return $"{bps / 1_000_000.0:F1} MB/s";
            if (bps >= 1_000)         return $"{bps / 1_000.0:F1} KB/s";
            return $"{bps} B/s";
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void Dispose()
        {
            _aggregator.SpeedUpdated -= OnSpeedUpdated;
            Application.Current.Dispatcher.Invoke(() =>
            {
                _meterWindow?.Close();
                _meterWindow = null;
            });
        }
    }
}
