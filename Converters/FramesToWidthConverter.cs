using System;
using System.Globalization;
using System.Windows.Data;

namespace VideoCreatorWPF.Converters
{
    public class FramesToWidthMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is int frames && values[1] is double pixelsPerFrame)
            {
                return frames * pixelsPerFrame;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // Width -> frames, pixelsPerFrame
            if (value is double width && targetTypes.Length >= 2)
            {
                return new object[] { 0, 0 }; // Not typically used in OneWay binding
            }
            return new object[] { 0, 0 };
        }
    }
}
