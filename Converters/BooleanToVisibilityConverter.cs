using NLog;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PatronGamingMonitor.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                bool boolValue = false;
                bool invert = false;

                // Safely extract boolean value
                if (value is bool b)
                    boolValue = b;

                // Support two ways to invert:
                // - ConverterParameter="Invert"
                // - ConverterParameter="True"
                if (parameter != null)
                {
                    var param = parameter.ToString().Trim().ToLowerInvariant();
                    invert = param == "invert" || param == "true";
                }

                // Apply inversion logic
                bool result = invert ? !boolValue : boolValue;
                var visibility = result ? Visibility.Visible : Visibility.Collapsed;

                //Logger.Info($"[Converter] value={boolValue}, invert={invert}, result={result}, visibility={visibility}");
                return visibility;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[Converter] Exception in BooleanToVisibilityConverter");
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility visibility && visibility == Visibility.Visible;
        }
    }
}