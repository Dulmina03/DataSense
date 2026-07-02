using System;
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

        public void Initialize()
        {
            _taskbarIcon = new TaskbarIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
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
