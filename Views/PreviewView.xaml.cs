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
            try
            {
                // Update duration when video is loaded
                if (VideoPlayer.NaturalDuration.HasTimeSpan)
                {
                    var durationSeconds = VideoPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                    System.Diagnostics.Debug.WriteLine($"VideoPlayer MediaOpened: {durationSeconds} seconds");

                    if (DataContext is ViewModels.PreviewViewModel viewModel)
                    {
                        viewModel.UpdateDuration(durationSeconds);
                    }

                    // Update TimelineViewModel duration (in frames at 30fps)
                    if (Window.GetWindow(this) is MainWindow mainWindow &&
                        mainWindow.TimelineViewControl.DataContext is ViewModels.TimelineViewModel timelineVm)
                    {
                        var totalFrames = (int)(durationSeconds * 30);
                        System.Diagnostics.Debug.WriteLine($"Setting total frames: {totalFrames} (from {durationSeconds}s at 30fps)");
                        timelineVm.SetTotalFrames(totalFrames);
                        mainWindow.TimelineViewControl.UpdateRuler(totalFrames);
                    }

                    // Create video block on timeline if pending
                    if (_pendingVideoFilePath != null && _pendingTrackForBlock != null && _pendingTimelineVmForBlock != null && _pendingMainWindowForBlock != null)
                    {
                        var videoDuration = (int)(durationSeconds * 30); // 30fps
                        System.Diagnostics.Debug.WriteLine($"Creating video block with duration: {videoDuration} frames (at 30fps)");

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
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("VideoPlayer NaturalDuration does not have TimeSpan");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in VideoPlayer_MediaOpened: {ex.Message}");
            }

            // Start position update timer
            _positionTimer = new System.Threading.Timer(_ =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdatePosition();
                });
            }, null, 0, 100);
        }

        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            // Reset to beginning when video ends
            VideoPlayer.Position = TimeSpan.Zero;
            _isPlaying = false;
            PlayPauseButton.Content = "▶";
            if (DataContext is ViewModels.PreviewViewModel viewModel)
            {
                viewModel.IsPlaying = false;
            }
        }

        private void UpdatePosition()
        {
            if (VideoPlayer.Source != null && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var positionSeconds = VideoPlayer.Position.TotalSeconds;

                // Update view model
                if (DataContext is ViewModels.PreviewViewModel viewModel)
                {
                    viewModel.UpdatePositionFromTimer(positionSeconds);
                }

                // Slider removed - seek bar moved to TimelineView

                // Update timeline playhead
                if (Window.GetWindow(this) is MainWindow mainWindow &&
                    mainWindow.TimelineViewControl.DataContext is ViewModels.TimelineViewModel timelineVm)
                {
                    var fps = 30.0;
                    var currentFrame = (int)(positionSeconds * fps);
                    timelineVm.CurrentFrame = currentFrame;

                    // Auto-scroll timeline to keep playhead visible
                    mainWindow.TimelineViewControl.ScrollToFrame(currentFrame);
                }
            }
        }

        private System.Threading.Timer? _positionTimer;
        private bool _isPlaying = false;
        private string? _pendingVideoFilePath;
        private ViewModels.TimelineTrackViewModel? _pendingTrackForBlock;
        private ViewModels.TimelineViewModel? _pendingTimelineVmForBlock;
        private MainWindow? _pendingMainWindowForBlock;

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

                                // Update timeline total frames
                                timelineVm.SetTotalFrames(videoDuration);
                                mainWindow.TimelineViewControl.UpdateRuler(videoDuration);

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

                            // Set main video player source
                            VideoPlayer.Source = new Uri(filePath);

                            // Set main video player source
                            VideoPlayer.Source = new Uri(filePath);
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
            PlayPauseButton.Content = "▶";
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
            PlayPauseButton.Content = "▶";
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            VideoPlayer.Stop();
            VideoPlayer.Position = TimeSpan.Zero;
            _isPlaying = false;
            PlayPauseButton.Content = "▶";
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (VideoPlayer.Source == null) return;

            // Check if video is at end, restart from beginning
            if (VideoPlayer.NaturalDuration.HasTimeSpan && VideoPlayer.Position >= VideoPlayer.NaturalDuration.TimeSpan)
            {
                VideoPlayer.Position = TimeSpan.Zero;
                VideoPlayer.Play();
                _isPlaying = true;
                PlayPauseButton.Content = "⏸";
                return;
            }

            // Toggle play/pause based on internal state
            if (_isPlaying)
            {
                VideoPlayer.Pause();
                _isPlaying = false;
                PlayPauseButton.Content = "▶";
            }
            else
            {
                VideoPlayer.Play();
                _isPlaying = true;
                PlayPauseButton.Content = "⏸";
            }
        }

        // Preview enhancement event handlers
        private void ResolutionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResolutionCombo.SelectedItem is not ComboBoxItem item) return;

            var scale = 1.0;
            if (item.Tag is string tag)
            {
                double.TryParse(tag, out scale);
            }

            // Apply resolution scale to preview
            PreviewGrid.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
        }

        private void GridToggle_Click(object sender, RoutedEventArgs e)
        {
            if (GridLinesCanvas != null)
            {
                GridLinesCanvas.Visibility = GridToggle.IsChecked == true
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            }
        }

        private void SafeAreaToggle_Click(object sender, RoutedEventArgs e)
        {
            if (SafeAreaCanvas != null)
            {
                SafeAreaCanvas.Visibility = SafeAreaToggle.IsChecked == true
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            }
        }

        private void SafeAreaCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (SafeAreaCanvas == null) return;

            var canvasWidth = e.NewSize.Width;
            var canvasHeight = e.NewSize.Height;

            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            // 90% safe area (5% margin on each side)
            var margin90 = canvasWidth * 0.05;
            Canvas.SetLeft(SafeArea90, margin90);
            Canvas.SetTop(SafeArea90, canvasHeight * 0.05);
            SafeArea90.Width = canvasWidth * 0.9;
            SafeArea90.Height = canvasHeight * 0.9;

            // 93% safe area (3.5% margin on each side)
            var margin93 = canvasWidth * 0.035;
            Canvas.SetLeft(SafeArea93, margin93);
            Canvas.SetTop(SafeArea93, canvasHeight * 0.035);
            SafeArea93.Width = canvasWidth * 0.93;
            SafeArea93.Height = canvasHeight * 0.93;
        }

        private void OnionSkinToggle_Click(object sender, RoutedEventArgs e)
        {
            if (OnionSkinCanvas != null)
            {
                OnionSkinCanvas.Visibility = OnionSkinToggle.IsChecked == true
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            }
        }

        private void FitModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FitModeCombo.SelectedItem is not ComboBoxItem item) return;
            var index = FitModeCombo.SelectedIndex;

            // Apply fit mode
            switch (index)
            {
                case 0: // Fit
                    VideoPlayer.Stretch = System.Windows.Media.Stretch.Uniform;
                    break;
                case 1: // Fill
                    VideoPlayer.Stretch = System.Windows.Media.Stretch.UniformToFill;
                    break;
                case 2: // 100%
                    VideoPlayer.Stretch = System.Windows.Media.Stretch.None;
                    break;
            }

            // No-op: Grid doesn't have StretchProperty
        }
    }
}
