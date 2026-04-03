using System;
using System.Collections.ObjectModel;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.ViewModels
{
    /// <summary>
    /// ViewModel for individual timeline tracks
    /// </summary>
    public class TimelineTrackViewModel : ViewModelBase
    {
        private readonly TimelineTrack _track;
        private string _name;
        private bool _isVisible;
        private bool _isLocked;
        private bool _isEnabled = true;
        private double _volume = 100;
        private bool _isMuted;
        private bool _isEditingName;
        private string _editingName = string.Empty;

        public TimelineTrackViewModel(TimelineTrack track)
        {
            _track = track;
            _name = track.Name;
            _isVisible = track.IsVisible;
            _isLocked = track.IsLocked;
            _isEnabled = track.IsEnabled;
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public bool IsEditingName
        {
            get => _isEditingName;
            set => SetProperty(ref _isEditingName, value);
        }

        public string EditingName
        {
            get => _editingName;
            set => SetProperty(ref _editingName, value);
        }

        public void StartRename()
        {
            _editingName = _name;
            IsEditingName = true;
        }

        public void EndRename()
        {
            if (!string.IsNullOrWhiteSpace(_editingName))
            {
                Name = _editingName;
            }
            IsEditingName = false;
        }

        public string? BlockColor => _track.BlockColor;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (SetProperty(ref _isVisible, value))
                {
                    _track.IsVisible = value;
                }
            }
        }

        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                if (SetProperty(ref _isLocked, value))
                {
                    _track.IsLocked = value;
                }
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (SetProperty(ref _isEnabled, value))
                {
                    _track.IsEnabled = value;
                    _isLocked = !value;
                    OnPropertyChanged(nameof(IsLocked));
                }
            }
        }

        public double Volume
        {
            get => _volume;
            set => SetProperty(ref _volume, value);
        }

        public bool IsMuted
        {
            get => _isMuted;
            set => SetProperty(ref _isMuted, value);
        }

        public ObservableCollection<TimelineBlock> Items => _track.Items;
    }
}
