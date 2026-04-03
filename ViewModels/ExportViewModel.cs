using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using VideoCreatorWPF.Commands;
using VideoCreatorWPF.Models;
using VideoCreatorWPF.Services;

namespace VideoCreatorWPF.ViewModels
{
    public class ExportViewModel : ViewModelBase
    {
        private readonly VideoProject _project;
        private readonly AppSettings _settings;
        private string _outputPath = "";
        private string _resolutionPreset = "FHD (1920x1080)";
        private string _exportFormat = "MP4";
        private int _exportWidth = 1920;
        private int _exportHeight = 1080;
        private int _exportFrameRate = 30;
        private int _exportVideoBitrate = 8000;
        private string _exportRange = "all";
        private int _rangeStartFrame;
        private int _rangeEndFrame;
        private int _progress;
        private bool _isExporting;
        private string _statusMessage = "";
        private CancellationTokenSource? _cancelToken;

        public ExportViewModel(VideoProject project, AppSettings settings)
        {
            _project = project;
            _settings = settings;
            ExportCommand = new RelayCommand(async _ => await Export(), _ => !_isExporting);
            CancelCommand = new RelayCommand(_ => Cancel());
            BrowseOutputCommand = new RelayCommand(_ => BrowseOutput());
            SavePresetCommand = new RelayCommand(_ => SavePreset());
            LoadPresetCommand = new RelayCommand(_ => LoadPreset());

            // Initialize from project settings
            _exportWidth = project.Width;
            _exportHeight = project.Height;
            _exportFrameRate = (int)project.FrameRate;

            _outputPath = Path.Combine(
                ExportService.GetOutputDirectory(settings),
                ExportService.GenerateOutputFileName(project.Name));

            CalculateRange();
        }

        /// <summary>
        /// エクスポート形式に応じて出力ファイルの拡張子を更新
        /// </summary>
        private void UpdateOutputExtension()
        {
            var extension = _exportFormat switch
            {
                "MP4" => ".mp4",
                "MOV" => ".mov",
                "WebM" => ".webm",
                "GIF" => ".gif",
                _ => ".mp4"
            };

            if (!string.IsNullOrEmpty(_outputPath))
            {
                var dir = System.IO.Path.GetDirectoryName(_outputPath);
                var name = System.IO.Path.GetFileNameWithoutExtension(_outputPath);
                OutputPath = System.IO.Path.Combine(dir ?? "", name + extension);
            }
        }

        // Preset commands
        public ICommand SavePresetCommand { get; }
        public ICommand LoadPresetCommand { get; }

        // Output settings
        public string OutputPath
        {
            get => _outputPath;
            set => SetProperty(ref _outputPath, value);
        }

        public string ResolutionPreset
        {
            get => _resolutionPreset;
            set
            {
                if (SetProperty(ref _resolutionPreset, value))
                {
                    UpdateResolutionFromPreset();
                }
            }
        }

        /// <summary>
        /// エクスポート形式（MP4, MOV, WebM, GIF）
        /// </summary>
        public string ExportFormat
        {
            get => _exportFormat;
            set
            {
                if (SetProperty(ref _exportFormat, value))
                {
                    UpdateOutputExtension();
                }
            }
        }

        public int ExportWidth
        {
            get => _exportWidth;
            set => SetProperty(ref _exportWidth, value);
        }

        public int ExportHeight
        {
            get => _exportHeight;
            set => SetProperty(ref _exportHeight, value);
        }

        public int ExportFrameRate
        {
            get => _exportFrameRate;
            set => SetProperty(ref _exportFrameRate, value);
        }

        public int ExportVideoBitrate
        {
            get => _exportVideoBitrate;
            set => SetProperty(ref _exportVideoBitrate, value);
        }

        // Export range
        public string ExportRange
        {
            get => _exportRange;
            set
            {
                if (SetProperty(ref _exportRange, value))
                {
                    CalculateRange();
                    OnPropertyChanged(nameof(ShowRangeSelector));
                }
            }
        }

        public int RangeStartFrame
        {
            get => _rangeStartFrame;
            set
            {
                if (SetProperty(ref _rangeStartFrame, value))
                {
                    OnPropertyChanged(nameof(RangeStartTimeText));
                }
            }
        }

        public int RangeEndFrame
        {
            get => _rangeEndFrame;
            set
            {
                if (SetProperty(ref _rangeEndFrame, value))
                {
                    OnPropertyChanged(nameof(RangeEndTimeText));
                }
            }
        }

        public bool ShowRangeSelector => _exportRange == "range";

        public string RangeStartTimeText => FrameToTimeText(_rangeStartFrame);

        public string RangeEndTimeText => FrameToTimeText(_rangeEndFrame);

        // Progress
        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public bool IsExporting
        {
            get => _isExporting;
            set => SetProperty(ref _isExporting, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand ExportCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand BrowseOutputCommand { get; }

        private void UpdateResolutionFromPreset()
        {
            switch (_resolutionPreset)
            {
                case "4K (3840x2160)":
                    ExportWidth = 3840;
                    ExportHeight = 2160;
                    break;
                case "FHD (1920x1080)":
                    ExportWidth = 1920;
                    ExportHeight = 1080;
                    break;
                case "HD (1280x720)":
                    ExportWidth = 1280;
                    ExportHeight = 720;
                    break;
                case "縦型 9:16 (1080x1920)":
                    ExportWidth = 1080;
                    ExportHeight = 1920;
                    break;
            }
        }

        private int _selectionStartFrame = -1;
        private int _selectionEndFrame = -1;

        /// <summary>
        /// タイムラインの選択範囲を設定（TimelineViewModelから呼び出し）
        /// </summary>
        public void SetSelectionRange(int startFrame, int endFrame)
        {
            _selectionStartFrame = startFrame;
            _selectionEndFrame = endFrame;

            // 選択範囲エクスポートが選択中の場合は再計算
            if (_exportRange == "selection")
            {
                CalculateRange();
            }
        }

        private void CalculateRange()
        {
            if (_exportRange == "all")
            {
                _rangeStartFrame = 0;
                _rangeEndFrame = GetMaxFrame(_project);
            }
            else if (_exportRange == "selection")
            {
                // Use selection range if available
                if (_selectionStartFrame >= 0 && _selectionEndFrame > _selectionStartFrame)
                {
                    _rangeStartFrame = _selectionStartFrame;
                    _rangeEndFrame = _selectionEndFrame;
                }
                else
                {
                    // Fallback to full range
                    _rangeStartFrame = 0;
                    _rangeEndFrame = GetMaxFrame(_project);
                }
            }
            // For "range", keep current values or default to full range

            OnPropertyChanged(nameof(RangeStartTimeText));
            OnPropertyChanged(nameof(RangeEndTimeText));
        }

        private int GetMaxFrame(VideoProject project)
        {
            var maxFrame = 0;
            foreach (var track in project.Tracks)
            {
                foreach (var block in track.Items)
                {
                    var endFrame = block.StartFrame + block.Duration;
                    if (endFrame > maxFrame) maxFrame = endFrame;
                }
            }
            return Math.Max(maxFrame, 30); // At least 1 second
        }

        private string FrameToTimeText(int frame)
        {
            var seconds = frame / _project.FrameRate;
            var minutes = (int)(seconds / 60);
            var secs = (int)(seconds % 60);
            var frames = frame % (int)_project.FrameRate;
            return $"{minutes:D2}:{secs:D2}.{frames:D2}";
        }

        private async Task Export()
        {
            _cancelToken = new CancellationTokenSource();
            IsExporting = true;
            StatusMessage = "エクスポート中...";
            Progress = 0;

            var progress = new Progress<int>(p => Progress = p);

            try
            {
                var result = await ExportService.ExportVideo(
                    _project, _outputPath, progress,
                    _exportWidth, _exportHeight, _exportFrameRate,
                    _exportVideoBitrate,
                    _rangeStartFrame, _rangeEndFrame,
                    _cancelToken.Token, _exportFormat);

                IsExporting = false;
                StatusMessage = result ? "エクスポート完了" : "エクスポート失敗";

                if (result)
                {
                    // Show completion message
                    System.Windows.MessageBox.Show(
                        $"エクスポートが完了しました！\n\n保存先: {_outputPath}",
                        "完了",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (OperationCanceledException)
            {
                IsExporting = false;
                StatusMessage = "エクスポートをキャンセルしました";
                Progress = 0;
            }
            finally
            {
                _cancelToken = null;
            }
        }

        private void Cancel()
        {
            if (IsExporting && _cancelToken != null)
            {
                _cancelToken.Cancel();
            }
        }

        private void BrowseOutput()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "MP4 ファイル (*.mp4)|*.mp4",
                Title = "出力先を指定",
                FileName = ExportService.GenerateOutputFileName(_project.Name)
            };

            if (dialog.ShowDialog() == true)
            {
                OutputPath = dialog.FileName;
            }
        }

        /// <summary>
        /// 現在の設定をプリセットとして保存
        /// </summary>
        private void SavePreset()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "プリセットを保存",
                Filter = "JSON ファイル (*.json)|*.json",
                FileName = "MyPreset",
                InitialDirectory = Models.ExportPreset.GetPresetDirectory()
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var preset = new Models.ExportPreset
                    {
                        Name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName),
                        Width = _exportWidth,
                        Height = _exportHeight,
                        FrameRate = _exportFrameRate,
                        VideoBitrate = _exportVideoBitrate,
                        AudioBitrate = "192k" // Default
                    };

                    Models.ExportPreset.SavePreset(dialog.FileName, preset);
                    StatusMessage = $"プリセットを保存しました: {preset.Name}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"プリセット保存エラー: {ex.Message}";
                }
            }
        }

        /// <summary>
        /// プリセットを読み込んで設定に適用
        /// </summary>
        private void LoadPreset()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "プリセットを読み込む",
                Filter = "JSON ファイル (*.json)|*.json",
                InitialDirectory = Models.ExportPreset.GetPresetDirectory()
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var preset = Models.ExportPreset.LoadPreset(dialog.FileName);
                    if (preset != null)
                    {
                        ExportWidth = preset.Width;
                        ExportHeight = preset.Height;
                        ExportFrameRate = preset.FrameRate;
                        ExportVideoBitrate = preset.VideoBitrate;

                        // Update resolution preset text
                        _resolutionPreset = $"{preset.Width}x{preset.Height}";
                        OnPropertyChanged(nameof(ResolutionPreset));

                        StatusMessage = $"プリセットを読み込みました: {preset.Name}";
                    }
                    else
                    {
                        StatusMessage = "プリセットの読み込みに失敗しました";
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"プリセット読み込みエラー: {ex.Message}";
                }
            }
        }
    }
}
