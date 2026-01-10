using System;
using System.Globalization;
using System.Net;
using System.Windows.Data;

namespace PatronGamingMonitor.Converters
{
    public class FilterMatchConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2)
                return false;

            var filterType = values[0]?.ToString();
            var commandParam = values[1]?.ToString();

            // Decode HTML entities (e.g., &lt; → <)
            if (!string.IsNullOrEmpty(commandParam))
            {
                commandParam = WebUtility.HtmlDecode(commandParam);
            }

            // Compare case-insensitive
            var isMatch = string.Equals(filterType, commandParam, StringComparison.OrdinalIgnoreCase);

            return isMatch;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}