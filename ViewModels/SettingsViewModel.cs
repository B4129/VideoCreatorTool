using System;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using VideoCreatorWPF.Commands;
using VideoCreatorWPF.Models;
using VideoCreatorWPF.Services;

namespace VideoCreatorWPF.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private AppSettings _settings;
        private bool _isOpen;
        private bool _isLoading = true;

        public SettingsViewModel()
        {
            _settings = new AppSettings();
            SaveCommand = new RelayCommand(async _ => await SaveInternal());
            CancelCommand = new RelayCommand(_ => Close());
            BrowseVoiceVoxCommand = new RelayCommand(_ => BrowseVoiceVoxPath());
            BrowseOutputCommand = new RelayCommand(_ => BrowseOutputDirectory());
        }

        public bool IsOpen
        {
            get => _isOpen;
            set => SetProperty(ref _isOpen, value);
        }

        public string VoiceVoxPath
        {
            get => _settings.VoiceVoxPath;
            set
            {
                _settings.VoiceVoxPath = value;
                OnPropertyChanged();
            }
        }

        public string? OutputDirectory
        {
            get => _settings.OutputDirectory;
            set
            {
                _settings.OutputDirectory = value;
                OnPropertyChanged();
            }
        }

        public int DefaultWidth
        {
            get => _settings.DefaultWidth;
            set
            {
                _settings.DefaultWidth = value;
                OnPropertyChanged();
            }
        }

        public int DefaultHeight
        {
            get => _settings.DefaultHeight;
            set
            {
                _settings.DefaultHeight = value;
                OnPropertyChanged();
            }
        }

        public double DefaultFrameRate
        {
            get => _settings.DefaultFrameRate;
            set
            {
                _settings.DefaultFrameRate = value;
                OnPropertyChanged();
            }
        }

        public string Theme
        {
            get => _settings.Theme;
            set
            {
                _settings.Theme = value;
                OnPropertyChanged();
            }
        }

        public bool AutoSave
        {
            get => _settings.AutoSave;
            set
            {
                _settings.AutoSave = value;
                OnPropertyChanged();
            }
        }

        public int AutoSaveInterval
        {
            get => _settings.AutoSaveInterval;
            set
            {
                _settings.AutoSaveInterval = value;
                OnPropertyChanged();
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand BrowseVoiceVoxCommand { get; }
        public ICommand BrowseOutputCommand { get; }

        public async void Load()
        {
            _isLoading = true;
            _settings = await SettingsService.LoadSettings();
            OnPropertyChanged(nameof(VoiceVoxPath));
            OnPropertyChanged(nameof(OutputDirectory));
            OnPropertyChanged(nameof(DefaultWidth));
            OnPropertyChanged(nameof(DefaultHeight));
            OnPropertyChanged(nameof(DefaultFrameRate));
            OnPropertyChanged(nameof(Theme));
            OnPropertyChanged(nameof(AutoSave));
            OnPropertyChanged(nameof(AutoSaveInterval));
            _isLoading = false;
        }

        private async System.Threading.Tasks.Task SaveInternal()
        {
            if (!_isLoading)
            {
                await SettingsService.SaveSettings(_settings);
                Close();
            }
        }

        private void Close()
        {
            IsOpen = false;
        }

        private void BrowseVoiceVoxPath()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "実行ファイル (*.exe)|*.exe|すべてのファイル (*.*)|*.*",
                Title = "VOICEVOX.exeを選択"
            };

            if (dialog.ShowDialog() == true)
            {
                VoiceVoxPath = dialog.FileName;
            }
        }

        private void BrowseOutputDirectory()
        {
            var dialog = new OpenFolderDialog();

            if (dialog.ShowDialog() == true)
            {
                OutputDirectory = dialog.FolderName;
            }
        }
    }
}
