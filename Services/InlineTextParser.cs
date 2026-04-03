using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.Services
{
    /// <summary>
    /// インラインテキストパーサー
    /// テキスト内のタグ（[tag] または :tag:）を検出してアイコンに置換するための要素を生成
    /// </summary>
    public static class InlineTextParser
    {
        // タグパターン：[xxx] または :xxx:
        private static readonly Regex TagPattern = new(@"(\[[^\]]+\])|(:[^:]+:)", RegexOptions.Compiled);

        /// <summary>
        /// テキストを解析して要素リストに変換（辞書ベース）
        /// </summary>
        public static ParsedTextResult Parse(Guid characterId, string text)
        {
            return IconDictionaryService.Instance.ParseText(characterId, text);
        }

        /// <summary>
        /// テキスト内にアイコンが含まれているかチェック（辞書ベース）
        /// </summary>
        public static bool ContainsIcon(Guid characterId, string text)
        {
            return IconDictionaryService.Instance.ContainsIcon(characterId, text);
        }

        /// <summary>
        /// テキストを解析して要素リストに変換（シンプルなタグ置換、辞書未登録時用）
        /// </summary>
        public static ParsedTextResult ParseSimple(string text, Func<string, string?> imageResolver)
        {
            var result = new ParsedTextResult();

            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            var matches = TagPattern.Matches(text);

            if (matches.Count == 0)
            {
                result.Elements.Add(new ParsedTextElement { Text = text });
                return result;
            }

            int currentIndex = 0;
            foreach (Match match in matches)
            {
                // マッチ前のテキスト
                if (match.Index > currentIndex)
                {
                    result.Elements.Add(new ParsedTextElement
                    {
                        Text = text.Substring(currentIndex, match.Index - currentIndex)
                    });
                }

                // タグを抽出
                var tag = match.Value;
                var iconName = tag.Trim('[', ']', ':');

                // 画像パスを解決
                var imagePath = imageResolver(iconName);

                if (imagePath != null)
                {
                    result.Elements.Add(new ParsedTextElement
                    {
                        IconPath = imagePath
                    });
                }
                else
                {
                    // 画像が見つからない場合はそのままテキスト
                    result.Elements.Add(new ParsedTextElement { Text = tag });
                }

                currentIndex = match.Index + match.Length;
            }

            // 残りのテキスト
            if (currentIndex < text.Length)
            {
                result.Elements.Add(new ParsedTextElement
                {
                    Text = text.Substring(currentIndex)
                });
            }

            return result;
        }

        /// <summary>
        /// タグ形式かチェック
        /// </summary>
        public static bool IsTagFormat(string text)
        {
            return TagPattern.IsMatch(text);
        }

        /// <summary>
        /// 利用可能なタグ一覧を取得
        /// </summary>
        public static List<string> GetAvailableTags(Guid characterId)
        {
            var icons = IconDictionaryService.Instance.GetCharacterIcons(characterId);
            return icons.Where(a => a.IsEnabled).Select(a => a.SearchKey).ToList();
        }
    }
}
