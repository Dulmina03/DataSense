using System.Windows;
using DataSense.Core.Services;
using DataSense.UI.Services;
using DataSense.UI.ViewModels;

namespace DataSense.UI
{
    public partial class MainWindow : Window
    {
        private readonly SystemTrayService _trayService;

        public MainWindow(MainViewModel viewModel, DataLimitAlertService alertService)
        {
            InitializeComponent();
            DataContext = viewModel;

            _trayService = new SystemTrayService(this);
            _trayService.Initialize();

            // Wire up alert notifications to tray balloon tips
            alertService.AlertTriggered += (title, msg) =>
            {
                Dispatcher.InvokeAsync(() => _trayService.ShowBalloonTip(title, msg));
            };

            // Responsive window resizing handler
            SizeChanged += OnWindowSizeChanged;
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // Auto-collapse sidebar when window width is constrained (< 1150px)
                if (e.NewSize.Width < 1150 && !vm.IsSidebarCollapsed)
                {
                    vm.IsSidebarCollapsed = true;
                }
                else if (e.NewSize.Width >= 1280 && vm.IsSidebarCollapsed)
                {
                    vm.IsSidebarCollapsed = false;
                }
            }
        }

        protected override void OnStateChanged(System.EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized)
            {
                Hide(); // Minimize to tray
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true; // Prevent actual close
            Hide();          // Minimize to tray instead
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _trayService.Dispose();
            base.OnClosed(e);
        }
    }
}