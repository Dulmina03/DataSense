using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DataSense.UI.Converters
{
    public class ProcessColorConverter : IValueConverter
    {
        public static readonly string[] Palette = new[]
        {
            "#3B82F6", // Blue
            "#06B6D4", // Cyan
            "#A855F7", // Purple
            "#F59E0B", // Amber
            "#22C55E", // Green
            "#EC4899", // Pink
            "#6366F1", // Indigo
            "#14B8A6", // Teal
            "#F97316", // Orange
            "#8B5CF6"  // Violet
        };

        public static string GetHexColor(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return "#6B7280";
            int hash = Math.Abs(processName.ToLowerInvariant().GetHashCode());
            return Palette[hash % Palette.Length];
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string processName = value as string ?? string.Empty;
            string hex = GetHexColor(processName);
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
