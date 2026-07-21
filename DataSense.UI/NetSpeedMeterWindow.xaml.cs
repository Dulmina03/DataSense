using System;
using System.Windows;
using System.Windows.Input;

namespace DataSense.UI
{
    public partial class NetSpeedMeterWindow : Window
    {
        // Allow dragging the overlay around the screen
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is Services.NetSpeedMeterService service && service.IsPinnedToTaskbar)
            {
                return; // Dragging is disabled when pinned to taskbar
            }

            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        public NetSpeedMeterWindow()
        {
            InitializeComponent();
        }



        public void UpdateSpeeds(string download, string upload)
        {
            Dispatcher.InvokeAsync(() =>
            {
                DownloadText.Text = download;
                UploadText.Text   = upload;
            });
        }
    }
}
