using System;
using System.Globalization;
using System.Windows.Data;

namespace VideoCreatorWPF.Converters
{
    [ValueConversion(typeof(bool), typeof(double))]
    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isVisible)
            {
                return isVisible ? 1.0 : 0.3;
            }
            return 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double opacity)
            {
                return opacity > 0.5;
            }
            return true;
        }
    }
}
