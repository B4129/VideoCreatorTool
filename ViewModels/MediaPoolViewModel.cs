using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using VideoCreatorWPF.Commands;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.ViewModels
{
    public class MediaPoolViewModel : ViewModelBase
    {
        private readonly VideoProject _project;
        private ObservableCollection<MediaItemViewModel> _mediaItems;
        private MediaItemViewModel? _selectedItem;
        private MediaType? _filterType;
        private bool _isAllFilterChecked = true;

        public MediaPoolViewModel(VideoProject project)
        {
            _project = project;
            _mediaItems = new ObservableCollection<MediaItemViewModel>(
                _project.MediaPool.Select(m => new MediaItemViewModel(m)));

            ImportCommand = new RelayCommand(_ => ImportMedia());
            DeleteCommand = new RelayCommand(_ => DeleteSelectedItem(), _ => SelectedItem != null);
            ClearFilterCommand = new RelayCommand(_ => ClearFilter());
            SetFilterCommand = new RelayCommand(param => SetFilter(param));
            SelectItemCommand = new RelayCommand(param => SelectItem(param));

            // Listen for media pool changes
            _project.MediaPool.CollectionChanged += (s, e) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    RefreshMediaItems();
                });
            };
        }

        public ObservableCollection<MediaItemViewModel> MediaItems
        {
            get => _mediaItems;
            set => SetProperty(ref _mediaItems, value);
        }

        public MediaItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public MediaType? FilterType
        {
            get => _filterType;
            set
            {
                if (SetProperty(ref _filterType, value))
                {
                    ApplyFilter();
                }
            }
        }

        public ICommand ImportCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearFilterCommand { get; }
        public ICommand SetFilterCommand { get; }
        public ICommand SelectItemCommand { get; }

        public bool IsAllFilterChecked
        {
            get => _isAllFilterChecked;
            set
            {
                if (SetProperty(ref _isAllFilterChecked, value) && value)
                {
                    ClearFilter();
                }
            }
        }

        private void ImportMedia()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "メディアファイル|*.mp4;*.avi;*.mov;*.wmv;*.mkv;*.webm;*.png;*.jpg;*.jpeg;*.bmp;*.wav;*.mp3;*.ogg;*.m4a|動画|*.mp4;*.avi;*.mov;*.wmv;*.mkv;*.webm|画像|*.png;*.jpg;*.jpeg;*.bmp|音声|*.wav;*.mp3;*.ogg;*.m4a",
                Multiselect = true,
                Title = "メディアファイルをインポート"
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    AddMediaItem(file);
                }
            }
        }

        public void AddMediaItem(string filePath)
        {
            try
            {
                var extension = System.IO.Path.GetExtension(filePath).ToLower();
                var mediaType = GetMediaTypeFromExtension(extension);

                var mediaItem = new MediaItem
                {
                    Name = System.IO.Path.GetFileNameWithoutExtension(filePath),
                    Type = mediaType,
                    FilePath = filePath
                };

                // Get metadata asynchronously
                _ = Task.Run(async () =>
                {
                    var metadata = await Services.MediaMetadataService.GetMetadataAsync(filePath, mediaType);
                    if (metadata != null)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            mediaItem.Duration = metadata.Duration;
                            mediaItem.Width = metadata.Width;
                            mediaItem.Height = metadata.Height;

                            // Update view model
                            var viewModel = MediaItems.FirstOrDefault(vm => vm.Id == mediaItem.Id);
                            viewModel?.RefreshMetadata();
                        });
                    }
                });

                // Generate thumbnail asynchronously
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var thumbnail = Services.MediaThumbnailService.GenerateThumbnail(filePath, mediaType);
                    if (thumbnail != null)
                    {
                        mediaItem.Thumbnail = thumbnail;
                        // Notify the view model if it exists
                        var viewModel = MediaItems.FirstOrDefault(vm => vm.Id == mediaItem.Id);
                        if (viewModel != null)
                        {
                            viewModel.Thumbnail = thumbnail;
                        }
                    }
                }));

                _project.MediaPool.Add(mediaItem);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"メディア追加エラー: {ex.Message}");
            }
        }

        public void RemoveMediaItem(MediaItemViewModel item)
        {
            var mediaItem = _project.MediaPool.FirstOrDefault(m => m.Id == item.Id);
            if (mediaItem != null)
            {
                _project.MediaPool.Remove(mediaItem);
                MediaItems.Remove(item);
            }
        }

        private void DeleteSelectedItem()
        {
            if (SelectedItem != null)
            {
                var item = SelectedItem;
                RemoveMediaItem(item);
                SelectedItem = null;
            }
        }

        private void RefreshMediaItems()
        {
            MediaItems.Clear();
            foreach (var item in _project.MediaPool)
            {
                MediaItems.Add(new MediaItemViewModel(item));
            }
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_filterType == null)
            {
                // Show all items
                var allItems = _project.MediaPool.Select(m => new MediaItemViewModel(m)).ToList();
                MediaItems.Clear();
                foreach (var item in allItems)
                {
                    MediaItems.Add(item);
                }
            }
            else
            {
                var filteredItems = _project.MediaPool
                    .Where(m => m.Type == _filterType)
                    .Select(m => new MediaItemViewModel(m))
                    .ToList();
                MediaItems.Clear();
                foreach (var item in filteredItems)
                {
                    MediaItems.Add(item);
                }
            }
        }

        private void ClearFilter()
        {
            FilterType = null;
            IsAllFilterChecked = true;
        }

        private void SetFilter(object? parameter)
        {
            if (parameter is string typeStr)
            {
                if (Enum.TryParse<MediaType>(typeStr, out var mediaType))
                {
                    FilterType = mediaType;
                    IsAllFilterChecked = false;
                }
            }
        }

        private void SelectItem(object? parameter)
        {
            if (parameter is MediaItemViewModel item)
            {
                // Deselect all other items
                foreach (var mediaItem in MediaItems)
                {
                    if (mediaItem != item)
                    {
                        mediaItem.IsSelected = false;
                    }
                }
                SelectedItem = item;
            }
        }

        private static MediaType GetMediaTypeFromExtension(string extension)
        {
            var videoExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mkv", ".webm" };
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
            var audioExtensions = new[] { ".wav", ".mp3", ".ogg", ".m4a", ".flac", ".aac" };

            if (videoExtensions.Contains(extension)) return MediaType.Video;
            if (imageExtensions.Contains(extension)) return MediaType.Image;
            if (audioExtensions.Contains(extension)) return MediaType.Audio;

            return MediaType.Audio; // Default fallback
        }

        public void ClearAll()
        {
            _project.MediaPool.Clear();
            MediaItems.Clear();
        }
    }
}
