using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VideoCreatorWPF.Commands;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.ViewModels
{
    public class PreviewViewModel : ViewModelBase
    {
        private readonly ProjectViewModel _project;
        private ImageSource? _previewImage;
        private string _currentText = "";
        private int _currentFrame;
        private bool _isPlaying;
        private double _currentPosition;
        private double _duration;
        private MediaPlayer? _mediaPlayer;
        private string? _currentVideoPath;
        private int _currentVideoStartFrame;
        private string? _standingImagePath;
        private string? _emotionEffectPath;

        public PreviewViewModel(ProjectViewModel project)
        {
            _project = project;
            PlayCommand = new RelayCommand(_ => Play(), _ => true);
            PauseCommand = new RelayCommand(_ => Pause(), _ => true);
            StopCommand = new RelayCommand(_ => Stop(), _ => true);
        }

        /// <summary>
        /// 立ち絵画像パス
        /// </summary>
        public string? StandingImagePath
        {
            get => _standingImagePath;
            set
            {
                SetProperty(ref _standingImagePath, value);
                OnPropertyChanged(nameof(HasStandingImage));
            }
        }

        /// <summary>
        /// 感情エフェクト画像パス
        /// </summary>
        public string? EmotionEffectPath
        {
            get => _emotionEffectPath;
            set => SetProperty(ref _emotionEffectPath, value);
        }

        /// <summary>
        /// 立ち絵画像が存在するか
        /// </summary>
        public bool HasStandingImage => !string.IsNullOrEmpty(_standingImagePath);

        /// <summary>
        /// プレイヘッド位置にある動画ブロックのパスを取得
        /// </summary>
        public string? CurrentVideoPath
        {
            get => _currentVideoPath;
            set => SetProperty(ref _currentVideoPath, value);
        }

        /// <summary>
        /// プレイヘッド位置にある動画ブロックの開始フレーム
        /// </summary>
        public int CurrentVideoStartFrame
        {
            get => _currentVideoStartFrame;
            set => SetProperty(ref _currentVideoStartFrame, value);
        }

        /// <summary>
        /// プレイヘッド位置にある全てのテキストブロック（DialogueとSubtitleを含む）
        /// </summary>
        public IEnumerable<TimelineBlock> CurrentTextBlocks
        {
            get
            {
                foreach (var track in _project.Tracks)
                {
                    foreach (var block in track.Items)
                    {
                        // Dialogue and Subtitle blocks
                        if (block.Type != BlockType.Dialogue && block.Type != BlockType.Subtitle) continue;

                        // プレイヘッド位置にテキストがあるかチェック
                        if (_currentFrame >= block.StartFrame && _currentFrame < block.StartFrame + block.Duration)
                        {
                            yield return block;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// プレイヘッド位置にある全ての画像ブロック
        /// </summary>
        public IEnumerable<TimelineBlock> CurrentImageBlocks
        {
            get
            {
                foreach (var track in _project.Tracks)
                {
                    foreach (var block in track.Items)
                    {
                        // Image blocks only
                        if (block.Type != BlockType.Image) continue;

                        // プレイヘッド位置に画像があるかチェック
                        if (_currentFrame >= block.StartFrame && _currentFrame < block.StartFrame + block.Duration)
                        {
                            yield return block;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// プレイヘッド位置を更新して関連プロパティも更新
        /// </summary>
        public void UpdateFrame(int frame)
        {
            CurrentFrame = frame;

            // デバッグ: 総トラック数とブロック数を表示
            Debug.WriteLine($"[Preview] UpdateFrame: frame={frame}, tracks={_project.Tracks.Count}");
            int totalBlocks = _project.Tracks.Sum(t => t.Items.Count);
            Debug.WriteLine($"[Preview] Total blocks: {totalBlocks}");

            // プレイヘッド位置の動画ブロックを探す
            TimelineBlock? videoBlock = null;
            foreach (var track in _project.Tracks)
            {
                foreach (var block in track.Items)
                {
                    Debug.WriteLine($"[Preview] Block: Type={block.Type}, Start={block.StartFrame}, Duration={block.Duration}, AudioPath={block.AudioPath}");
                    if (block.Type == BlockType.Video &&
                        frame >= block.StartFrame && frame < block.StartFrame + block.Duration)
                    {
                        Debug.WriteLine($"[Preview] Found video block at frame {frame}: {block.AudioPath}");
                        videoBlock = block;
                        break;
                    }
                }
                if (videoBlock != null) break;
            }

            if (videoBlock != null)
            {
                _currentVideoPath = videoBlock.AudioPath;
                _currentVideoStartFrame = videoBlock.StartFrame;
                OnPropertyChanged(nameof(CurrentVideoPath));
                OnPropertyChanged(nameof(CurrentVideoStartFrame));
                Debug.WriteLine($"[Preview] Setting CurrentVideoPath: {videoBlock.AudioPath}");
            }
            else
            {
                // Clean up MediaPlayer when no video block exists
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Close();
                    _mediaPlayer = null;
                    MediaPlayer = null;
                }

                _currentVideoPath = null;
                _currentVideoStartFrame = 0;
                OnPropertyChanged(nameof(CurrentVideoPath));
                OnPropertyChanged(nameof(CurrentVideoStartFrame));
                Debug.WriteLine($"[Preview] No video block found at frame {frame}, cleared MediaPlayer");
            }

            // テキストブロックの更新を通知
            OnPropertyChanged(nameof(CurrentTextBlocks));

            // 現在のテキストを最初のブロックから取得
            var textBlock = CurrentTextBlocks.FirstOrDefault();
            if (textBlock != null)
            {
                CurrentText = textBlock.Text;
            }
            else
            {
                CurrentText = "";
            }
        }

        public ImageSource? PreviewImage
        {
            get => _previewImage;
            set => SetProperty(ref _previewImage, value);
        }

        public string CurrentText
        {
            get => _currentText;
            set => SetProperty(ref _currentText, value);
        }

        public int CurrentFrame
        {
            get => _currentFrame;
            set => SetProperty(ref _currentFrame, value);
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetProperty(ref _isPlaying, value);
        }

        public double CurrentPosition
        {
            get => _currentPosition;
            set
            {
                if (SetProperty(ref _currentPosition, value))
                {
                    OnPropertyChanged(nameof(CurrentPositionText));
                }
            }
        }

        public double Duration
        {
            get => _duration;
            set => SetProperty(ref _duration, value);
        }

        public string CurrentPositionText
        {
            get
            {
                var ts = TimeSpan.FromSeconds(_currentPosition);
                return $"{ts:mm\\:ss}";
            }
        }

        public string DurationText
        {
            get
            {
                var ts = TimeSpan.FromSeconds(_duration);
                return $"{ts:mm\\:ss}";
            }
        }

        public MediaPlayer? MediaPlayer
        {
            get => _mediaPlayer;
            set => SetProperty(ref _mediaPlayer, value);
        }

        private string? _videoPath;
        public string? VideoPath
        {
            get => _videoPath;
            set => SetProperty(ref _videoPath, value);
        }

        public int Width => _project.Width;
        public int Height => _project.Height;

        public ICommand PlayCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand StopCommand { get; }

        private System.Threading.Timer? _playTimer;

        public void Play()
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Play();
                IsPlaying = true;
            }
            else
            {
                IsPlaying = true;
                _playTimer = new System.Threading.Timer(_ =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        CurrentFrame++;
                        CurrentText = $"Frame {CurrentFrame}";
                    });
                }, null, 0, 33); // ~30fps
            }
        }

        public void Pause()
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Pause();
            }
            IsPlaying = false;
            _playTimer?.Dispose();
            _playTimer = null;
        }

        public void Stop()
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Position = TimeSpan.Zero;
            }
            IsPlaying = false;
            _playTimer?.Dispose();
            _playTimer = null;
            CurrentFrame = 0;
            CurrentPosition = 0;
            CurrentText = "Frame 0";
        }

        public void UpdatePreview(int frame)
        {
            CurrentFrame = frame;
            // Display current block text if any
            CurrentText = $"Frame {frame}";
        }

        public void LoadPreviewImage(string imagePath)
        {
            try
            {
                if (File.Exists(imagePath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    PreviewImage = bitmap;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"画像読み込みエラー: {ex.Message}");
            }
        }

        public void AddVideoFile(string videoPath)
        {
            Debug.WriteLine($"Video file added: {videoPath}");
            CurrentText = $"動画追加: {Path.GetFileName(videoPath)}";

            // Set the video path for MediaElement to use
            VideoPath = videoPath;

            // Create MediaPlayer for tracking duration and position
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.Open(new Uri(videoPath));
            _mediaPlayer.MediaOpened += (s, e) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Duration = _mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                    OnPropertyChanged(nameof(DurationText));
                });
            };

            // Update position periodically
            var positionTimer = new System.Threading.Timer(_ =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_mediaPlayer != null && IsPlaying)
                    {
                        CurrentPosition = _mediaPlayer.Position.TotalSeconds;
                        OnPropertyChanged(nameof(CurrentPositionText));
                    }
                });
            }, null, 0, 100);
        }

        public void SeekToPosition(double position)
        {
            if (_mediaPlayer != null)
            {
                var duration = _mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                var clampedPosition = Math.Max(0, Math.Min(position, duration));
                _mediaPlayer.Position = TimeSpan.FromSeconds(clampedPosition);
                CurrentPosition = clampedPosition;
                OnPropertyChanged(nameof(CurrentPositionText));
            }
        }

        public void UpdateDuration(double duration)
        {
            Duration = duration;
            OnPropertyChanged(nameof(DurationText));
        }

        public void UpdatePositionFromTimer(double position)
        {
            CurrentPosition = position;
            OnPropertyChanged(nameof(CurrentPositionText));
        }

        /// <summary>
        /// 指定フレームのプレビュー画像を生成（FFMpegCore使用）
        /// </summary>
        public async System.Threading.Tasks.Task GeneratePreviewFrame(int frame)
        {
            CurrentFrame = frame;

            // 現在のフレームに対応するタイムスタンプを計算
            var frameRate = _project.FrameRate;
            var timestamp = frame / frameRate;
            var ts = TimeSpan.FromSeconds(timestamp);

            // 一時ファイルに出力
            var tempFile = Path.GetTempFileName() + ".png";

            try
            {
                // タイムライン上の最初の動画ブロックを探す
                var firstVideoBlock = _project.Tracks
                    .SelectMany(t => t.Items)
                    .FirstOrDefault(b => b.Type == Models.BlockType.Video &&
                                         frame >= b.StartFrame &&
                                         frame < b.StartFrame + b.Duration);

                if (firstVideoBlock != null && !string.IsNullOrEmpty(firstVideoBlock.VideoPath)
                    && File.Exists(firstVideoBlock.VideoPath))
                {
                    // FFMpegCoreを使って指定フレームを抽出
                    var blockFrame = frame - firstVideoBlock.StartFrame;
                    var blockTimestamp = blockFrame / frameRate;
                    var blockTs = TimeSpan.FromSeconds(blockTimestamp);

                    Debug.WriteLine($"[Preview] Extracting frame {blockFrame} from {firstVideoBlock.VideoPath}");

                    // FFMpegCoreでフレームを抽出
                    var success = await FFMpegCore.FFMpegArguments
                        .FromFileInput(firstVideoBlock.VideoPath)
                        .OutputToFile(tempFile, overwrite: true, options => options
                            .ForceFormat("image2")
                            .WithCustomArgument($"-ss {blockTs.ToString(@"hh\:mm\:ss\.fff")} -vframes 1 -q:v 2"))
                        .ProcessAsynchronously();

                    if (success && File.Exists(tempFile))
                    {
                        // 画像読み込み
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(tempFile);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();

                        PreviewImage = bitmap;
                        CurrentText = $"Frame {frame} - {Path.GetFileName(firstVideoBlock.VideoPath)}";
                    }
                    else
                    {
                        // FFMpegCore失敗時はfallback
                        Debug.WriteLine("[Preview] FFMpegCore failed, using fallback");
                        await GenerateFallbackPreview(frame);
                    }
                }
                else
                {
                    // 動画ブロックがない場合はfallback
                    await GenerateFallbackPreview(frame);
                }

                File.Delete(tempFile);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"プレビュー生成エラー: {ex.Message}");
                if (File.Exists(tempFile)) File.Delete(tempFile);
                await GenerateFallbackPreview(frame);
            }
        }

        /// <summary>
        /// フォールバック用プレビュー画像生成
        /// </summary>
        private async System.Threading.Tasks.Task GenerateFallbackPreview(int frame)
        {
            var width = _project.Width;
            var height = _project.Height;

            // 簡易プレビュー：黒背景にテキスト
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(System.Windows.Media.Brushes.Black, null,
                    new System.Windows.Rect(0, 0, width, height));

                var text = new FormattedText(
                    $"Frame {frame}",
                    System.Globalization.CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface("Yu Gothic UI"),
                    48,
                    System.Windows.Media.Brushes.White);

                var x = (width - text.Width) / 2;
                var y = (height - text.Height) / 2;
                drawingContext.DrawText(text, new System.Windows.Point(x, y));
            }

            var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(drawingVisual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));

            var tempFile = Path.GetTempFileName() + ".png";
            using (var fileStream = new FileStream(tempFile, FileMode.Create))
            {
                encoder.Save(fileStream);
            }

            // 画像読み込み
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(tempFile);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            PreviewImage = bitmap;
            CurrentText = $"Frame {frame} / {_project.Tracks.Sum(t => t.Items.Count)} blocks";

            File.Delete(tempFile);
        }

        public void SetMediaElementSource(System.Windows.Controls.MediaElement mediaElement, string videoPath)
        {
            mediaElement.Source = new Uri(videoPath);
        }

        /// <summary>
        /// キャラクターの立ち絵画像を読み込む
        /// </summary>
        public void LoadStandingImage(Guid characterId)
        {
            var character = _project.Characters.FirstOrDefault(c => c.Id == characterId);
            if (character != null && !string.IsNullOrEmpty(character.ImagePath) && File.Exists(character.ImagePath))
            {
                try
                {
                    var bitmap = new BitmapImage(new Uri(character.ImagePath));
                    StandingImagePath = character.ImagePath;
                    Debug.WriteLine($"[PreviewViewModel] Loaded standing image: {character.ImagePath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PreviewViewModel] Error loading standing image: {ex.Message}");
                    StandingImagePath = null;
                }
            }
            else
            {
                StandingImagePath = null;
            }
        }

        /// <summary>
        /// テキストブロックの文字列から感情エフェクトを判定
        /// </summary>
        public void DetectEmotionFromText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                EmotionEffectPath = null;
                return;
            }

            // Emotion detection mapping (to be implemented with advanced NLP in the future)
            // 簡易版: キーワードに基づく感情判定
            var emotionMap = new Dictionary<string, string>
            {
                { "笑", "emotions/joy.png" },
                { "泣", "emotions/cry.png" },
                { "怒", "emotions/anger.png" },
                { "驚", "emotions/surprise.png" }
            };

            foreach (var kvp in emotionMap)
            {
                if (text.Contains(kvp.Key))
                {
                    var basePath = AppDomain.CurrentDomain.BaseDirectory;
                    var fullPath = Path.Combine(basePath, kvp.Value);
                    if (File.Exists(fullPath))
                    {
                        EmotionEffectPath = fullPath;
                        Debug.WriteLine($"[PreviewViewModel] Detected emotion: {kvp.Key} -> {fullPath}");
                        return;
                    }
                }
            }

            EmotionEffectPath = null;
        }
    }
}
