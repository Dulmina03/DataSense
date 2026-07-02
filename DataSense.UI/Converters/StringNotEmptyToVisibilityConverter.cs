using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DataSense.UI.Converters
{
    /// <summary>
    /// Returns Visibility.Visible when the bound string is non-empty; Collapsed otherwise.
    /// Used to show/hide the info text in the Time Period Usage panel.
    /// </summary>
    public class StringNotEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string s && !string.IsNullOrEmpty(s)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
