using System;
using System.Windows.Media;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.ViewModels
{
    public class MediaItemViewModel : ViewModelBase
    {
        private readonly MediaItem _mediaItem;
        private ImageSource? _thumbnail;
        private bool _isSelected;

        public MediaItemViewModel(MediaItem mediaItem)
        {
            _mediaItem = mediaItem;
        }

        public Guid Id => _mediaItem.Id;

        public string DisplayName
        {
            get
            {
                var name = _mediaItem.Name;
                if (name.Length > 12)
                {
                    return name.Substring(0, 10) + "...";
                }
                return name;
            }
        }

        public string FullDisplayName => _mediaItem.Name;

        public MediaType Type => _mediaItem.Type;

        public string FilePath => _mediaItem.FilePath;

        public double? Duration => _mediaItem.Duration;

        public int? Width => _mediaItem.Width;

        public int? Height => _mediaItem.Height;

        public ImageSource? Thumbnail
        {
            get => _thumbnail ?? _mediaItem.Thumbnail;
            set
            {
                _thumbnail = value;
                OnPropertyChanged(nameof(Thumbnail));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                _mediaItem.IsSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public string DurationText
        {
            get
            {
                if (!_mediaItem.Duration.HasValue) return "--:--";
                var ts = TimeSpan.FromSeconds(_mediaItem.Duration.Value);
                if (ts.TotalHours >= 1)
                {
                    return ts.ToString(@"h\:mm\:ss");
                }
                return ts.ToString(@"mm\:ss");
            }
        }

        public string ResolutionText
        {
            get
            {
                if (!_mediaItem.Width.HasValue || !_mediaItem.Height.HasValue) return string.Empty;
                return $"{_mediaItem.Width.Value}x{_mediaItem.Height.Value}";
            }
        }

        public DateTime AddedAt => _mediaItem.AddedAt;

        // Convenience properties for XAML bindings
        public bool IsVideo => _mediaItem.Type == MediaType.Video;
        public bool IsImage => _mediaItem.Type == MediaType.Image;
        public bool IsAudio => _mediaItem.Type == MediaType.Audio;
        public bool HasThumbnail => Thumbnail != null || _mediaItem.Thumbnail != null;

        /// <summary>
        /// メタ情報更新後にプロパティ変更を通知
        /// </summary>
        public void RefreshMetadata()
        {
            OnPropertyChanged(nameof(Duration));
            OnPropertyChanged(nameof(DurationText));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(ResolutionText));
        }
    }
}
