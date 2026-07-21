using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DataSense.UI.Converters
{
    /// <summary>
    /// Returns White brush when value is true (dark theme), otherwise Black.
    /// Used for foreground of the net speed meter to ensure contrast.
    /// </summary>
    public class BoolToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush WhiteBrush = new SolidColorBrush(Colors.White);
        private static readonly SolidColorBrush BlackBrush = new SolidColorBrush(Colors.Black);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDark)
                return isDark ? WhiteBrush : BlackBrush;
            return BlackBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
