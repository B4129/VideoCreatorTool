using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;

namespace VideoCreatorWPF.Converters
{
    /// <summary>
    /// Converts percentage (0-100) to pixel position based on container size
    /// </summary>
    public class PercentageToPixelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage && parameter is Grid grid)
            {
                // Convert percentage to pixels
                // TextPositionX/Y are in percentage (0-100), convert to pixels
                return percentage / 100.0 * (targetType == typeof(double) ? 1.0 : 1.0);
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double pixels && parameter is Grid grid)
            {
                // Convert pixels back to percentage (0-100)
                // This is used when dragging to update the percentage value
                return pixels;
            }
            return 0.0;
        }
    }

    /// <summary>
    /// Converts X/Y pixel delta to percentage delta for text position updates
    /// </summary>
    public class PixelToPercentageDeltaConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] = pixelDeltaX, values[1] = containerWidth, values[2] = currentPercentageX
            if (values.Length >= 3 && values[0] is double pixelDelta && values[1] is double containerSize)
            {
                // Convert pixel delta to percentage delta
                var percentageDelta = (pixelDelta / containerSize) * 100.0;
                return percentageDelta;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
