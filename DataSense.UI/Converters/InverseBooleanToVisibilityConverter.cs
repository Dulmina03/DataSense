using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DataSense.UI.Converters
{
    /// <summary>
    /// Returns Visibility.Collapsed when true; Visibility.Visible when false.
    /// Useful for hiding text labels in the sidebar when collapsed.
    /// </summary>
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Collapsed;
        }
    }
}
