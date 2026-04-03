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
            // ApplyCommand removed - values are now reflected immediately
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
            set
            {
                if (SetProperty(ref _blockName, value))
                {
                    if (_selectedBlock != null) _selectedBlock.Name = value;
                }
            }
        }

        public int StartFrame
        {
            get => _startFrame;
            set
            {
                if (SetProperty(ref _startFrame, value))
                {
                    if (_selectedBlock != null) _selectedBlock.StartFrame = value;
                }
            }
        }

        public int Duration
        {
            get => _duration;
            set
            {
                if (SetProperty(ref _duration, value))
                {
                    if (_selectedBlock != null) _selectedBlock.Duration = value;
                }
            }
        }

        public double Volume
        {
            get => _volume;
            set
            {
                if (SetProperty(ref _volume, value))
                {
                    if (_selectedBlock != null) _selectedBlock.Volume = value;
                }
            }
        }

        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            set
            {
                if (SetProperty(ref _playbackSpeed, value))
                {
                    if (_selectedBlock != null) _selectedBlock.PlaybackSpeed = value;
                }
            }
        }

        public double Opacity
        {
            get => _opacity;
            set
            {
                if (SetProperty(ref _opacity, value))
                {
                    if (_selectedBlock != null) _selectedBlock.Opacity = value;
                }
            }
        }

        public bool Loop
        {
            get => _loop;
            set
            {
                if (SetProperty(ref _loop, value))
                {
                    if (_selectedBlock != null) _selectedBlock.Loop = value;
                }
            }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (SetProperty(ref _isVisible, value))
                {
                    if (_selectedBlock != null) _selectedBlock.IsVisible = value;
                }
            }
        }

        // テキストブロック用プロパティ
        public string TextContent
        {
            get => _textContent;
            set
            {
                if (SetProperty(ref _textContent, value))
                {
                    if (_selectedBlock != null) _selectedBlock.Text = value;
                }
            }
        }

        public string FontFamily
        {
            get => _fontFamily;
            set
            {
                if (SetProperty(ref _fontFamily, value))
                {
                    if (_selectedBlock != null) _selectedBlock.FontFamily = value;
                }
            }
        }

        public int FontSize
        {
            get => _fontSize;
            set
            {
                if (SetProperty(ref _fontSize, value))
                {
                    if (_selectedBlock != null) _selectedBlock.FontSize = value;
                }
            }
        }

        public string FontColor
        {
            get => _fontColor;
            set
            {
                if (SetProperty(ref _fontColor, value))
                {
                    if (_selectedBlock != null) _selectedBlock.FontColor = value;
                }
            }
        }

        public double PositionX
        {
            get => _positionX;
            set
            {
                if (SetProperty(ref _positionX, value))
                {
                    if (_selectedBlock != null) _selectedBlock.TextPositionX = value;
                }
            }
        }

        public double PositionY
        {
            get => _positionY;
            set
            {
                if (SetProperty(ref _positionY, value))
                {
                    if (_selectedBlock != null) _selectedBlock.TextPositionY = value;
                }
            }
        }

        public int FadeInDuration
        {
            get => _fadeInDuration;
            set
            {
                if (SetProperty(ref _fadeInDuration, value))
                {
                    if (_selectedBlock != null) _selectedBlock.FadeInDuration = value;
                }
            }
        }

        public int FadeOutDuration
        {
            get => _fadeOutDuration;
            set
            {
                if (SetProperty(ref _fadeOutDuration, value))
                {
                    if (_selectedBlock != null) _selectedBlock.FadeOutDuration = value;
                }
            }
        }

        public void UpdateFromBlock(TimelineBlock block)
        {
            SelectedBlock = block;
        }
    }
}
