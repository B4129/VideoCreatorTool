using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using VideoCreatorWPF.Commands;
using VideoCreatorWPF.Core;
using VideoCreatorWPF.Models;
using VideoCreatorWPF.Services;
using VideoCreatorWPF.Events;
using VideoCreatorWPF.Views;

namespace VideoCreatorWPF.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly EventBus _eventBus = EventBus.Instance;
        private readonly AutoSaveService _autoSaveService;

        private string _statusMessage = "VOICEVOX起動確認中...";
        private bool _isVoiceVoxConnected;
        private bool _isVoiceVoxStarting;
        private VideoProject? _currentProject;
        private ProjectViewModel? _currentProjectViewModel;
        private MediaPoolViewModel? _mediaPoolViewModel;
        private BlockPropertyViewModel _blockProperties = new();
        private AppSettings _settings = new();

        // Version information
        public string AppVersion { get; } = GetAppVersion();

        public MainWindowViewModel()
        {
            _settings = new AppSettings();
            _autoSaveService = new AutoSaveService(_settings);
            _autoSaveService.AutoSaved += (path) =>
            {
                StatusMessage = $"自動保存完了: {System.IO.Path.GetFileName(path)}";
            };

            CheckVoiceVoxStatusCommand = new RelayCommand(async _ => await CheckStatusAsync());
            OpenSettingsCommand = new RelayCommand(async _ => await OpenSettingsInternal());
            OpenProjectSettingsCommand = new RelayCommand(_ => OpenProjectSettings(), _ => _currentProject != null);
            NewProjectCommand = new RelayCommand(_ => NewProject());
            OpenProjectCommand = new RelayCommand(async _ => await OpenProject());
            SaveProjectCommand = new RelayCommand(async _ => await SaveProject(), _ => _currentProject != null);
            SaveProjectAsCommand = new RelayCommand(async _ => await SaveProjectAs(), _ => _currentProject != null);
            ExportVideoCommand = new RelayCommand(_ => ExportVideo(), _ => _currentProject != null);
            PlayCommand = new RelayCommand(_ => Play());
            PauseCommand = new RelayCommand(_ => Pause());
            StopCommand = new RelayCommand(_ => Stop());

            // Initialize settings and create default project after UI is ready
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
            {
                // Load settings first
                _settings = await SettingsService.LoadSettings();
                _autoSaveService.UpdateSettings(_settings);

                // Then create default project
                NewProject();

                // Finally initialize VOICEVOX
                await InitializeVoiceVox();
            }));
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsVoiceVoxConnected
        {
            get => _isVoiceVoxConnected;
            set => SetProperty(ref _isVoiceVoxConnected, value);
        }

        public bool IsVoiceVoxStarting
        {
            get => _isVoiceVoxStarting;
            set => SetProperty(ref _isVoiceVoxStarting, value);
        }

        public VideoProject? CurrentProject
        {
            get => _currentProject;
            set => SetProperty(ref _currentProject, value);
        }

        public ProjectViewModel? CurrentProjectViewModel
        {
            get => _currentProjectViewModel;
            set => SetProperty(ref _currentProjectViewModel, value);
        }

        public MediaPoolViewModel? MediaPoolViewModel
        {
            get => _mediaPoolViewModel;
            set => SetProperty(ref _mediaPoolViewModel, value);
        }

        public BlockPropertyViewModel BlockProperties
        {
            get => _blockProperties;
            set => SetProperty(ref _blockProperties, value);
        }

        public ICommand CheckVoiceVoxStatusCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenProjectSettingsCommand { get; }
        public ICommand NewProjectCommand { get; }
        public ICommand OpenProjectCommand { get; }
        public ICommand SaveProjectCommand { get; }
        public ICommand SaveProjectAsCommand { get; }
        public ICommand ExportVideoCommand { get; }
        public ICommand PlayCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand StopCommand { get; }

        private async System.Threading.Tasks.Task InitializeVoiceVox()
        {
            IsVoiceVoxStarting = true;
            StatusMessage = "VOICEVOXを起動中...";

            var result = await VoiceVoxService.EnsureVoiceVoxRunning();

            IsVoiceVoxStarting = false;
            IsVoiceVoxConnected = result;
            StatusMessage = result ? "VOICEVOX接続済み" : "VOICEVOX接続失敗";
        }

        private async Task CheckStatusAsync()
        {
            var isConnected = await VoiceVoxService.CheckHealth();
            IsVoiceVoxConnected = isConnected;
            StatusMessage = isConnected ? "VOICEVOX接続済み" : "VOICEVOX未接続";
        }

        private async System.Threading.Tasks.Task OpenSettingsInternal()
        {
            var dialog = new SettingsDialog();
            var viewModel = new ViewModels.SettingsViewModel();
            viewModel.Load();
            dialog.DataContext = viewModel;
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();

            // Reload settings after dialog closes
            _settings = await SettingsService.LoadSettings();
            _autoSaveService.UpdateSettings(_settings);

            StatusMessage = "設定を閉じました";
        }

        private void OpenProjectSettings()
        {
            if (_currentProject == null) return;

            var dialog = new Views.ProjectSettingsDialog();
            var viewModel = new ViewModels.ProjectSettingsViewModel(_currentProject);
            dialog.DataContext = viewModel;
            dialog.Owner = Application.Current.MainWindow;

            viewModel.OnClose += (saved) =>
            {
                if (saved)
                {
                    StatusMessage = $"プロジェクト設定を更新: {_currentProject.Name} ({_currentProject.Width}x{_currentProject.Height}@{_currentProject.FrameRate}fps)";
                }
                dialog.Close();
            };

            dialog.ShowDialog();
        }

        private void NewProject()
        {
            var project = ProjectService.CreateNewProject();

            // デフォルトで5つのトラックを作成
            for (int i = 0; i < 5; i++)
            {
                var track = new Models.TimelineTrack
                {
                    Name = $"トラック{i + 1}",
                    BlockColor = GetRandomColor()
                };
                project.Tracks.Add(track);
            }

            CurrentProject = project;
            CurrentProjectViewModel = new ProjectViewModel(project);
            MediaPoolViewModel = new MediaPoolViewModel(project);
            _autoSaveService.SetCurrentProject(project);

            StatusMessage = $"新規プロジェクト作成: {project.Name}";
            _eventBus.Publish(new ProjectCreatedEvent(project.Name));
        }

        private async Task OpenProject()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "VideoCreator プロジェクト (*.vcp)|*.vcp|すべてのファイル (*.*)|*.*",
                Title = "プロジェクトを開く"
            };

            if (dialog.ShowDialog() == true)
            {
                var project = await ProjectService.LoadProject(dialog.FileName);
                if (project != null)
                {
                    CurrentProject = project;
                    CurrentProjectViewModel = new ProjectViewModel(project);
                    MediaPoolViewModel = new MediaPoolViewModel(project);
                    _autoSaveService.SetCurrentProject(project);
                    StatusMessage = $"プロジェクトを開きました: {project.Name}";
                    _eventBus.Publish(new ProjectLoadedEvent(dialog.FileName));
                }
                else
                {
                    StatusMessage = "プロジェクトの読み込みに失敗しました";
                }
            }
        }

        private async Task SaveProject()
        {
            if (_currentProject == null) return;

            var filePath = _currentProject.FilePath;
            if (string.IsNullOrEmpty(filePath))
            {
                await SaveProjectAs();
                return;
            }

            var result = await ProjectService.SaveProject(_currentProject, filePath);
            StatusMessage = result ? "プロジェクトを保存しました" : "保存に失敗しました";
            if (result)
            {
                _eventBus.Publish(new ProjectSavedEvent(filePath));
            }
        }

        private async Task SaveProjectAs()
        {
            if (_currentProject == null) return;

            var dialog = new SaveFileDialog
            {
                Filter = "VideoCreator プロジェクト (*.vcp)|*.vcp",
                Title = "名前を付けて保存",
                FileName = ProjectService.GenerateDefaultFileName(_currentProject.Name)
            };

            if (dialog.ShowDialog() == true)
            {
                var result = await ProjectService.SaveProject(_currentProject, dialog.FileName);
                StatusMessage = result ? "プロジェクトを保存しました" : "保存に失敗しました";
                if (result)
                {
                    _eventBus.Publish(new ProjectSavedEvent(dialog.FileName));
                }
            }
        }

        private void ExportVideo()
        {
            if (_currentProject == null) return;

            var dialog = new ExportDialog();
            var viewModel = new ViewModels.ExportViewModel(_currentProject, _settings);
            dialog.DataContext = viewModel;
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
            StatusMessage = "動画エクスポート完了";
        }

        private void Play()
        {
            // TimelineViewModel will handle playback through keyboard shortcuts
            StatusMessage = "再生中...";
        }

        public void Pause()
        {
            StatusMessage = "一時停止";
        }

        private void Stop()
        {
            StatusMessage = "停止";
        }

        private string GetRandomColor()
        {
            return Utilities.ColorHelper.GetRandomColor();
        }

        private int _selectionStartFrame = -1;
        private int _selectionEndFrame = -1;

        /// <summary>
        /// タイムラインの選択範囲を設定
        /// </summary>
        public void SetSelectionRange(int startFrame, int endFrame)
        {
            _selectionStartFrame = startFrame;
            _selectionEndFrame = endFrame;

            // ExportViewModelに選択範囲を通知
            if (_exportViewModel != null)
            {
                _exportViewModel.SetSelectionRange(startFrame, endFrame);
            }
        }

        /// <summary>
        /// タイムラインの更新通知（字幕インポート後などに使用）
        /// </summary>
        public void RefreshTimeline()
        {
            // タイムライントラックの変更を通知するため、PropertyChangedを発火
            OnPropertyChanged(nameof(CurrentProject));
        }

        private ExportViewModel? _exportViewModel;

        /// <summary>
        /// ExportViewModelを取得（存在する場合）
        /// </summary>
        public ExportViewModel? GetExportViewModel()
        {
            return _exportViewModel;
        }

        /// <summary>
        /// ExportViewModelを設定
        /// </summary>
        public void SetExportViewModel(ExportViewModel vm)
        {
            _exportViewModel = vm;
        }

        public void Dispose()
        {
            _autoSaveService?.Dispose();
        }

        /// <summary>
        /// アプリケーションのバージョン情報を取得
        /// </summary>
        private static string GetAppVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                if (version != null)
                {
                    return $"v{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
                }
            }
            catch { }
            return "v0.0.0.0";
        }
    }
}
