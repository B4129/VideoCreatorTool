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

        public PreviewViewModel(ProjectViewModel project)
        {
            _project = project;
            PlayCommand = new RelayCommand(_ => Play(), _ => true);
            PauseCommand = new RelayCommand(_ => Pause(), _ => true);
            StopCommand = new RelayCommand(_ => Stop(), _ => true);
        }

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
        /// プレイヘッド位置にある全てのテキストブロック（音声・動画ブロックは除外）
        /// </summary>
        public IEnumerable<TimelineBlock> CurrentTextBlocks
        {
            get
            {
                foreach (var track in _project.Tracks)
                {
                    foreach (var block in track.Items)
                    {
                        // Dialogue blocks only (skip audio, video, and other block types)
                        if (block.Type != BlockType.Dialogue) continue;

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
                _currentVideoPath = null;
                _currentVideoStartFrame = 0;
                OnPropertyChanged(nameof(CurrentVideoPath));
                OnPropertyChanged(nameof(CurrentVideoStartFrame));
                Debug.WriteLine($"[Preview] No video block found at frame {frame}");
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
        /// 指定フレームのプレビュー画像を生成（ffmpeg使用）
        /// </summary>
        public async System.Threading.Tasks.Task GeneratePreviewFrame(int frame)
        {
            CurrentFrame = frame;

            // ffmpegで指定フレームの画像を生成
            var ffmpegPath = FindFfmpeg();
            if (ffmpegPath == null)
            {
                CurrentText = $"Frame {frame} (ffmpeg not found)";
                return;
            }

            // 現在のフレームに対応するタイムスタンプを計算
            var frameRate = _project.FrameRate;
            var timestamp = frame / frameRate;
            var ts = TimeSpan.FromSeconds(timestamp);
            var tsStr = $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";

            // 一時ファイルに出力
            var tempFile = Path.GetTempFileName() + ".png";

            try
            {
                // ffmpegコマンド: ffmpeg -ss <timestamp> -i video.mp4 -vframes 1 -y output.png
                // 注意: 実際のプロジェクトではエクスポート前の動画を生成する必要がある
                // 暫定実装として黒背景にフレーム番号を表示

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
            catch (Exception ex)
            {
                Debug.WriteLine($"プレビュー生成エラー: {ex.Message}");
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        /// <summary>
        /// ffmpeg.exeを検索
        /// </summary>
        private static string? FindFfmpeg()
        {
            var paths = new[]
            {
                "ffmpeg.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", "ffmpeg.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "ffmpeg", "bin", "ffmpeg.exe"),
            };

            foreach (var path in paths)
            {
                if (File.Exists(path)) return path;
            }

            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv != null)
            {
                foreach (var dir in pathEnv.Split(';'))
                {
                    var ffmpegPath = Path.Combine(dir, "ffmpeg.exe");
                    if (File.Exists(ffmpegPath)) return ffmpegPath;
                }
            }

            return null;
        }

        public void SetMediaElementSource(System.Windows.Controls.MediaElement mediaElement, string videoPath)
        {
            mediaElement.Source = new Uri(videoPath);
        }
    }
}
