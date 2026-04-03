using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;

namespace VideoCreatorWPF.Models
{
    public enum MediaType
    {
        Video,
        Image,
        Audio
    }

    public class MediaItem : INotifyPropertyChanged
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public MediaType Type { get; set; }

        public string FilePath { get; set; } = string.Empty;

        public double? Duration { get; set; }

        public int? Width { get; set; }

        public int? Height { get; set; }

        public string? ThumbnailPath { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.Now;

        public ObservableCollection<string> Tags { get; } = new ObservableCollection<string>();

        [JsonIgnore]
        private ImageSource? _thumbnail;
        [JsonIgnore]
        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set
            {
                _thumbnail = value;
                OnPropertyChanged(nameof(Thumbnail));
            }
        }

        [JsonIgnore]
        private bool _isSelected;
        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
