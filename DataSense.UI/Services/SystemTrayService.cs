using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;

namespace DataSense.UI.Services
{
    public class SystemTrayService : IDisposable
    {
        private TaskbarIcon? _taskbarIcon;
        private readonly Window _mainWindow;

        public SystemTrayService(Window mainWindow)
        {
            _mainWindow = mainWindow;
        }

        private static System.Drawing.Icon LoadAppIcon()
        {
            try
            {
                // Load from embedded resource (pack URI → stream)
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

            // Context menu
            var contextMenu = new System.Windows.Controls.ContextMenu();

            var showItem = new System.Windows.Controls.MenuItem { Header = "Show DataSense" };
            showItem.Click += (s, e) => ShowMainWindow();
            contextMenu.Items.Add(showItem);

            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
            exitItem.Click += (s, e) => Application.Current.Shutdown();
            contextMenu.Items.Add(exitItem);

            _taskbarIcon.ContextMenu = contextMenu;
            _taskbarIcon.TrayMouseDoubleClick += (s, e) => ShowMainWindow();
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
