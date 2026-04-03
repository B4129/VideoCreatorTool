using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VideoCreatorWPF.ViewModels;
using VideoCreatorWPF.Views;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF
{
    public partial class MainWindow : Window
    {
        public MainWindowViewModel ViewModel { get; }
        private System.Threading.Timer? _autoSaveTimer;
        private string? _currentProjectPath;
        private const int AutoSaveInterval = 30000; // 30 seconds
        private TimelineViewModel? _timelineViewModel;

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainWindowViewModel();
            DataContext = ViewModel;

            // 自動保存タイマーを開始
            StartAutoSaveTimer();

            // キャラクターリスト/タイムライン/プレビューのViewModelをバインド
            var propertyChanged = new System.ComponentModel.PropertyChangedEventHandler((sender, e) =>
            {
                if (e.PropertyName == nameof(MainWindowViewModel.CurrentProjectViewModel))
                {
                    if (ViewModel.CurrentProjectViewModel != null)
                    {
                        _timelineViewModel = new TimelineViewModel(ViewModel.CurrentProjectViewModel);
                        TimelineViewControl.DataContext = _timelineViewModel;
                        PreviewViewControl.DataContext = new PreviewViewModel(ViewModel.CurrentProjectViewModel);

                        // Subscribe to CurrentFrame changes to update playhead
                        _timelineViewModel.PropertyChanged += (s, args) =>
                        {
                            if (args.PropertyName == nameof(TimelineViewModel.CurrentFrame))
                            {
                                TimelineViewControl.RefreshPlayhead();
                            }
                        };

                        // Update Space key binding to use TimelineViewModel's PlayCommand
                        UpdateSpaceKeyBinding();
                    }
                }
            });
            ViewModel.PropertyChanged += propertyChanged;

            // キーボードショートカットを設定（初期設定）
            InputBindings.Add(new InputBinding(ViewModel.NewProjectCommand, new KeyGesture(Key.N, ModifierKeys.Control)));
            InputBindings.Add(new InputBinding(ViewModel.OpenProjectCommand, new KeyGesture(Key.O, ModifierKeys.Control)));
            InputBindings.Add(new InputBinding(ViewModel.SaveProjectCommand, new KeyGesture(Key.S, ModifierKeys.Control)));
            InputBindings.Add(new InputBinding(ViewModel.PlayCommand, new KeyGesture(Key.Space)));

            // ウィンドウ読み込み時にスクリーンショットを撮影
            Loaded += MainWindow_Loaded;
        }

        // Text formatting state
        private string _currentFontFamily = "Yu Gothic UI";
        private double _currentFontSize = 14.0;
        private string _currentTextColor = "#FFFFFF";

        private void FontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
            {
                _currentFontFamily = item.Content.ToString();
            }
        }

        private void FontSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
            {
                if (double.TryParse(item.Content.ToString(), out var size))
                {
                    _currentFontSize = size;
                }
            }
        }

        private async void AddSubtitle_Click(object sender, RoutedEventArgs e)
        {
            // Add text and audio blocks to the timeline with current formatting
            if (ViewModel.CurrentProjectViewModel == null ||
                TimelineViewControl.DataContext is not TimelineViewModel timelineVm ||
                timelineVm.Tracks.Count == 0)
            {
                return;
            }

            var text = SubtitleInput.Text;
            if (string.IsNullOrEmpty(text)) return;

            // Get character name from ComboBox
            string characterName = "ずんだもん";
            if (CharacterCombo.SelectedItem is ComboBoxItem selectedItem)
            {
                characterName = selectedItem.Content.ToString();
            }

            // Get speaker ID from character name
            int speakerId = GetSpeakerIdFromCharacterName(characterName);

            // 再生位置を固定（音声生成中に変更されないように）
            var playheadFrame = timelineVm.CurrentFrame;

            // Generate audio asynchronously
            var audioPath = await Services.VoiceVoxService.GenerateAudioFromText(text, speakerId);

            // Get audio duration (30fps)
            int blockDuration = 90; // Default: 3 seconds
            if (audioPath != null)
            {
                var metadata = await Services.MediaMetadataService.GetMetadataAsync(audioPath, Models.MediaType.Audio);
                if (metadata != null && metadata.Duration.HasValue)
                {
                    // Convert seconds to frames: duration * 30fps
                    blockDuration = (int)(metadata.Duration.Value * 30);
                }
            }

            // Find track where new block won't overlap with existing blocks
            ViewModels.TimelineTrackViewModel? targetTrack = null;

            // Calculate the end frame of the new block we want to add
            int newBlockEndFrame = playheadFrame + blockDuration;

            foreach (var track in timelineVm.Tracks)
            {
                // Check if any existing block overlaps with the new block's time range
                var hasOverlap = track.Items.Any(b =>
                {
                    int existingBlockEndFrame = b.StartFrame + b.Duration;
                    // Overlap occurs if: newStart < existingEnd AND newEnd > existingStart
                    return playheadFrame < existingBlockEndFrame && newBlockEndFrame > b.StartFrame;
                });

                if (!hasOverlap)
                {
                    targetTrack = track;
                    break;
                }
            }

            // If all tracks have overlapping blocks, create new track
            if (targetTrack == null)
            {
                timelineVm.AddTrackInternal();
                targetTrack = timelineVm.Tracks.Last();
            }

            // Create text block (yellow-green)
            var textBlock = new TimelineBlock
            {
                CharacterId = Guid.NewGuid(),
                StartFrame = playheadFrame,
                Duration = blockDuration,
                Text = text,
                BackgroundColor = "#7CFC00", // Yellow-green for text
                Type = BlockType.Dialogue,
                FontFamily = _currentFontFamily,
                FontSize = _currentFontSize,
                TextColor = _currentTextColor,
                TrackId = targetTrack.Items.Count > 0 ? targetTrack.Items.Last().TrackId : Guid.NewGuid()
            };

            // Add text block to target track
            targetTrack.Items.Add(textBlock);

            // If audio was generated, create audio block on next track with SAME duration
            var textTrackIndex = timelineVm.Tracks.IndexOf(targetTrack);
            if (audioPath != null && timelineVm.Tracks.Count > textTrackIndex + 1)
            {
                var audioTrack = timelineVm.Tracks[textTrackIndex + 1];
                var audioBlock = new TimelineBlock
                {
                    CharacterId = textBlock.CharacterId,
                    StartFrame = playheadFrame,
                    Duration = blockDuration, // Same duration as text block
                    Text = text,
                    AudioPath = audioPath,
                    BackgroundColor = "#FF4500", // Red for audio
                    Type = BlockType.Audio,
                    FontFamily = _currentFontFamily,
                    FontSize = _currentFontSize,
                    TextColor = _currentTextColor,
                    TrackId = audioTrack.Items.Count > 0 ? audioTrack.Items.Last().TrackId : Guid.NewGuid()
                };
                audioTrack.Items.Add(audioBlock);
            }
            else if (audioPath != null)
            {
                // Create new track for audio
                timelineVm.AddTrackInternal();
                var audioTrack = timelineVm.Tracks.Last();
                var audioBlock = new TimelineBlock
                {
                    CharacterId = textBlock.CharacterId,
                    StartFrame = playheadFrame,
                    Duration = blockDuration,
                    Text = text,
                    AudioPath = audioPath,
                    BackgroundColor = "#FF4500", // Red for audio
                    Type = BlockType.Audio,
                    FontFamily = _currentFontFamily,
                    FontSize = _currentFontSize,
                    TextColor = _currentTextColor,
                    TrackId = audioTrack.Items.Count > 0 ? audioTrack.Items.Last().TrackId : Guid.NewGuid()
                };
                audioTrack.Items.Add(audioBlock);
            }

            // プレイヘッドの位置を更新
            timelineVm.CurrentFrame = playheadFrame;
            TimelineViewControl.RefreshPlayhead();
            TimelineViewControl.ScrollToFrame(playheadFrame);

            ViewModel.StatusMessage = $"字幕追加: {textBlock.Text} (フレーム {playheadFrame} に配置)";
        }

        private int GetSpeakerIdFromCharacterName(string name)
        {
            return name switch
            {
                "ずんだもん" => 2,
                "四国めたん" => 1,
                "春日部つむぎ" => 8,
                _ => 2
            };
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.OpenSettingsCommand.Execute(null);
        }

        private void Shortcuts_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Views.ShortcutsDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        /// <summary>
        /// SRT字幕ファイルのインポート
        /// </summary>
        private void ImportSubtitle_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "SRT字幕ファイルを選択",
                Filter = "SRT字幕ファイル (*.srt)|*.srt|すべてのファイル (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var srtPath = dialog.FileName;
                    var project = ViewModel.CurrentProject;

                    if (project == null)
                    {
                        MessageBox.Show("プロジェクトが開かれていません。", "エラー",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // SubtitleServiceを使用してインポート
                    var success = VideoCreatorWPF.Services.SubtitleService.ImportToProject(
                        project, srtPath);

                    if (success)
                    {
                        MessageBox.Show("字幕をインポートしました。", "成功",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        // タイムラインを更新
                        ViewModel.RefreshTimeline();
                    }
                    else
                    {
                        MessageBox.Show("字幕のインポートに失敗しました。", "エラー",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"字幕インポート中にエラーが発生しました:\n{ex.Message}",
                        "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ウィンドウ読み込み時にスクリーンショットを自動撮影
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // UIが完全に描画されるのを待つ
            await Task.Delay(3000);

            await Dispatcher.InvokeAsync(() =>
            {
                // ssフォルダ用（既存）
                TakeScreenshot();

                // docsフォルダ用 - README用スクリーンショット
                TakeDocScreenshot("screenshot_main");

                // 各コンポーネントのスクリーンショットを撮影
                TakeComponentScreenshots();
            });
        }

        // スクリーンショットを撮影して保存（最新1つだけ）
        private void TakeScreenshot()
        {
            try
            {
                var width = (int)ActualWidth;
                var height = (int)ActualHeight;

                if (width <= 0 || height <= 0) return;

                var renderTarget = new RenderTargetBitmap(
                    width, height, 96, 96, PixelFormats.Pbgra32);
                renderTarget.Render(this);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));

                var ssDir = @"C:\Users\neko3\Desktop\agent\動画作成ツール\ss";
                if (!Directory.Exists(ssDir))
                {
                    Directory.CreateDirectory(ssDir);
                }

                var oldFiles = Directory.GetFiles(ssDir, "SS_*.png");
                foreach (var f in oldFiles)
                {
                    File.Delete(f);
                }

                var fileName = $"SS_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var filePath = Path.Combine(ssDir, fileName);

                using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                ViewModel.StatusMessage = $"ss\\{fileName} に保存";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Screenshot error: {ex.Message}");
            }
        }

        // ドキュメント用スクリーンショットを撮影（publicで外部から呼び出し可能）
        public void TakeDocScreenshot(string screenshotName)
        {
            try
            {
                // 少し待ってUIが完全に描画されるのを待つ
                System.Threading.Thread.Sleep(500);

                var width = (int)ActualWidth;
                var height = (int)ActualHeight;

                if (width <= 0 || height <= 0)
                {
                    System.Diagnostics.Debug.WriteLine("TakeDocScreenshot: Invalid window size");
                    return;
                }

                var renderTarget = new RenderTargetBitmap(
                    width, height, 96, 96, PixelFormats.Pbgra32);
                renderTarget.Render(this);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));

                // docsフォルダに保存
                var docsDir = @"C:\Users\neko3\Desktop\agent\動画作成ツール\VideoCreatorWPF\docs";
                if (!Directory.Exists(docsDir))
                {
                    Directory.CreateDirectory(docsDir);
                }

                var filePath = Path.Combine(docsDir, $"{screenshotName}.png");
                using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                ViewModel.StatusMessage = $"スクリーンショットを保存: docs\\{screenshotName}.png";
                System.Diagnostics.Debug.WriteLine($"Doc screenshot saved to: {filePath}");
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"スクリーンショットエラー: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Doc screenshot error: {ex.Message}");
            }
        }

        // 各コンポーネントのスクリーンショットを撮影
        private void TakeComponentScreenshots()
        {
            try
            {
                var docsDir = @"C:\Users\neko3\Desktop\agent\動画作成ツール\VideoCreatorWPF\docs";
                if (!Directory.Exists(docsDir))
                {
                    Directory.CreateDirectory(docsDir);
                }

                // タイムラインビューのスクリーンショット
                if (TimelineViewControl != null)
                {
                    SaveElementScreenshot(TimelineViewControl, "screenshot_timeline");
                }

                // プレビュービューのスクリーンショット
                if (PreviewViewControl != null)
                {
                    SaveElementScreenshot(PreviewViewControl, "screenshot_preview");
                }

                ViewModel.StatusMessage = $"コンポーネントスクリーンショット完了";
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"コンポーネントスクショエラー: {ex.Message}";
            }
        }

        // UI要素のスクリーンショットを撮影して保存
        private void SaveElementScreenshot(FrameworkElement element, string fileName)
        {
            try
            {
                var width = (int)element.ActualWidth;
                var height = (int)element.ActualHeight;

                if (width <= 0 || height <= 0) return;

                var renderTarget = new RenderTargetBitmap(
                    width, height, 96, 96, PixelFormats.Pbgra32);
                element.Arrange(new Rect(new Size(width, height)));
                renderTarget.Render(element);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));

                var docsDir = @"C:\Users\neko3\Desktop\agent\動画作成ツール\VideoCreatorWPF\docs";
                var filePath = Path.Combine(docsDir, $"{fileName}.png");

                using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                System.Diagnostics.Debug.WriteLine($"Component screenshot saved to: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Component screenshot error: {ex.Message}");
            }
        }

        // スペースキーのバインディングをTimelineViewModelのPlayCommandに更新
        private void UpdateSpaceKeyBinding()
        {
            if (_timelineViewModel != null)
            {
                // 既存のバインディングをクリア
                InputBindings.Clear();

                // 再度すべてのキーバインディングを設定（SpaceはTimelineViewModelを使用）
                InputBindings.Add(new InputBinding(ViewModel.NewProjectCommand, new KeyGesture(Key.N, ModifierKeys.Control)));
                InputBindings.Add(new InputBinding(ViewModel.OpenProjectCommand, new KeyGesture(Key.O, ModifierKeys.Control)));
                InputBindings.Add(new InputBinding(ViewModel.SaveProjectCommand, new KeyGesture(Key.S, ModifierKeys.Control)));
                InputBindings.Add(new InputBinding(_timelineViewModel.PlayCommand, new KeyGesture(Key.Space)));
            }
        }

        // 自動保存タイマーを開始
        private void StartAutoSaveTimer()
        {
            _autoSaveTimer = new System.Threading.Timer(_ =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AutoSaveProject();
                });
            }, null, AutoSaveInterval, AutoSaveInterval);
        }

        // プロジェクトを自動保存
        private async void AutoSaveProject()
        {
            try
            {
                if (ViewModel.CurrentProjectViewModel == null) return;

                // 保存先ディレクトリ（プロジェクトがある場合）またはデスクトップ
                var saveDir = !string.IsNullOrEmpty(_currentProjectPath)
                    ? Path.GetDirectoryName(_currentProjectPath)
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop", "VideoCreator_AutoSave");

                if (!Directory.Exists(saveDir))
                {
                    Directory.CreateDirectory(saveDir);
                }

                // ファイル名: AutoSave_YYYYMMDD_HHMMSS.json
                var fileName = $"AutoSave_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(saveDir, fileName);

                await SaveProjectToJson(filePath);
                _currentProjectPath = filePath;

                System.Diagnostics.Debug.WriteLine($"Auto-saved to: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-save error: {ex.Message}");
            }
        }

        // プロジェクトをJSONに保存
        private async Task SaveProjectToJson(string filePath)
        {
            var project = ViewModel.CurrentProjectViewModel;
            if (project == null) return;

            var projectData = new
            {
                Width = project.Width,
                Height = project.Height,
                FrameRate = project.FrameRate,
            };

            var json = System.Text.Json.JsonSerializer.Serialize(projectData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8);
        }

        // プロジェクトを読み込む
        private async Task LoadProjectFromJson(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);
                var projectData = System.Text.Json.JsonSerializer.Deserialize<JsonProjectData>(json);
                if (projectData == null) return;

                _currentProjectPath = filePath;
                ViewModel.StatusMessage = $"プロジェクトを読み込みました: {Path.GetFileName(filePath)}";
                // TODO: プロジェクト復元処理を実装
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load project error: {ex.Message}");
            }
        }

        // JSON逆シリアル化用データクラス
        private class JsonProjectData
        {
            public string ProjectName { get; set; } = "";
            public int Width { get; set; }
            public int Height { get; set; }
            public double FrameRate { get; set; }
            public DateTime CreatedAt { get; set; }
            public List<JsonTrackData> Tracks { get; set; } = new();
        }

        private class JsonTrackData
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = "";
            public bool IsVisible { get; set; }
            public bool IsEnabled { get; set; }
            public double Volume { get; set; }
            public bool IsMuted { get; set; }
            public List<JsonBlockData> Blocks { get; set; } = new();
        }

        private class JsonBlockData
        {
            public Guid Id { get; set; }
            public Guid CharacterId { get; set; }
            public int StartFrame { get; set; }
            public int Duration { get; set; }
            public string Text { get; set; } = "";
            public string BackgroundColor { get; set; } = "";
            public string Type { get; set; } = "";
            public string? AudioPath { get; set; }
            public string FontFamily { get; set; } = "";
            public double FontSize { get; set; }
            public string TextColor { get; set; } = "";
        }
    }
}
