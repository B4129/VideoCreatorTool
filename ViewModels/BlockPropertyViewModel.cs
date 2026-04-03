using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using VideoCreatorWPF.Commands;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.ViewModels
{
    public class BlockPropertyViewModel : ViewModelBase
    {
        private TimelineBlock? _selectedBlock;
        private string _blockName = "";
        private int _startFrame;
        private int _duration;
        private double _volume = 1.0;
        private bool _isVisible = true;
        private double _playbackSpeed = 1.0;
        private double _opacity = 1.0;
        private bool _loop = false;
        private bool _isTextBlock;
        private bool _isAudioBlock;
        private bool _isVideoBlock;

        // テキストブロック用プロパティ
        private string _textContent = "";
        private string _fontFamily = "MJoy";
        private int _fontSize = 32;
        private string _fontColor = "#ffffff";
        private double _positionX = 50;
        private double _positionY = 85;
        private int _fadeInDuration;
        private int _fadeOutDuration;

        public BlockPropertyViewModel()
        {
            ApplyCommand = new RelayCommand(_ => ApplyChanges());
            FontFamilies = new ObservableCollection<string>
            {
                "MJoy",
                "Yu Gothic",
                "Meiryo",
                "MS Gothic",
                "Hiragino Kaku Gothic ProN",
                "Noto Sans JP",
                "源真ゴシック",
                "さざなみゴシック"
            };
        }

        public TimelineBlock? SelectedBlock
        {
            get => _selectedBlock;
            set
            {
                _selectedBlock = value;
                if (value != null)
                {
                    BlockName = value.Name ?? "";
                    StartFrame = value.StartFrame;
                    Duration = value.Duration;
                    IsVisible = value.IsVisible;
                    FadeInDuration = value.FadeInDuration;
                    FadeOutDuration = value.FadeOutDuration;

                    // Check block type
                    _isTextBlock = value.Type == BlockType.Dialogue || value.Type == BlockType.Subtitle;
                    _isAudioBlock = value.Type == BlockType.Audio;
                    _isVideoBlock = value.Type == BlockType.Video;

                    if (_isTextBlock)
                    {
                        TextContent = value.Text ?? "";
                        FontFamily = value.FontFamily ?? "MJoy";
                        FontSize = (int)value.FontSize;
                        FontColor = value.FontColor;
                        PositionX = value.TextPositionX;
                        PositionY = value.TextPositionY;
                        Volume = 1.0;
                        PlaybackSpeed = 1.0;
                        Opacity = 1.0;
                        Loop = false;
                    }
                    else if (_isAudioBlock)
                    {
                        Volume = value.Volume;
                        PlaybackSpeed = value.PlaybackSpeed;
                        Opacity = 1.0;
                        Loop = false;
                        TextContent = "";
                    }
                    else if (_isVideoBlock)
                    {
                        Volume = value.Volume;
                        PlaybackSpeed = value.PlaybackSpeed;
                        Opacity = value.Opacity;
                        Loop = value.Loop;
                        TextContent = "";
                    }
                    else
                    {
                        Volume = 1.0;
                        PlaybackSpeed = 1.0;
                        Opacity = 1.0;
                        Loop = false;
                        TextContent = "";
                    }
                }
                else
                {
                    BlockName = "";
                    StartFrame = 0;
                    Duration = 0;
                    Volume = 1.0;
                    PlaybackSpeed = 1.0;
                    Opacity = 1.0;
                    Loop = false;
                    IsVisible = true;

                    _isTextBlock = false;
                    _isAudioBlock = false;
                    _isVideoBlock = false;
                    TextContent = "";
                    FontFamily = "MJoy";
                    FontSize = 32;
                    FontColor = "#ffffff";
                    PositionX = 50;
                    PositionY = 85;
                    FadeInDuration = 0;
                    FadeOutDuration = 0;
                }
                OnPropertyChanged(nameof(SelectedBlock));
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(IsTextBlock));
                OnPropertyChanged(nameof(IsAudioBlock));
                OnPropertyChanged(nameof(IsVideoBlock));
            }
        }

        public bool HasSelection => _selectedBlock != null;

        public bool IsTextBlock
        {
            get => _isTextBlock;
            private set => SetProperty(ref _isTextBlock, value);
        }

        public bool IsAudioBlock
        {
            get => _isAudioBlock;
            private set => SetProperty(ref _isAudioBlock, value);
        }

        public bool IsVideoBlock
        {
            get => _isVideoBlock;
            private set => SetProperty(ref _isVideoBlock, value);
        }

        public ObservableCollection<string> FontFamilies { get; }

        public string BlockName
        {
            get => _blockName;
            set => SetProperty(ref _blockName, value);
        }

        public int StartFrame
        {
            get => _startFrame;
            set => SetProperty(ref _startFrame, value);
        }

        public int Duration
        {
            get => _duration;
            set => SetProperty(ref _duration, value);
        }

        public double Volume
        {
            get => _volume;
            set => SetProperty(ref _volume, value);
        }

        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            set => SetProperty(ref _playbackSpeed, value);
        }

        public double Opacity
        {
            get => _opacity;
            set => SetProperty(ref _opacity, value);
        }

        public bool Loop
        {
            get => _loop;
            set => SetProperty(ref _loop, value);
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        // テキストブロック用プロパティ
        public string TextContent
        {
            get => _textContent;
            set => SetProperty(ref _textContent, value);
        }

        public string FontFamily
        {
            get => _fontFamily;
            set => SetProperty(ref _fontFamily, value);
        }

        public int FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }

        public string FontColor
        {
            get => _fontColor;
            set => SetProperty(ref _fontColor, value);
        }

        public double PositionX
        {
            get => _positionX;
            set => SetProperty(ref _positionX, value);
        }

        public double PositionY
        {
            get => _positionY;
            set => SetProperty(ref _positionY, value);
        }

        public int FadeInDuration
        {
            get => _fadeInDuration;
            set => SetProperty(ref _fadeInDuration, value);
        }

        public int FadeOutDuration
        {
            get => _fadeOutDuration;
            set => SetProperty(ref _fadeOutDuration, value);
        }

        public ICommand ApplyCommand { get; }

        private void ApplyChanges()
        {
            if (_selectedBlock != null)
            {
                _selectedBlock.Name = BlockName;
                _selectedBlock.StartFrame = StartFrame;
                _selectedBlock.Duration = Duration;
                _selectedBlock.IsVisible = IsVisible;
                _selectedBlock.FadeInDuration = FadeInDuration;
                _selectedBlock.FadeOutDuration = FadeOutDuration;
                _selectedBlock.Volume = Volume;
                _selectedBlock.PlaybackSpeed = PlaybackSpeed;

                if (_isTextBlock)
                {
                    _selectedBlock.Text = TextContent;
                    _selectedBlock.FontFamily = FontFamily;
                    _selectedBlock.FontSize = FontSize;
                    _selectedBlock.FontColor = FontColor;
                    _selectedBlock.TextPositionX = PositionX;
                    _selectedBlock.TextPositionY = PositionY;
                }
                else if (_isVideoBlock)
                {
                    _selectedBlock.Opacity = Opacity;
                    _selectedBlock.Loop = Loop;
                }
            }
        }

        public void UpdateFromBlock(TimelineBlock block)
        {
            SelectedBlock = block;
        }
    }
}
