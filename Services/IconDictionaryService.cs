using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.Services
{
    /// <summary>
    /// インラインアイコン辞書サービス
    /// キャラクター単位のテキスト→画像置換ルールを管理
    /// </summary>
    public class IconDictionaryService
    {
        private readonly Dictionary<Guid, List<IconAlias>> _characterIcons = new();
        private static IconDictionaryService? _instance;

        public static IconDictionaryService Instance => _instance ??= new IconDictionaryService();

        private IconDictionaryService()
        {
        }

        /// <summary>
        /// キャラクターのアイコン辞書を登録
        /// </summary>
        public void RegisterCharacter(Guid characterId, IEnumerable<IconAlias> aliases)
        {
            var aliasList = aliases.ToList();

            // パターンをコンパイル
            foreach (var alias in aliasList)
            {
                alias.CompilePattern();
            }

            _characterIcons[characterId] = aliasList;
        }

        /// <summary>
        /// キャラクターのアイコン辞書を取得
        /// </summary>
        public List<IconAlias> GetCharacterIcons(Guid characterId)
        {
            return _characterIcons.TryGetValue(characterId, out var icons) ? icons : new();
        }

        /// <summary>
        /// アイコンエイリアスを追加
        /// </summary>
        public void AddIconAlias(Guid characterId, IconAlias alias)
        {
            if (!_characterIcons.ContainsKey(characterId))
            {
                _characterIcons[characterId] = new();
            }

            alias.CompilePattern();
            _characterIcons[characterId].Add(alias);
        }

        /// <summary>
        /// アイコンエイリアスを削除
        /// </summary>
        public void RemoveIconAlias(Guid characterId, string searchKey)
        {
            if (_characterIcons.TryGetValue(characterId, out var aliases))
            {
                aliases.RemoveAll(a => a.SearchKey == searchKey);
            }
        }

        /// <summary>
        /// テキストを解析して要素リストに変換
        /// </summary>
        public ParsedTextResult ParseText(Guid characterId, string text)
        {
            var result = new ParsedTextResult();
            var icons = GetCharacterIcons(characterId);

            if (icons.Count == 0 || string.IsNullOrEmpty(text))
            {
                result.Elements.Add(new ParsedTextElement { Text = text });
                return result;
            }

            // 有効なエイリアスのみ使用
            var activeAliases = icons.Where(a => a.IsEnabled && a.Pattern != null).ToList();

            if (activeAliases.Count == 0)
            {
                result.Elements.Add(new ParsedTextElement { Text = text });
                return result;
            }

            // 全てのマッチング位置を収集
            var matches = new List<(int Start, int End, IconAlias Alias)>();
            foreach (var alias in activeAliases)
            {
                var match = alias.GetMatch(text);
                while (match.Success)
                {
                    matches.Add((match.Index, match.Index + match.Length, alias));
                    match = match.NextMatch();
                }
            }

            // 位置でソート
            matches = matches.OrderBy(m => m.Start).ToList();

            // 重複を除去（最初にマッチしたものを優先）
            var filteredMatches = new List<(int Start, int End, IconAlias Alias)>();
            int lastEnd = 0;
            foreach (var match in matches)
            {
                if (match.Start >= lastEnd)
                {
                    filteredMatches.Add(match);
                    lastEnd = match.End;
                }
            }

            // 要素リストを構築
            int currentIndex = 0;
            foreach (var match in filteredMatches)
            {
                // マッチ前のテキスト
                if (match.Start > currentIndex)
                {
                    result.Elements.Add(new ParsedTextElement
                    {
                        Text = text.Substring(currentIndex, match.Start - currentIndex)
                    });
                }

                // アイコン
                result.Elements.Add(new ParsedTextElement
                {
                    IconPath = match.Alias.ImagePath,
                    IconAlias = match.Alias
                });

                currentIndex = match.End;
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
        /// テキスト内にアイコンが含まれているかチェック
        /// </summary>
        public bool ContainsIcon(Guid characterId, string text)
        {
            var icons = GetCharacterIcons(characterId);
            return icons.Any(a => a.IsEnabled && a.Matches(text));
        }

        /// <summary>
        /// 辞書をクリア
        /// </summary>
        public void Clear()
        {
            _characterIcons.Clear();
        }

        /// <summary>
        /// 設定を保存
        /// </summary>
        public void SaveSettings(string basePath)
        {
            var settings = new Dictionary<Guid, List<IconAliasSettings>>();

            foreach (var kvp in _characterIcons)
            {
                settings[kvp.Key] = kvp.Value.Select(a => new IconAliasSettings
                {
                    SearchKey = a.SearchKey,
                    ImagePath = a.ImagePath,
                    HeightScale = a.HeightScale,
                    OffsetX = a.OffsetX,
                    OffsetY = a.OffsetY,
                    IsEnabled = a.IsEnabled,
                    Description = a.Description
                }).ToList();
            }

            var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            var path = Path.Combine(basePath, "icon_dictionary.json");
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// 設定を読み込み
        /// </summary>
        public void LoadSettings(string basePath)
        {
            var path = Path.Combine(basePath, "icon_dictionary.json");
            if (!File.Exists(path)) return;

            try
            {
                var json = File.ReadAllText(path);
                var settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<Guid, List<IconAliasSettings>>>(json);

                if (settings == null) return;

                _characterIcons.Clear();
                foreach (var kvp in settings)
                {
                    var aliases = kvp.Value.Select(s => new IconAlias
                    {
                        SearchKey = s.SearchKey,
                        ImagePath = s.ImagePath,
                        HeightScale = s.HeightScale,
                        OffsetX = s.OffsetX,
                        OffsetY = s.OffsetY,
                        IsEnabled = s.IsEnabled,
                        Description = s.Description
                    }).ToList();

                    foreach (var alias in aliases)
                    {
                        alias.CompilePattern();
                    }

                    _characterIcons[kvp.Key] = aliases;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IconDictionaryService: Failed to load settings: {ex.Message}");
            }
        }

        // 保存用データクラス
        private class IconAliasSettings
        {
            public string SearchKey { get; set; } = "";
            public string ImagePath { get; set; } = "";
            public double HeightScale { get; set; } = 1.0;
            public double OffsetX { get; set; } = 0.0;
            public double OffsetY { get; set; } = 0.0;
            public bool IsEnabled { get; set; } = true;
            public string Description { get; set; } = "";
        }
    }
}
