using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PatronGamingMonitor.Converters
{
    public class TicketStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Brushes.LightGray;

            string status = value.ToString()?.Trim()?.ToUpperInvariant();

            switch (status)
            {
                case "EXPIRED":
                    return new SolidColorBrush(Color.FromRgb(255, 220, 220)); // light red
                case "ACTIVE":
                    return new SolidColorBrush(Color.FromRgb(220, 255, 220)); // light green
                case "INUSE":
                case "USED":
                    return new SolidColorBrush(Color.FromRgb(255, 245, 200)); // yellowish
                default:
                    return new SolidColorBrush(Color.FromRgb(240, 240, 240)); // neutral gray
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
