using System;
using System.Collections.ObjectModel;
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
            _characters = new ObservableCollection<SimpleCharacterViewModel>();
            _inlineIconDictionary = new ObservableCollection<SimpleInlineIconDictionaryItem>();
            _shortcuts = new ObservableCollection<ShortcutItem>();
            SaveCommand = new RelayCommand(async _ => await SaveInternal());
            CancelCommand = new RelayCommand(_ => Close());
            BrowseVoiceVoxCommand = new RelayCommand(_ => BrowseVoiceVoxPath());
            BrowseOutputCommand = new RelayCommand(_ => BrowseOutputDirectory());
            AddCharacterCommand = new RelayCommand(_ => AddCharacter());
            AddDictionaryRowCommand = new RelayCommand(_ => AddDictionaryRow());
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

        // Character settings
        private ObservableCollection<SimpleCharacterViewModel> _characters;
        public ObservableCollection<SimpleCharacterViewModel> Characters
        {
            get => _characters;
            set => SetProperty(ref _characters, value);
        }

        // Inline Icon Dictionary
        private ObservableCollection<SimpleInlineIconDictionaryItem> _inlineIconDictionary;
        public ObservableCollection<SimpleInlineIconDictionaryItem> InlineIconDictionary
        {
            get => _inlineIconDictionary;
            set => SetProperty(ref _inlineIconDictionary, value);
        }

        // Shortcuts
        private ObservableCollection<ShortcutItem> _shortcuts;
        public ObservableCollection<ShortcutItem> Shortcuts
        {
            get => _shortcuts;
            set => SetProperty(ref _shortcuts, value);
        }

        // Voice Synthesis Parameters
        public double DefaultSpeed
        {
            get => _settings.DefaultSpeed;
            set
            {
                _settings.DefaultSpeed = value;
                OnPropertyChanged();
            }
        }

        public double DefaultPitch
        {
            get => _settings.DefaultPitch;
            set
            {
                _settings.DefaultPitch = value;
                OnPropertyChanged();
            }
        }

        public double DefaultIntonation
        {
            get => _settings.DefaultIntonation;
            set
            {
                _settings.DefaultIntonation = value;
                OnPropertyChanged();
            }
        }

        public double DefaultVolume
        {
            get => _settings.DefaultVolume;
            set
            {
                _settings.DefaultVolume = value;
                OnPropertyChanged();
            }
        }

        public int DefaultStartSilence
        {
            get => _settings.DefaultStartSilence;
            set
            {
                _settings.DefaultStartSilence = value;
                OnPropertyChanged();
            }
        }

        public int DefaultEndSilence
        {
            get => _settings.DefaultEndSilence;
            set
            {
                _settings.DefaultEndSilence = value;
                OnPropertyChanged();
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand BrowseVoiceVoxCommand { get; }
        public ICommand BrowseOutputCommand { get; }
        public ICommand AddCharacterCommand { get; }
        public ICommand AddDictionaryRowCommand { get; }

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
            OnPropertyChanged(nameof(DefaultSpeed));
            OnPropertyChanged(nameof(DefaultPitch));
            OnPropertyChanged(nameof(DefaultIntonation));
            OnPropertyChanged(nameof(DefaultVolume));
            OnPropertyChanged(nameof(DefaultStartSilence));
            OnPropertyChanged(nameof(DefaultEndSilence));

            // Load characters from settings
            _characters.Clear();
            if (_settings.Characters != null)
            {
                foreach (var charSetting in _settings.Characters)
                {
                    _characters.Add(new SimpleCharacterViewModel(charSetting, RemoveCharacter));
                }
            }

            // Load inline icon dictionary
            _inlineIconDictionary.Clear();
            if (_settings.InlineIconDictionary != null)
            {
                foreach (var item in _settings.InlineIconDictionary)
                {
                    _inlineIconDictionary.Add(new SimpleInlineIconDictionaryItem
                    {
                        SearchKey = item.SearchKey,
                        ImagePath = item.ImagePath,
                        HeightScale = item.HeightScale,
                        IsEnabled = item.IsEnabled
                    });
                }
            }

            // Load shortcuts
            _shortcuts.Clear();
            if (_settings.Shortcuts != null)
            {
                foreach (var sc in _settings.Shortcuts)
                {
                    _shortcuts.Add(new ShortcutItem
                    {
                        Name = sc.Name,
                        KeySequence = sc.KeySequence
                    });
                }
            }

            _isLoading = false;
        }

        private async System.Threading.Tasks.Task SaveInternal()
        {
            if (!_isLoading)
            {
                // Save characters
                _settings.Characters = new System.Collections.Generic.List<CharacterSettings>();
                foreach (var vm in _characters)
                {
                    _settings.Characters.Add(vm.ToSettings());
                }

                // Save inline icon dictionary
                _settings.InlineIconDictionary = new System.Collections.Generic.List<VideoCreatorWPF.Models.InlineIconDictionaryItem>();
                foreach (var item in _inlineIconDictionary)
                {
                    _settings.InlineIconDictionary.Add(new VideoCreatorWPF.Models.InlineIconDictionaryItem
                    {
                        SearchKey = item.SearchKey,
                        ImagePath = item.ImagePath,
                        HeightScale = item.HeightScale,
                        IsEnabled = item.IsEnabled
                    });
                }

                // Save shortcuts
                _settings.Shortcuts = new System.Collections.Generic.List<ShortcutSetting>();
                foreach (var sc in _shortcuts)
                {
                    _settings.Shortcuts.Add(new ShortcutSetting
                    {
                        Name = sc.Name,
                        KeySequence = sc.KeySequence
                    });
                }

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

        private void AddCharacter()
        {
            var vm = new SimpleCharacterViewModel(new CharacterSettings(), RemoveCharacter);
            _characters.Add(vm);
        }

        private void RemoveCharacter(SimpleCharacterViewModel vm)
        {
            _characters.Remove(vm);
        }

        private void AddDictionaryRow()
        {
            _inlineIconDictionary.Add(new SimpleInlineIconDictionaryItem
            {
                SearchKey = "",
                ImagePath = "",
                HeightScale = 1.0,
                IsEnabled = true
            });
        }
    }

    // Simple Character ViewModel for Settings Dialog
    public class SimpleCharacterViewModel : ViewModelBase
    {
        private CharacterSettings _settings;
        private Action<SimpleCharacterViewModel>? _onRemove;

        public SimpleCharacterViewModel(CharacterSettings settings, Action<SimpleCharacterViewModel>? onRemove = null)
        {
            _settings = settings;
            _onRemove = onRemove;
        }

        public string Name
        {
            get => _settings.Name;
            set
            {
                _settings.Name = value;
                OnPropertyChanged();
            }
        }

        public string Color
        {
            get => _settings.Color;
            set
            {
                _settings.Color = value;
                OnPropertyChanged();
            }
        }

        public CharacterSettings ToSettings() => _settings;

        public ICommand RemoveCharacterCommand => new RelayCommand(_ => RemoveFromParent());

        private void RemoveFromParent()
        {
            _onRemove?.Invoke(this);
        }
    }

    // Simple Inline Icon Dictionary Item for Settings Dialog
    public class SimpleInlineIconDictionaryItem : ViewModelBase
    {
        private string _searchKey = "";
        private string _imagePath = "";
        private double _heightScale = 1.0;
        private bool _isEnabled = true;

        public string SearchKey
        {
            get => _searchKey;
            set
            {
                _searchKey = value;
                OnPropertyChanged();
            }
        }

        public string ImagePath
        {
            get => _imagePath;
            set
            {
                _imagePath = value;
                OnPropertyChanged();
            }
        }

        public double HeightScale
        {
            get => _heightScale;
            set
            {
                _heightScale = value;
                OnPropertyChanged();
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        public ICommand BrowseImageCommand => new RelayCommand(_ => BrowseImage());

        private void BrowseImage()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "画像ファイル (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|すべてのファイル (*.*)|*.*",
                Title = "画像ファイルを選択"
            };

            if (dialog.ShowDialog() == true)
            {
                ImagePath = dialog.FileName;
            }
        }
    }

    // Shortcut Item for Settings Dialog
    public class ShortcutItem : ViewModelBase
    {
        private string _name = "";
        private string _keySequence = "";

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string KeySequence
        {
            get => _keySequence;
            set
            {
                _keySequence = value;
                OnPropertyChanged();
            }
        }
    }
}
