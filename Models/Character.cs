using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VideoCreatorWPF.Models
{
    public class Character : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public int SpeakerId { get; set; } = 2; // VOICEVOXデフォルト話者
        public int StyleId { get; set; } = 0; // スタイルID
        public string? ImagePath { get; set; }
        public string? Color { get; set; } = "#3b82f6";

        // 音声合成パラメータ
        private double _volume = 1.0;
        private double _speed = 1.0;
        private double _pitch = 1.0;
        private double _intonation = 1.0;
        private int _startSilenceMs = 200;
        private int _endSilenceMs = 200;

        /// <summary>
        /// 音量 (0.0 - 2.0)
        /// </summary>
        public double Volume
        {
            get => _volume;
            set => SetProperty(ref _volume, Math.Max(0.0, Math.Min(2.0, value)));
        }

        /// <summary>
        /// 話速 (0.5 - 2.0, デフォルト1.0)
        /// </summary>
        public double Speed
        {
            get => _speed;
            set => SetProperty(ref _speed, Math.Max(0.5, Math.Min(2.0, value)));
        }

        /// <summary>
        /// 音高 (-0.15 - 0.15, デフォルト0.0)
        /// </summary>
        public double Pitch
        {
            get => _pitch;
            set => SetProperty(ref _pitch, Math.Max(-0.15, Math.Min(0.15, value)));
        }

        /// <summary>
        /// 抑揚 (0.0 - 2.0, デフォルト1.0)
        /// 0に近づけるとロボットっぽく、上げると感情豊か
        /// </summary>
        public double Intonation
        {
            get => _intonation;
            set => SetProperty(ref _intonation, Math.Max(0.0, Math.Min(2.0, value)));
        }

        /// <summary>
        /// 開始無音（ミリ秒）
        /// </summary>
        public int StartSilenceMs
        {
            get => _startSilenceMs;
            set => SetProperty(ref _startSilenceMs, Math.Max(0, Math.Min(2000, value)));
        }

        /// <summary>
        /// 終了無音（ミリ秒）
        /// </summary>
        public int EndSilenceMs
        {
            get => _endSilenceMs;
            set => SetProperty(ref _endSilenceMs, Math.Max(0, Math.Min(2000, value)));
        }

        public bool IsMuted { get; set; }

        /// <summary>
        /// インラインアイコン（テキスト→画像置換）の辞書
        /// </summary>
        public ObservableCollection<IconAlias> IconAliases { get; set; } = new();

        /// <summary>
        /// 利用可能なスタイル一覧
        /// </summary>
        public ObservableCollection<VoiceStyle> AvailableStyles { get; set; } = new();
    }

    /// <summary>
    /// VOICEVOXのスタイル情報
    /// </summary>
    public class VoiceStyle : INotifyPropertyChanged
    {
        private int _id;
        private string _name = "";
        private string _displayName = "";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        public override string ToString() => DisplayName;
    }

    public class Scene
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public double FrameRate { get; set; } = 30;
        public ObservableCollection<TimelineBlock> Blocks { get; set; } = new();
    }

    public class TimelineBlock : INotifyPropertyChanged
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CharacterId { get; set; }
        public int TrackIndex { get; set; }
        public Guid TrackId { get; set; }

        public TimelineBlock()
        {
            // Set default values for all properties
            _volume = 1.0;
            _playbackSpeed = 1.0;
            _opacity = 1.0;
            _loop = false;
            _fontFamily = "MJoy";
            _fontSize = 32.0;
            _fontColor = "#ffffff";
            _textColor = "#FFFFFF";
            _textPositionX = 50.0;
            _textPositionY = 85.0;
            _fadeInFrames = 0;
            _fadeOutFrames = 0;
            _audioFadeInFrames = 0;
            _audioFadeOutFrames = 0;
        }

        private int _startFrame;
        public int StartFrame
        {
            get => _startFrame;
            set
            {
                if (_startFrame != value)
                {
                    _startFrame = value;
                    OnPropertyChanged(nameof(StartFrame));
                }
            }
        }

        private int _duration;
        public int Duration
        {
            get => _duration;
            set
            {
                if (_duration != value)
                {
                    _duration = value;
                    OnPropertyChanged(nameof(Duration));
                }
            }
        }

        public string Text { get; set; } = string.Empty;
        public string? AudioPath { get; set; }
        public string? VideoPath { get; set; }
        public string? BackgroundColor { get; set; }
        public string Name { get; set; } = "";
        public bool IsVisible { get; set; } = true;
        public BlockType Type { get; set; } = BlockType.Dialogue;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Text formatting properties
        private string _fontFamily = "MJoy";
        private double _fontSize = 32.0;
        private string _textColor = "#FFFFFF";
        private string _fontColor = "#ffffff";
        private double _textPositionX = 50.0; // percentage (0-100)
        private double _textPositionY = 85.0; // percentage (0-100)
        private double _textOutlineWidth = 2.0;
        private string _textOutlineColor = "#000000";
        private bool _hasShadow = true;
        private int _fadeInFrames = 0;
        private int _fadeOutFrames = 0;

        // Audio/Video properties
        private double _volume = 1.0;
        private double _playbackSpeed = 1.0;
        private double _opacity = 1.0;
        private bool _loop = false;

        // Audio fade effects
        private int _audioFadeInFrames = 0;
        private int _audioFadeOutFrames = 0;

        public string FontFamily
        {
            get => _fontFamily;
            set => _fontFamily = value;
        }

        public double FontSize
        {
            get => _fontSize;
            set => _fontSize = value;
        }

        public string TextColor
        {
            get => _textColor;
            set => _textColor = value;
        }

        public string FontColor
        {
            get => _fontColor;
            set => _fontColor = value;
        }

        /// <summary>
        /// Text horizontal position as percentage (0-100)
        /// </summary>
        public double TextPositionX
        {
            get => _textPositionX;
            set => _textPositionX = value;
        }

        /// <summary>
        /// Text vertical position as percentage (0-100, 0=top, 100=bottom)
        /// </summary>
        public double TextPositionY
        {
            get => _textPositionY;
            set => _textPositionY = value;
        }

        public double PositionX
        {
            get => _textPositionX;
            set => _textPositionX = value;
        }

        public double PositionY
        {
            get => _textPositionY;
            set => _textPositionY = value;
        }

        /// <summary>
        /// Text outline width in pixels
        /// </summary>
        public double TextOutlineWidth
        {
            get => _textOutlineWidth;
            set => _textOutlineWidth = value;
        }

        /// <summary>
        /// Text outline color in hex
        /// </summary>
        public string TextOutlineColor
        {
            get => _textOutlineColor;
            set => _textOutlineColor = value;
        }

        /// <summary>
        /// Whether to draw a shadow behind the text
        /// </summary>
        public bool HasShadow
        {
            get => _hasShadow;
            set => _hasShadow = value;
        }

        /// <summary>
        /// Fade in duration in frames
        /// </summary>
        public int FadeInFrames
        {
            get => _fadeInFrames;
            set => _fadeInFrames = value;
        }

        /// <summary>
        /// Fade out duration in frames
        /// </summary>
        public int FadeOutFrames
        {
            get => _fadeOutFrames;
            set => _fadeOutFrames = value;
        }

        public int FadeInDuration
        {
            get => _fadeInFrames;
            set => _fadeInFrames = value;
        }

        public int FadeOutDuration
        {
            get => _fadeOutFrames;
            set => _fadeOutFrames = value;
        }

        /// <summary>
        /// Audio fade in duration in frames
        /// </summary>
        public int AudioFadeInFrames
        {
            get => _audioFadeInFrames;
            set => _audioFadeInFrames = value;
        }

        /// <summary>
        /// Audio fade out duration in frames
        /// </summary>
        public int AudioFadeOutFrames
        {
            get => _audioFadeOutFrames;
            set => _audioFadeOutFrames = value;
        }

        // Audio/Video properties
        public double Volume
        {
            get => _volume;
            set => _volume = value;
        }

        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            set => _playbackSpeed = value;
        }

        public double Opacity
        {
            get => _opacity;
            set => _opacity = value;
        }

        public bool Loop
        {
            get => _loop;
            set => _loop = value;
        }

        // Selection and interaction state (not serialized)
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsSelected { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsBeingDragged { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int DragStartFrame { get; set; }

        // Waveform data (not serialized)
        [System.Text.Json.Serialization.JsonIgnore]
        public ViewModels.WaveformDisplayData? WaveformData { get; set; }

        // Audio block indicator
        public bool IsAudioBlock => !string.IsNullOrEmpty(AudioPath);
    }

    public enum BlockType
    {
        Dialogue,       // テキスト（黄緑）
        Image,
        Video,          // 動画（青）
        Effect,
        Subtitle,
        Audio           // 音声（赤）
    }

    public class TimelineTrack
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public Guid? CharacterId { get; set; }
        public string? BlockColor { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsLocked { get; set; }
        public bool IsEnabled { get; set; } = true;
        public ObservableCollection<TimelineBlock> Items { get; set; } = new();
    }

    /// <summary>
    /// タイムラインマーカー
    /// </summary>
    public class TimelineMarker
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int Frame { get; set; }
        public string Name { get; set; } = "";
        public string Color { get; set; } = "#FF0000";
        public string Comment { get; set; } = "";
    }
}
