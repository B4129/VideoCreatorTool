using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        private ObservableCollection<string> _recentProjects = new();
        private string _timecodeInput = "";

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

            // 最近開いたファイル履歴をロード
            LoadRecentProjects();

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
            JumpToTimecodeCommand = new RelayCommand(_ => JumpToTimecode(), _ => !string.IsNullOrEmpty(_timecodeInput));
            NewProjectFromTemplateCommand = new RelayCommand(async _ => await NewProjectFromTemplate());
            SaveAsTemplateCommand = new RelayCommand(_ => SaveAsTemplate(), _ => _currentProject != null);

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

        /// <summary>
        /// 最近開いたファイル履歴
        /// </summary>
        public ObservableCollection<string> RecentProjects
        {
            get => _recentProjects;
            set => SetProperty(ref _recentProjects, value);
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
        public ICommand JumpToTimecodeCommand { get; }
        public ICommand NewProjectFromTemplateCommand { get; }
        public ICommand SaveAsTemplateCommand { get; }

        /// <summary>
        /// タイムコード入力欄の値
        /// </summary>
        public string TimecodeInput
        {
            get => _timecodeInput;
            set => SetProperty(ref _timecodeInput, value);
        }

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

                    // 履歴に追加
                    AddToRecentProjects(dialog.FileName);
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
                    AddToRecentProjects(dialog.FileName);
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

        /// <summary>
        /// 入力されたタイムコードの位置にジャンプ
        /// 形式: MM:SS または MM:SS.FF
        /// </summary>
        private void JumpToTimecode()
        {
            if (string.IsNullOrEmpty(_timecodeInput))
                return;

            try
            {
                int targetFrame = 0;

                // MM:SS.FF 形式を解析
                var parts = _timecodeInput.Split(':');
                if (parts.Length == 2)
                {
                    var minutes = int.Parse(parts[0]);
                    var secondsParts = parts[1].Split('.');
                    var seconds = int.Parse(secondsParts[0]);
                    var frames = secondsParts.Length > 1 ? int.Parse(secondsParts[1]) : 0;

                    targetFrame = (minutes * 60 + seconds) * 30 + frames;
                }
                else if (parts.Length == 1)
                {
                    // フレーム数直接指定
                    targetFrame = int.Parse(parts[0]);
                }

                // タイムラインの現在フレームを更新
                // Note: TimelineViewModelはMainWindow.TimelineViewControl.DataContextに設定されている
                // ここではstatus messageを更新
                StatusMessage = $"タイムコード {_timecodeInput} にジャンプ（フレーム: {targetFrame}）";
            }
            catch (Exception ex)
            {
                StatusMessage = $"タイムコード解析エラー: {ex.Message}";
            }
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

        /// <summary>
        /// テンプレートから新規プロジェクトを作成
        /// </summary>
        private async Task NewProjectFromTemplate()
        {
            var templateDir = Models.ProjectTemplate.GetTemplateDirectory();
            var templates = Directory.GetFiles(templateDir, "*.json");

            if (templates.Length == 0)
            {
                // テンプレートがない場合はデフォルトを使用
                var defaultTemplate = Models.ProjectTemplate.CreateDefaultTemplate();
                Models.ProjectTemplate.SaveTemplate(Path.Combine(templateDir, "デフォルト.json"), defaultTemplate);
                templates = Directory.GetFiles(templateDir, "*.json");
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Project Template (*.json)|*.json",
                Title = "テンプレートを選択",
                InitialDirectory = templateDir
            };

            if (dialog.ShowDialog() == true)
            {
                var template = Models.ProjectTemplate.LoadTemplate(dialog.FileName);
                if (template != null)
                {
                    // テンプレートからプロジェクトを作成
                    var project = ProjectService.CreateNewProject();
                    project.Name = template.Name;
                    project.Width = template.Width;
                    project.Height = template.Height;
                    project.FrameRate = template.FrameRate;
                    project.BackgroundColor = template.BackgroundColor;

                    // トラックを作成
                    foreach (var trackData in template.Tracks)
                    {
                        var track = new Models.TimelineTrack
                        {
                            Name = trackData.Name,
                            BlockColor = trackData.Color
                        };
                        project.Tracks.Add(track);
                    }

                    CurrentProject = project;
                    CurrentProjectViewModel = new ProjectViewModel(project);
                    MediaPoolViewModel = new MediaPoolViewModel(project);
                    _autoSaveService.SetCurrentProject(project);

                    StatusMessage = $"テンプレート '{template.Name}' からプロジェクトを作成";
                }
                else
                {
                    StatusMessage = "テンプレートの読み込みに失敗しました";
                }
            }
        }

        /// <summary>
        /// 現在のプロジェクトをテンプレートとして保存
        /// </summary>
        private void SaveAsTemplate()
        {
            if (_currentProject == null) return;

            var templateDir = Models.ProjectTemplate.GetTemplateDirectory();
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Project Template (*.json)|*.json",
                Title = "テンプレートとして保存",
                InitialDirectory = templateDir,
                FileName = _currentProject.Name
            };

            if (dialog.ShowDialog() == true)
            {
                var template = new Models.ProjectTemplate
                {
                    Name = _currentProject.Name,
                    Description = $"テンプレート: {_currentProject.Name}",
                    Width = _currentProject.Width,
                    Height = _currentProject.Height,
                    FrameRate = _currentProject.FrameRate,
                    BackgroundColor = _currentProject.BackgroundColor,
                    Tracks = _currentProject.Tracks.Select(t => new Models.TemplateTrack
                    {
                        Name = t.Name,
                        Color = t.BlockColor ?? "#3b82f6"
                    }).ToList()
                };

                Models.ProjectTemplate.SaveTemplate(dialog.FileName, template);
                StatusMessage = $"テンプレートを保存しました: {template.Name}";
            }
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

        /// <summary>
        /// 最近開いたファイル履歴をロード
        /// </summary>
        private void LoadRecentProjects()
        {
            try
            {
                var recentFile = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VideoCreator", "recent.txt");

                if (System.IO.File.Exists(recentFile))
                {
                    var lines = System.IO.File.ReadAllLines(recentFile);
                    _recentProjects = new ObservableCollection<string>(
                        lines.Where(l => !string.IsNullOrWhiteSpace(l) && System.IO.File.Exists(l))
                             .Take(10)); // 最新10件まで
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"最近のプロジェクト読み込みエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 最近開いたファイル履歴に追加
        /// </summary>
        public void AddToRecentProjects(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                return;

            // 既存のエントリーを削除
            _recentProjects.Remove(filePath);

            // 先頭に追加
            _recentProjects.Insert(0, filePath);

            // 10件以上に制限
            while (_recentProjects.Count > 10)
            {
                _recentProjects.RemoveAt(_recentProjects.Count - 1);
            }

            // ファイルに保存
            SaveRecentProjects();
        }

        /// <summary>
        /// 最近開いたファイル履歴を保存
        /// </summary>
        private void SaveRecentProjects()
        {
            try
            {
                var recentDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VideoCreator");

                if (!System.IO.Directory.Exists(recentDir))
                {
                    System.IO.Directory.CreateDirectory(recentDir);
                }

                var recentFile = System.IO.Path.Combine(recentDir, "recent.txt");
                System.IO.File.WriteAllLines(recentFile, _recentProjects);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"最近のプロジェクト保存エラー: {ex.Message}");
            }
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
