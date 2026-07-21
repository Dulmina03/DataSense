using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace DataSense.UI
{
    public partial class NetSpeedMeterWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

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

            // Listen for size changes to reposition when font size or content changes
            this.SizeChanged += (s, e) =>
            {
                if (DataContext is Services.NetSpeedMeterService service)
                {
                    service.UpdateWindowPosition();
                }
            };
        }

        public void SetClickThrough(bool clickThrough)
        {
            try
            {
                var helper = new WindowInteropHelper(this);
                if (helper.Handle == IntPtr.Zero) return;

                int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
                if (clickThrough)
                {
                    // Add WS_EX_TRANSPARENT so mouse clicks pass through
                    SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
                }
                else
                {
                    // Remove WS_EX_TRANSPARENT, keep WS_EX_NOACTIVATE
                    SetWindowLong(helper.Handle, GWL_EXSTYLE, (exStyle & ~WS_EX_TRANSPARENT) | WS_EX_NOACTIVATE);
                }
            }
            catch { }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Apply style depending on initial pinned status
            if (DataContext is Services.NetSpeedMeterService service)
            {
                SetClickThrough(service.IsPinnedToTaskbar);
            }
            else
            {
                SetClickThrough(false);
            }
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
