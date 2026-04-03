using System;
using System.Windows.Input;
using VideoCreatorWPF.Commands;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.ViewModels
{
    public class ProjectSettingsViewModel : ViewModelBase
    {
        private readonly VideoProject _project;
        private string _projectName;
        private int _width;
        private int _height;
        private double _frameRate;
        private string _backgroundColor;
        private string _aspectRatioText;

        public ProjectSettingsViewModel(VideoProject project)
        {
            _project = project;
            _projectName = project.Name;
            _width = project.Width;
            _height = project.Height;
            _frameRate = project.FrameRate;
            _backgroundColor = project.BackgroundColor;
            _aspectRatioText = $"{project.Width}:{project.Height}";

            SaveCommand = new RelayCommand(_ => Save());
            CancelCommand = new RelayCommand(_ => Cancel());
            ApplyPresetCommand = new RelayCommand<string>(ApplyPreset);
        }

        public string ProjectName
        {
            get => _projectName;
            set => SetProperty(ref _projectName, value);
        }

        public int Width
        {
            get => _width;
            set
            {
                if (SetProperty(ref _width, value))
                {
                    UpdateAspectRatio();
                }
            }
        }

        public int Height
        {
            get => _height;
            set
            {
                if (SetProperty(ref _height, value))
                {
                    UpdateAspectRatio();
                }
            }
        }

        public double FrameRate
        {
            get => _frameRate;
            set => SetProperty(ref _frameRate, value);
        }

        public string BackgroundColor
        {
            get => _backgroundColor;
            set => SetProperty(ref _backgroundColor, value);
        }

        public string AspectRatioText
        {
            get => _aspectRatioText;
            set => SetProperty(ref _aspectRatioText, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ApplyPresetCommand { get; }

        public event Action<bool>? OnClose;

        private void Save()
        {
            _project.Name = _projectName;
            _project.Width = _width;
            _project.Height = _height;
            _project.FrameRate = _frameRate;
            _project.BackgroundColor = _backgroundColor;
            OnClose?.Invoke(true);
        }

        private void Cancel()
        {
            OnClose?.Invoke(false);
        }

        private void ApplyPreset(string preset)
        {
            switch (preset)
            {
                case "FHD":
                    _width = 1920;
                    _height = 1080;
                    break;
                case "HD":
                    _width = 1280;
                    _height = 720;
                    break;
                case "4K":
                    _width = 3840;
                    _height = 2160;
                    break;
                case "Vertical9:16":
                    _width = 1080;
                    _height = 1920;
                    break;
            }
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            UpdateAspectRatio();
        }

        private void UpdateAspectRatio()
        {
            _aspectRatioText = $"{_width}:{_height}";
            OnPropertyChanged(nameof(AspectRatioText));
        }
    }
}
