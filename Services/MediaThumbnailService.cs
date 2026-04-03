using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VideoCreatorWPF.Services
{
    /// <summary>
    /// メディアファイルのサムネイルを生成するサービス
    /// </summary>
    public static class MediaThumbnailService
    {
        public static ImageSource? GenerateThumbnail(string filePath, Models.MediaType mediaType, int width = 80, int height = 60)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Debug.WriteLine($"ファイルが見つかりません: {filePath}");
                    return null;
                }

                return mediaType switch
                {
                    Models.MediaType.Video => CreateColorThumbnail(width, height, "#1a3a5c", "▶"),
                    Models.MediaType.Image => CreateImageThumbnail(filePath, width, height),
                    Models.MediaType.Audio => CreateColorThumbnail(width, height, "#5c1a3a", "♪"),
                    _ => CreateColorThumbnail(width, height, "#333333", "?")
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"サムネイル生成エラー: {ex.Message}");
                return null;
            }
        }

        private static ImageSource? CreateImageThumbnail(string imagePath, int width, int height)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath);
                bitmap.DecodePixelWidth = width;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"画像サムネイル生成エラー: {ex.Message}");
                return CreateColorThumbnail(width, height, "#2d5c1a", "IMG");
            }
        }

        private static ImageSource CreateColorThumbnail(int width, int height, string colorHex, string icon)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);

            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                var brush = new SolidColorBrush(color);
                context.DrawRectangle(brush, null, new System.Windows.Rect(0, 0, width, height));

                var text = new FormattedText(icon,
                    System.Globalization.CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    20, Brushes.White, 1.0);
                var textX = (width - text.Width) / 2;
                var textY = (height - text.Height) / 2;
                context.DrawText(text, new System.Windows.Point(textX, textY));
            }

            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
