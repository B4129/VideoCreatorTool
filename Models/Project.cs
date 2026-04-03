using System;
using System.Collections.ObjectModel;

namespace VideoCreatorWPF.Models
{
    public class VideoProject
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        private int _width = 1920;
        private int _height = 1080;
        private double _frameRate = 30;
        private string _backgroundColor = "#000000";
        private double _aspectRatio = 16.0 / 9.0;
        private string? _backgroundMusicPath;
        private double _bgmVolume = 0.5;
        private double _bgmFadeIn = 0;
        private double _bgmFadeOut = 0;
        private bool _bgmLoop = true;

        public int Width
        {
            get => _width;
            set => _width = value;
        }

        public int Height
        {
            get => _height;
            set => _height = value;
        }

        public double FrameRate
        {
            get => _frameRate;
            set => _frameRate = value;
        }

        public string BackgroundColor
        {
            get => _backgroundColor;
            set => _backgroundColor = value;
        }

        public double AspectRatio
        {
            get => _aspectRatio;
            set => _aspectRatio = value;
        }

        public string? BackgroundMusicPath
        {
            get => _backgroundMusicPath;
            set => _backgroundMusicPath = value;
        }

        public double BgmVolume
        {
            get => _bgmVolume;
            set => _bgmVolume = value;
        }

        public double BGMFadeIn
        {
            get => _bgmFadeIn;
            set => _bgmFadeIn = value;
        }

        public double BGMFadeOut
        {
            get => _bgmFadeOut;
            set => _bgmFadeOut = value;
        }

        public bool BGMLoop
        {
            get => _bgmLoop;
            set => _bgmLoop = value;
        }

        public ObservableCollection<Character> Characters { get; } = new();
        public ObservableCollection<TimelineTrack> Tracks { get; } = new();
        public ObservableCollection<MediaItem> MediaPool { get; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ModifiedAt { get; set; } = DateTime.Now;
    }

    public class AppSettings
    {
        private string _voiceVoxPath = string.Empty;
        private string? _outputDirectory;
        private int _defaultWidth = 1920;
        private int _defaultHeight = 1080;
        private double _defaultFrameRate = 30;
        private string _theme = "Dark";
        private bool _autoSave = true;
        private int _autoSaveInterval = 5;

        public string VoiceVoxPath
        {
            get => _voiceVoxPath;
            set => _voiceVoxPath = value;
        }

        public string? OutputDirectory
        {
            get => _outputDirectory;
            set => _outputDirectory = value;
        }

        public int DefaultWidth
        {
            get => _defaultWidth;
            set => _defaultWidth = value;
        }

        public int DefaultHeight
        {
            get => _defaultHeight;
            set => _defaultHeight = value;
        }

        public double DefaultFrameRate
        {
            get => _defaultFrameRate;
            set => _defaultFrameRate = value;
        }

        public string Theme
        {
            get => _theme;
            set => _theme = value;
        }

        public bool AutoSave
        {
            get => _autoSave;
            set => _autoSave = value;
        }

        public int AutoSaveInterval
        {
            get => _autoSaveInterval;
            set => _autoSaveInterval = value;
        }
    }
}
