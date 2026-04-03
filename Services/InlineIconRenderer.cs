using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.Services
{
    /// <summary>
    /// インラインアイコンレンダラー
    /// WPF TextBlockにInlineUIContainerを使用して画像を挿入
    /// </summary>
    public static class InlineIconRenderer
    {
        /// <summary>
        /// TextBlockに解析済み要素リストを描画
        /// </summary>
        public static void RenderToTextBlock(TextBlock textBlock, List<ParsedTextElement> elements, double fontSize)
        {
            textBlock.Inlines.Clear();

            if (elements == null || elements.Count == 0)
            {
                return;
            }

            foreach (var element in elements)
            {
                if (element.IsIcon && element.IconPath != null)
                {
                    // アイコン描画
                    var image = CreateIconImage(element.IconPath, element.IconAlias, fontSize);
                    if (image != null)
                    {
                        var container = new InlineUIContainer(image);
                        textBlock.Inlines.Add(container);
                    }
                }
                else
                {
                    // テキスト描画
                    textBlock.Inlines.Add(new Run(element.Text));
                }
            }
        }

        /// <summary>
        /// アイコン画像を作成
        /// </summary>
        private static Image? CreateIconImage(string imagePath, IconAlias? alias, double fontSize)
        {
            try
            {
                if (!System.IO.File.Exists(imagePath))
                {
                    return null;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                // サイズ計算
                var height = fontSize * (alias?.HeightScale ?? 1.0);
                var width = height * (bitmap.PixelWidth / (double)bitmap.PixelHeight);

                var image = new Image
                {
                    Source = bitmap,
                    Height = height,
                    Width = width,
                    Margin = new Thickness(
                        alias?.OffsetX ?? 0,
                        alias?.OffsetY ?? 0,
                        0,
                        0
                    )
                };

                return image;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InlineIconRenderer: Failed to load image: {imagePath}, Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// シンプルなテキスト描画（アイコンなし）
        /// </summary>
        public static void RenderSimpleText(TextBlock textBlock, string text, string fontFamily, double fontSize, string fontColor)
        {
            textBlock.Inlines.Clear();
            textBlock.Inlines.Add(new Run(text));
        }

        /// <summary>
        /// 要素リストから描画すべきアイコンのパス一覧を取得（エクスポート用）
        /// </summary>
        public static List<(string Path, double X, double Y, double Width, double Height)> GetIconPositions(
            List<ParsedTextElement> elements,
            double baseX,
            double baseY,
            double fontSize,
            double textWidth)
        {
            var positions = new List<(string Path, double X, double Y, double Width, double Height)>();
            double currentX = baseX;

            foreach (var element in elements)
            {
                if (element.IsIcon && element.IconPath != null)
                {
                    var alias = element.IconAlias;
                    var height = fontSize * (alias?.HeightScale ?? 1.0);

                    // アスペクト比を維持（デフォルト1:1）
                    var width = height;

                    positions.Add((
                        element.IconPath,
                        currentX + (alias?.OffsetX ?? 0),
                        baseY + (alias?.OffsetY ?? 0),
                        width,
                        height
                    ));

                    currentX += width;
                }
                else
                {
                    // テキスト幅を加算（簡易計算）
                    currentX += element.Text.Length * fontSize * 0.5;
                }
            }

            return positions;
        }
    }
}
