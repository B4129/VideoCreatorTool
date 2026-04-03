using System;
using System.Collections.ObjectModel;

namespace VideoCreatorWPF.Models
{
    public class Character
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public int SpeakerId { get; set; } = 2; // VOICEVOXデフォルト話者
        public string? ImagePath { get; set; }
        public string? Color { get; set; } = "#3b82f6";
        public double Volume { get; set; } = 1.0;
        public double Speed { get; set; } = 1.0;
        public double Pitch { get; set; } = 1.0;
        public bool IsMuted { get; set; }
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

    public class TimelineBlock
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CharacterId { get; set; }
        public int TrackIndex { get; set; }
        public int StartFrame { get; set; }
        public int Duration { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? AudioPath { get; set; }
        public string? BackgroundColor { get; set; }
        public BlockType Type { get; set; } = BlockType.Dialogue;

        // Text formatting properties
        private string _fontFamily = "Yu Gothic UI";
        private double _fontSize = 48.0;
        private string _textColor = "#FFFFFF";

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

        // Subtitle positioning
        private double _textPositionX = 50.0; // percentage (0-100)
        private double _textPositionY = 85.0; // percentage (0-100)
        private double _textOutlineWidth = 2.0;
        private string _textOutlineColor = "#000000";
        private bool _hasShadow = true;
        private int _fadeInFrames = 0;
        private int _fadeOutFrames = 0;

        // Audio fade effects
        private int _audioFadeInFrames = 0;
        private int _audioFadeOutFrames = 0;

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

        // Selection and interaction state (not serialized)
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsSelected { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsBeingDragged { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int DragStartFrame { get; set; }

        // Track association
        public Guid TrackId { get; set; }

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
}
