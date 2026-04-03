using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VideoCreatorWPF.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Brushes.Green : Brushes.Red;
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not typically used in OneWay binding
            return false;
        }
    }
}
