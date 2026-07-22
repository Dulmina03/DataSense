using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Hardcodet.Wpf.TaskbarNotification;

namespace DataSense.UI.Services
{
    public class SystemTrayService : IDisposable
    {
        private TaskbarIcon? _taskbarIcon;
        private ContextMenu? _contextMenu;
        private readonly Window _mainWindow;

        // Required to properly give focus to the WPF popup so the menu doesn't blink/dismiss
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public SystemTrayService(Window mainWindow)
        {
            _mainWindow = mainWindow;
        }

        private static System.Drawing.Icon LoadAppIcon()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/datasense_icon.png", UriKind.Absolute);
                var sri = System.Windows.Application.GetResourceStream(uri);
                if (sri != null)
                {
                    using var bmp = new System.Drawing.Bitmap(sri.Stream);
                    return System.Drawing.Icon.FromHandle(bmp.GetHicon());
                }
            }
            catch { }
            return System.Drawing.SystemIcons.Application;
        }

        public void Initialize()
        {
            _taskbarIcon = new TaskbarIcon
            {
                Icon = LoadAppIcon(),
                ToolTipText = "DataSense - Monitoring your network",
                Visibility = Visibility.Visible
            };

            // Build the context menu
            _contextMenu = new ContextMenu();
            _contextMenu.Placement = PlacementMode.Mouse;

            var showItem = new MenuItem { Header = "Show DataSense" };
            showItem.Click += (s, e) => ShowMainWindow();
            _contextMenu.Items.Add(showItem);

            _contextMenu.Items.Add(new Separator());

            var exitItem = new MenuItem { Header = "Exit" };
            exitItem.Click += (s, e) => Application.Current.Shutdown();
            _contextMenu.Items.Add(exitItem);

            // Handle closing when the menu loses focus
            _contextMenu.Closed += (s, e) => _contextMenu.IsOpen = false;

            // Use TrayRightMouseUp so we control when/how the menu opens
            // This is key: we bring the hidden helper window to the foreground first
            // so the WPF ContextMenu popup receives keyboard focus and doesn't blink away.
            _taskbarIcon.TrayRightMouseUp += OnTrayRightMouseUp;
            _taskbarIcon.TrayMouseDoubleClick += (s, e) => ShowMainWindow();
        }

        private void OnTrayRightMouseUp(object sender, RoutedEventArgs e)
        {
            if (_contextMenu == null) return;

            // Bring our main window to the foreground (even if hidden) so WPF gets focus.
            // Without this the ContextMenu immediately loses activation and flickers closed.
            var hwnd = new System.Windows.Interop.WindowInteropHelper(_mainWindow).Handle;
            SetForegroundWindow(hwnd);

            _contextMenu.IsOpen = true;
        }

        private void ShowMainWindow()
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }

        public void ShowBalloonTip(string title, string message, BalloonIcon icon = BalloonIcon.Info)
        {
            _taskbarIcon?.ShowBalloonTip(title, message, icon);
        }

        public void Dispose()
        {
            _taskbarIcon?.Dispose();
        }
    }
}
