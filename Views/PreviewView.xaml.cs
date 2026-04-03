using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace VideoCreatorWPF.Views
{
    public partial class PreviewView : UserControl
    {
        public PreviewView()
        {
            InitializeComponent();

            // Setup MediaElement event handlers
            VideoPlayer.MediaOpened += VideoPlayer_MediaOpened;
            VideoPlayer.MediaEnded += VideoPlayer_MediaEnded;

            // DataContext の変更を監視
            DataContextChanged += PreviewView_DataContextChanged;
        }

        private void PreviewView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ViewModels.PreviewViewModel vm)
            {
                vm.PropertyChanged += PreviewViewModel_PropertyChanged;
            }
        }

        private void PreviewViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.PreviewViewModel.CurrentVideoPath))
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (DataContext is ViewModels.PreviewViewModel vm && vm.CurrentVideoPath != null)
                    {
                        Debug.WriteLine($"[Preview] CurrentVideoPath changed: {vm.CurrentVideoPath}");
                        // 動画パスが変更されたら読み込み
                        if (_lastVideoPath != vm.CurrentVideoPath)
                        {
                            _lastVideoPath = vm.CurrentVideoPath;
                            try
                            {
                                VideoPlayer.BeginInit();
                                VideoPlayer.Source = new Uri(vm.CurrentVideoPath);
                                VideoPlayer.LoadedBehavior = MediaState.Manual;
                                VideoPlayer.UnloadedBehavior = MediaState.Manual;
                                VideoPlayer.EndInit();

                                Debug.WriteLine($"[Preview] Video loaded, waiting for MediaOpened");

                                // 現在のフレーム位置にシーク（MediaOpenedで実行）
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[Preview] Error loading video: {ex.Message}");
                            }
                        }
                        else
                        {
                            // 同じ動画ならシークのみ
                            var fps = 30.0;
                            var offsetFrames = vm.CurrentFrame - vm.CurrentVideoStartFrame;
                            var positionSeconds = offsetFrames / fps;
                            Debug.WriteLine($"[Preview] Seeking to {positionSeconds}s (frame {vm.CurrentFrame}, start {vm.CurrentVideoStartFrame})");
                            if (positionSeconds >= 0 && VideoPlayer.Source != null)
                            {
                                VideoPlayer.Position = TimeSpan.FromSeconds(positionSeconds);
                                // If was playing, continue playback after seek
                                if (_isPlaying)
                                {
                                    VideoPlayer.Play();
                                }
                                else
                                {
                                    VideoPlayer.Pause();
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[Preview] Clearing video");
                        // 動画がない場合はクリア
                        VideoPlayer.Source = null;
                        _lastVideoPath = null;
                    }
                });
            }
        }

        /// <summary>
        /// Get video duration using Windows Shell API via dynamic COM
        /// Returns duration in seconds, or null if failed
        /// </summary>
        private double? GetVideoDurationFromFile(string filePath)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null)
                {
                    System.Diagnostics.Debug.WriteLine("Shell.Application not available");
                    return null;
                }

                dynamic shell = Activator.CreateInstance(shellType);
                var folder = shell.NameSpace(Path.GetDirectoryName(filePath));
                if (folder == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot access folder: {Path.GetDirectoryName(filePath)}");
                    return null;
                }

                var item = folder.ParseName(Path.GetFileName(filePath));
                if (item == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot access file: {filePath}");
                    return null;
                }

                // Property 27 is "Length" in Windows Shell
                var lengthStr = folder.GetDetailsOf(item, 27);
                System.Diagnostics.Debug.WriteLine($"Shell API returned length: '{lengthStr}'");

                // Parse "HH:MM:SS" or "MM:SS" format
                if (TimeSpan.TryParse(lengthStr, out TimeSpan duration))
                {
                    return duration.TotalSeconds;
                }

                System.Diagnostics.Debug.WriteLine($"Failed to parse duration: '{lengthStr}'");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting video duration via Shell API: {ex.Message}");
                return null;
            }
        }

        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[Preview] MediaOpened called");
            try
            {
                // Update duration when video is loaded
                if (VideoPlayer.NaturalDuration.HasTimeSpan)
                {
                    var durationSeconds = VideoPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                    System.Diagnostics.Debug.WriteLine($"[Preview] VideoPlayer MediaOpened: {durationSeconds} seconds");

                    if (DataContext is ViewModels.PreviewViewModel viewModel)
                    {
                        viewModel.UpdateDuration(durationSeconds);
                    }

                    // Update TimelineViewModel duration (in frames at 30fps)
                    if (Window.GetWindow(this) is MainWindow mainWindow &&
                        mainWindow.TimelineViewControl.DataContext is ViewModels.TimelineViewModel timelineVm)
                    {
                        var totalFrames = (int)(durationSeconds * 30);
                        System.Diagnostics.Debug.WriteLine($"[Preview] Setting total frames: {totalFrames} (from {durationSeconds}s at 30fps)");
                        timelineVm.SetTotalFrames(totalFrames);
                        mainWindow.TimelineViewControl.UpdateRuler(totalFrames);
                    }

                    // Create video block on timeline if pending
                    if (_pendingVideoFilePath != null && _pendingTrackForBlock != null && _pendingTimelineVmForBlock != null && _pendingMainWindowForBlock != null)
                    {
                        var videoDuration = (int)(durationSeconds * 30); // 30fps
                        System.Diagnostics.Debug.WriteLine($"[Preview] Creating video block with duration: {videoDuration} frames (at 30fps)");

                        var videoBlock = new Models.TimelineBlock
                        {
                            CharacterId = Guid.NewGuid(),
                            StartFrame = _pendingTimelineVmForBlock.CurrentFrame,
                            Duration = videoDuration,
                            Text = $"動画: {Path.GetFileName(_pendingVideoFilePath)}",
                            BackgroundColor = "#10b981",
                            Type = Models.BlockType.Video,
                            AudioPath = _pendingVideoFilePath,
                            FontFamily = "Yu Gothic UI",
                            FontSize = 14,
                            TextColor = "#FFFFFF"
                        };

                        _pendingTrackForBlock.Items.Add(videoBlock);

                        // Update status message
                        if (_pendingMainWindowForBlock.ViewModel != null)
                        {
                            var minutes = (int)(durationSeconds / 60);
                            var seconds = (int)(durationSeconds % 60);
                            _pendingMainWindowForBlock.ViewModel.StatusMessage = $"動画追加: {Path.GetFileName(_pendingVideoFilePath)} ({minutes}分{seconds}秒)";
                        }

                        // Clear pending state
                        _pendingVideoFilePath = null;
                        _pendingTrackForBlock = null;
                        _pendingTimelineVmForBlock = null;
                        _pendingMainWindowForBlock = null;
                    }

                    // Start playback from current position with normal speed
                    VideoPlayer.Position = TimeSpan.Zero;
                    VideoPlayer.SpeedRatio = 1.0; // Normal playback speed
                    VideoPlayer.Play();
                    _isPlaying = true;
                    Debug.WriteLine("[Preview] Starting video playback at 1x speed");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[Preview] VideoPlayer NaturalDuration does not have TimeSpan");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Preview] Error in VideoPlayer_MediaOpened: {ex.Message}");
            }

            // Start position update timer only during playback
            StartPositionTimer();
        }

        private void StartPositionTimer()
        {
            _positionTimer?.Dispose();
            _positionTimer = new System.Threading.Timer(state =>
            {
                try
                {
                    var app = System.Windows.Application.Current;
                    if (app == null) return;

                    app.Dispatcher.Invoke(() =>
                    {
                        if (VideoPlayer != null && _isPlaying)
                        {
                            UpdatePosition();
                        }
                    });
                }
                catch (TaskCanceledException)
                {
                    // Ignore - app shutting down
                }
            }, null, 0, 100);
        }

        private void StopPositionTimer()
        {
            _positionTimer?.Dispose();
            _positionTimer = null;
        }

        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            // Reset to beginning when video ends
            VideoPlayer.Position = TimeSpan.Zero;
            _isPlaying = false;
            StopPositionTimer();
            if (DataContext is ViewModels.PreviewViewModel viewModel)
            {
                viewModel.IsPlaying = false;
            }
        }

        private void UpdatePosition()
        {
            if (VideoPlayer != null && VideoPlayer.Source != null && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var positionSeconds = VideoPlayer.Position.TotalSeconds;

                // Update view model
                if (DataContext is ViewModels.PreviewViewModel viewModel)
                {
                    viewModel.UpdatePositionFromTimer(positionSeconds);
                }

                // Slider removed - seek bar moved to TimelineView

                // Update timeline playhead (without auto-scroll during playback)
                if (Window.GetWindow(this) is MainWindow mainWindow &&
                    mainWindow.TimelineViewControl.DataContext is ViewModels.TimelineViewModel timelineVm)
                {
                    var fps = 30.0;
                    var currentFrame = (int)(positionSeconds * fps);
                    timelineVm.CurrentFrame = currentFrame;
                }
            }
        }

        private System.Threading.Timer? _positionTimer;
        private bool _isPlaying = false;
        private string? _pendingVideoFilePath;
        private ViewModels.TimelineTrackViewModel? _pendingTrackForBlock;
        private ViewModels.TimelineViewModel? _pendingTimelineVmForBlock;
        private MainWindow? _pendingMainWindowForBlock;

        // プレビュー更新用
        private string? _lastVideoPath;

        private void PreviewGrid_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            var filePath = files[0];
            var extension = Path.GetExtension(filePath).ToLower();

            // 動画ファイルのみ受け付ける
            var videoExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mkv", ".webm", ".flv", ".m4v" };
            if (!videoExtensions.Contains(extension))
            {
                MessageBox.Show("動画ファイル（.mp4, .avi, .mov, .wmv, .mkv, .webm, .flv, .m4v）をドラッグしてください",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DataContext is ViewModels.PreviewViewModel viewModel)
            {
                viewModel.AddVideoFile(filePath);
                DropHintText.Visibility = Visibility.Collapsed;

                // Add to timeline via MainWindow's TimelineViewControl
                if (Window.GetWindow(this) is MainWindow mainWindow)
                {
                    try
                    {
                        if (mainWindow.TimelineViewControl.DataContext is ViewModels.TimelineViewModel timelineVm)
                        {
                            // Create tracks if none exist
                            while (timelineVm.Tracks.Count < 2)
                            {
                                timelineVm.AddTrackInternal();
                            }

                            // Create video block on second track (index 1)
                            int trackIndex = timelineVm.Tracks.Count > 1 ? 1 : 0;
                            var track = timelineVm.Tracks[trackIndex];

                            // Get video duration using Shell API
                            var durationSeconds = GetVideoDurationFromFile(filePath);

                            if (durationSeconds.HasValue)
                            {
                                var videoDuration = (int)(durationSeconds.Value * 30); // 30fps
                                System.Diagnostics.Debug.WriteLine($"Video duration from Shell API: {durationSeconds}s = {videoDuration} frames (at 30fps)");
                                System.Diagnostics.Debug.WriteLine($"Adding video block at frame: {timelineVm.CurrentFrame}");

                                var videoBlock = new Models.TimelineBlock
                                {
                                    CharacterId = Guid.NewGuid(),
                                    StartFrame = timelineVm.CurrentFrame,
                                    Duration = videoDuration,
                                    Text = $"動画: {Path.GetFileName(filePath)}",
                                    BackgroundColor = "#10b981",
                                    Type = Models.BlockType.Video,
                                    AudioPath = filePath,
                                    FontFamily = "Yu Gothic UI",
                                    FontSize = 14,
                                    TextColor = "#FFFFFF"
                                };

                                track.Items.Add(videoBlock);

                                // 総フレーム数を更新（現在のブロックの終了位置を考慮）
                                var endFrame = videoBlock.StartFrame + videoBlock.Duration;
                                var newTotalFrames = Math.Max(timelineVm.TotalFrames, endFrame + 5 * 30);
                                timelineVm.SetTotalFrames(newTotalFrames);
                                mainWindow.TimelineViewControl.UpdateRuler(newTotalFrames);

                                // Update status message
                                if (mainWindow.ViewModel != null)
                                {
                                    var minutes = (int)(durationSeconds.Value / 60);
                                    var seconds = (int)(durationSeconds.Value % 60);
                                    mainWindow.ViewModel.StatusMessage = $"動画追加: {Path.GetFileName(filePath)} ({minutes}分{seconds}秒)";
                                }
                            }
                            else
                            {
                                // Fallback: Use default duration and let MediaOpened update it
                                System.Diagnostics.Debug.WriteLine("Failed to get video duration from Shell API, will try MediaOpened");
                                _pendingVideoFilePath = filePath;
                                _pendingTrackForBlock = track;
                                _pendingTimelineVmForBlock = timelineVm;
                                _pendingMainWindowForBlock = mainWindow;
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("TimelineViewControl DataContext is null");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error adding to timeline: {ex.Message}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("MainWindow not found");
                }
            }
        }


        private void PreviewGrid_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    var extension = Path.GetExtension(files[0]).ToLower();
                    var videoExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mkv", ".webm", ".flv", ".m4v" };
                    if (videoExtensions.Contains(extension))
                    {
                        e.Effects = DragDropEffects.Copy;
                        DropHintText.Visibility = Visibility.Visible;
                        e.Handled = true;
                        return;
                    }
                }
            }

            e.Effects = DragDropEffects.None;
            DropHintText.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }

        public void StopPlayback()
        {
            VideoPlayer.Stop();
            VideoPlayer.Position = TimeSpan.Zero;
            _isPlaying = false;
            // PlayPauseButton removed
            if (DataContext is ViewModels.PreviewViewModel viewModel)
            {
                viewModel.CurrentPosition = 0;
                viewModel.UpdatePositionFromTimer(0);
            }
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            // Return to beginning without playing
            VideoPlayer.Position = TimeSpan.Zero;
            _isPlaying = false;
            // PlayPauseButton removed
            StopPositionTimer();
            if (DataContext is ViewModels.PreviewViewModel viewModel)
            {
                viewModel.CurrentPosition = 0;
                viewModel.UpdatePositionFromTimer(0);
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            VideoPlayer.Stop();
            VideoPlayer.Position = TimeSpan.Zero;
            _isPlaying = false;
            // PlayPauseButton removed
            StopPositionTimer();
            if (DataContext is ViewModels.PreviewViewModel viewModel)
            {
                viewModel.CurrentPosition = 0;
                viewModel.UpdatePositionFromTimer(0);
            }
        }

        private void PreviewSeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Seek to the new position when slider value changes
            if (Window.GetWindow(this) is MainWindow mainWindow &&
                DataContext is ViewModels.PreviewViewModel vm)
            {
                var newFrame = (int)e.NewValue;
                vm.CurrentFrame = newFrame;

                // Update video position if playing
                if (VideoPlayer.Source != null && VideoPlayer.NaturalDuration.HasTimeSpan)
                {
                    var fps = 30.0;
                    var positionSeconds = newFrame / fps;
                    VideoPlayer.Position = TimeSpan.FromSeconds(positionSeconds);
                }

                // Update timeline playhead
                if (mainWindow.TimelineViewControl.DataContext is ViewModels.TimelineViewModel timelineVm)
                {
                    timelineVm.CurrentFrame = newFrame;
                }
            }
        }
    }
}
