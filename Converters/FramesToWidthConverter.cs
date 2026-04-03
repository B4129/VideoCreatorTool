using System;
using System.Globalization;
using System.Windows.Data;

namespace VideoCreatorWPF.Converters
{
    /// <summary>
    /// フレーム数をピクセル幅に変換するコンバーター（単一値・MultiBinding両対応）
    /// </summary>
    public class FramesToWidthMultiConverter : IMultiValueConverter, IValueConverter
    {
        // IMultiValueConverter implementation
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
            if (value is double width && targetTypes.Length >= 2)
            {
                return new object[] { 0, 0 };
            }
            return new object[] { 0, 0 };
        }

        // IValueConverter implementation (for single value binding with PixelsPerFrame from DataContext)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // value is Duration (frames)
            // PixelsPerFrame needs to come from binding context or parameter
            // For simplicity, we use a default value or expect parameter to be PixelsPerFrame
            if (value is int frames)
            {
                double pixelsPerFrame = 1.0; // default
                if (parameter is double p)
                {
                    pixelsPerFrame = p;
                }
                return frames * pixelsPerFrame;
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                double pixelsPerFrame = 1.0;
                if (parameter is double p)
                {
                    pixelsPerFrame = p;
                }
                return (int)(width / pixelsPerFrame);
            }
            return 0;
        }
    }
}
