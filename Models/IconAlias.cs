using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VideoCreatorWPF.Models
{
    /// <summary>
    /// インラインアイコン（テキスト→画像置換）の定義
    /// [tag] または :tag: 形式のテキストを画像に置換するためのルール
    /// </summary>
    public class IconAlias : ViewModelBase
    {
        private string _searchKey = "";
        private string _imagePath = "";
        private double _heightScale = 1.0;
        private double _offsetX = 0.0;
        private double _offsetY = 0.0;
        private bool _isEnabled = true;
        private string _description = "";

        /// <summary>
        /// 検索キー（例：[笑], :apple:）
        /// </summary>
        public string SearchKey
        {
            get => _searchKey;
            set => SetProperty(ref _searchKey, value);
        }

        /// <summary>
        /// 置換画像のパス
        /// </summary>
        public string ImagePath
        {
            get => _imagePath;
            set => SetProperty(ref _imagePath, value);
        }

        /// <summary>
        /// 高さ倍率（1.0 = フォントサイズと同じ）
        /// </summary>
        public double HeightScale
        {
            get => _heightScale;
            set => SetProperty(ref _heightScale, value);
        }

        /// <summary>
        /// X軸オフセット（ピクセル）
        /// </summary>
        public double OffsetX
        {
            get => _offsetX;
            set => SetProperty(ref _offsetX, value);
        }

        /// <summary>
        /// Y軸オフセット（ピクセル、下向き正）
        /// </summary>
        public double OffsetY
        {
            get => _offsetY;
            set => SetProperty(ref _offsetY, value);
        }

        /// <summary>
        /// 有効/無効フラグ
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        /// <summary>
        /// 説明（管理用）
        /// </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// 正規表現パターン（内部使用）
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public Regex Pattern { get; private set; }

        /// <summary>
        /// パターンをコンパイル
        /// </summary>
        public void CompilePattern()
        {
            try
            {
                // 検索キーを正規表現エスケープ
                var escaped = Regex.Escape(_searchKey);
                Pattern = new Regex(escaped, RegexOptions.Compiled);
            }
            catch
            {
                Pattern = null;
            }
        }

        /// <summary>
        /// テキスト内に検索キーが含まれているかチェック
        /// </summary>
        public bool Matches(string text)
        {
            if (!IsEnabled || Pattern == null) return false;
            return Pattern.IsMatch(text);
        }

        /// <summary>
        /// マッチした位置と長さを取得
        /// </summary>
        public Match GetMatch(string text)
        {
            if (Pattern == null) return null;
            return Pattern.Match(text);
        }
    }

    /// <summary>
    /// 解析されたテキスト要素（テキストまたはアイコン）
    /// </summary>
    public class ParsedTextElement
    {
        public string Text { get; set; } = "";
        public string? IconPath { get; set; }
        public IconAlias? IconAlias { get; set; }
        public bool IsIcon => IconPath != null;
    }

    /// <summary>
    /// 解析結果
    /// </summary>
    public class ParsedTextResult
    {
        public List<ParsedTextElement> Elements { get; set; } = new();
        public bool HasIcons => Elements.Any(e => e.IsIcon);
    }
}
