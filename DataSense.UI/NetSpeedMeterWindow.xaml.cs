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
        private const int GWL_HWNDPARENT = -8;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

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

            // When the window is loaded, ensure position is calculated with the final rendered size.
            this.Loaded += (s, e) =>
            {
                if (DataContext is Services.NetSpeedMeterService service)
                {
                    service.UpdateWindowPosition();
                }
            };

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

            var helper = new WindowInteropHelper(this);

            // Find Windows Taskbar handle and set it as the owner of this window
            IntPtr taskbarHWnd = FindWindow("Shell_TrayWnd", null);
            if (taskbarHWnd != IntPtr.Zero)
            {
                SetWindowLongPtr(helper.Handle, GWL_HWNDPARENT, taskbarHWnd);
            }

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
