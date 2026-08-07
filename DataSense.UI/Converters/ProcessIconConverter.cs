using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DataSense.UI.Converters
{
    public class ProcessIconConverter : IValueConverter
    {
        private static readonly ConcurrentDictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string processName || string.IsNullOrWhiteSpace(processName))
                return null;

            if (IconCache.TryGetValue(processName, out var cachedIcon))
                return cachedIcon;

            ImageSource? iconSource = ExtractIconForProcess(processName);
            IconCache[processName] = iconSource;
            return iconSource;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static ImageSource? ExtractIconForProcess(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                foreach (var proc in processes)
                {
                    try
                    {
                        string? mainModulePath = proc.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(mainModulePath) && File.Exists(mainModulePath))
                        {
                            using Icon? icon = Icon.ExtractAssociatedIcon(mainModulePath);
                            if (icon != null)
                            {
                                ImageSource bitmap = Imaging.CreateBitmapSourceFromHIcon(
                                    icon.Handle,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                                bitmap.Freeze();
                                return bitmap;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore permission / 64-bit access issues for specific system processes
                    }
                }
            }
            catch
            {
                // Fallback
            }

            return null;
        }
    }
}
