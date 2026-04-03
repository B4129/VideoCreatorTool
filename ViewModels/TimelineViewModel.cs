using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using VideoCreatorWPF.Commands;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.ViewModels
{
    public class TimelineViewModel : ViewModelBase
    {
        internal readonly ProjectViewModel _project;
        private ObservableCollection<TimelineTrackViewModel> _tracks;
        private int _currentFrame;
        private bool _isPlaying;
        private double _zoom = 1.0;
        private readonly ObservableCollection<TimelineAction> _undoStack = new();
        private readonly ObservableCollection<TimelineAction> _redoStack = new();
        private bool _isSnapEnabled = true;
        private double _pixelsPerFrame = 0.5;
        private readonly ObservableCollection<TimelineBlock> _selectedBlocks = new();
        private const double MinPixelsPerFrame = 0.1;
        private const double MaxPixelsPerFrame = 5.0;

        public event Action<object, TimelineBlock>? BlockSelected;

        public TimelineViewModel(ProjectViewModel project)
        {
            _project = project;
            _tracks = new ObservableCollection<TimelineTrackViewModel>(
                _project.Tracks.Select(t => new TimelineTrackViewModel(t)));

            // Initialize grid lines
            UpdateGridLines();

            // Subscribe to collection changes on each track
            foreach (var track in _tracks)
            {
                track.Items.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalFrames));
            }

            // Subscribe to track add/remove
            _tracks.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (TimelineTrackViewModel track in e.NewItems)
                    {
                        track.Items.CollectionChanged += (s2, e2) => OnPropertyChanged(nameof(TotalFrames));
                    }
                }
                OnPropertyChanged(nameof(TotalFrames));
            };

            PlayCommand = new RelayCommand(_ => Play());
            PauseCommand = new RelayCommand(_ => Pause());
            StopCommand = new RelayCommand(_ => Stop());
            AddTrackCommand = new RelayCommand(_ => AddTrack());
            GoToStartCommand = new RelayCommand(_ => GoToStart());
            RemoveTrackCommand = new RelayCommand(_ => RemoveTrackInternal((TimelineTrackViewModel)_), _ => CurrentTrackForRemoval != null);
            SplitBlockCommand = new RelayCommand(_ => SplitSelectedBlocks(), _ => SelectedBlocks.Count > 0);
            ZoomInCommand = new RelayCommand(_ => ZoomIn());
            ZoomOutCommand = new RelayCommand(_ => ZoomOut());

            // Temporary dummy commands - will be implemented later
            UndoCommand = new RelayCommand(_ => Undo(), _ => CanUndo);
            RedoCommand = new RelayCommand(_ => Redo(), _ => CanRedo);
        }

        /// <summary>
        /// 現在のフレーム位置にある音声/動画ブロックを探して再生（再生開始時のみ）
        /// </summary>
        private void StartAudioBlocksAtCurrentFrame()
        {
            foreach (var track in _tracks)
            {
                foreach (var block in track.Items)
                {
                    // 音声ブロックで、現在のフレームがブロック内かチェック
                    if (block.Type == BlockType.Audio &&
                        CurrentFrame >= block.StartFrame &&
                        CurrentFrame < block.StartFrame + block.Duration &&
                        !string.IsNullOrEmpty(block.AudioPath) &&
                        !_playingAudioBlocks.Contains(block.Id))
                    {
                        // 現在のフレーム位置から再生（30fps換算）
                        var offsetSeconds = (CurrentFrame - block.StartFrame) / 30.0;
                        _playingAudioBlocks.Add(block.Id);
                        _ = Services.AudioService.PlayAudioAsync(block.AudioPath, offsetSeconds, block.PlaybackSpeed);

                        System.Diagnostics.Debug.WriteLine($"[Audio] Starting audio block at frame {CurrentFrame}: {block.AudioPath} (offset: {offsetSeconds:F2}s)");
                    }

                    // 動画ブロックで、現在のフレームがブロック内かチェック
                    if (block.Type == BlockType.Video &&
                        CurrentFrame >= block.StartFrame &&
                        CurrentFrame < block.StartFrame + block.Duration &&
                        !string.IsNullOrEmpty(block.AudioPath) &&
                        !_playingAudioBlocks.Contains(block.Id))
                    {
                        _playingAudioBlocks.Add(block.Id);
                        System.Diagnostics.Debug.WriteLine($"[Video] Starting video block at frame {CurrentFrame}: {block.AudioPath} (block start: {block.StartFrame})");

                        // MainWindow経由でプレビューの動画プレイヤーに通知
                        VideoBlockStarted?.Invoke(this, new VideoBlockEventArgs
                        {
                            VideoPath = block.AudioPath,
                            StartFrame = block.StartFrame,
                            Duration = block.Duration
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 動画ブロック開始イベント
        /// </summary>
        public event EventHandler<VideoBlockEventArgs>? VideoBlockStarted;

        /// <summary>
        /// プレイヘッド位置変更イベント
        /// </summary>
        public event EventHandler<int>? PlayheadPositionChanged;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public ObservableCollection<TimelineTrackViewModel> Tracks
        {
            get => _tracks;
            set => SetProperty(ref _tracks, value);
        }

        public ObservableCollection<TimelineBlock> SelectedBlocks
        {
            get => _selectedBlocks;
        }

        public double MinPixelsPerFrameConst => MinPixelsPerFrame;
        public double MaxPixelsPerFrameConst => MaxPixelsPerFrame;

        public ICommand PlayCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand AddTrackCommand { get; }
        public ICommand GoToStartCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand RemoveTrackCommand { get; }
        public ICommand SplitBlockCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }

        public TimelineTrackViewModel? CurrentTrackForRemoval { get; private set; }

        public int CurrentFrame
        {
            get => _currentFrame;
            set
            {
                if (SetProperty(ref _currentFrame, value))
                {
                    OnPropertyChanged(nameof(CurrentFrame));
                    OnPropertyChanged(nameof(CurrentPositionText));
                    OnPropertyChanged(nameof(DurationText));
                    PlayheadPositionChanged?.Invoke(this, value);
                }
            }
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetProperty(ref _isPlaying, value);
        }

        public double Zoom
        {
            get => _zoom;
            set => SetProperty(ref _zoom, value);
        }

        public bool IsSnapEnabled
        {
            get => _isSnapEnabled;
            set => SetProperty(ref _isSnapEnabled, value);
        }

        public double PixelsPerFrame
        {
            get => _pixelsPerFrame;
            set
            {
                if (SetProperty(ref _pixelsPerFrame, value))
                {
                    UpdateGridLines();
                    // PixelsPerFrameが変更されたらTotalFramesも更新通知
                    OnPropertyChanged(nameof(TotalFrames));
                    OnPropertyChanged(nameof(GridLines));
                }
            }
        }

        private ObservableCollection<GridLineItem> _gridLines;
        public ObservableCollection<GridLineItem> GridLines
        {
            get
            {
                if (_gridLines == null)
                {
                    _gridLines = new ObservableCollection<GridLineItem>();
                }
                return _gridLines;
            }
        }

        public void UpdateGridLines()
        {
            _gridLines?.Clear();
            if (_gridLines == null) return;

            var totalWidth = TotalFrames * PixelsPerFrame;
            var pixelStep = 50.0; // 50ピクセル毎にグリッド線
            var frameStep = pixelStep / PixelsPerFrame;

            var currentX = 0.0;
            var currentFrame = 0;

            while (currentX < totalWidth)
            {
                _gridLines.Add(new GridLineItem
                {
                    X = currentX,
                    Color = currentFrame % 30 == 0 ? "#555555" : "#444444"
                });
                currentX += pixelStep;
                currentFrame += (int)frameStep;
            }
        }

        private int? _totalFrames;

        public int TotalFrames
        {
            get
            {
                // If no blocks, show at least 15 seconds (30fps = 450 frames)
                var minFrames = 30 * 15; // 15 seconds at 30fps

                if (_totalFrames.HasValue && _totalFrames.Value > minFrames)
                {
                    return _totalFrames.Value;
                }

                // Calculate based on last block + 5 seconds
                var lastEndFrame = minFrames;
                foreach (var track in Tracks)
                {
                    foreach (var block in track.Items)
                    {
                        var blockEnd = block.StartFrame + block.Duration;
                        if (blockEnd > lastEndFrame)
                        {
                            lastEndFrame = blockEnd;
                        }
                    }
                }

                // Add 5 seconds (150 frames at 30fps)
                return lastEndFrame + (5 * 30);
            }
        }

        public void RefreshGridLines()
        {
            UpdateGridLines();
        }

        public void SetTotalFrames(int frames)
        {
            _totalFrames = frames;
            OnPropertyChanged(nameof(TotalFrames));
            OnPropertyChanged(nameof(DurationText));
        }

        // Position text properties for seek bar
        public string CurrentPositionText
        {
            get
            {
                var seconds = _currentFrame / 30.0;
                var minutes = (int)(seconds / 60);
                var secs = (int)(seconds % 60);
                return $"{minutes:D2}:{secs:D2}";
            }
        }

        public string DurationText
        {
            get
            {
                var totalFrames = TotalFrames;
                var seconds = totalFrames / 30.0;
                var minutes = (int)(seconds / 60);
                var secs = (int)(seconds % 60);
                return $"{minutes:D2}:{secs:D2}";
            }
        }

        // Zoom methods
        public void ZoomIn()
        {
            PixelsPerFrame = Math.Min(MaxPixelsPerFrame, PixelsPerFrame * 1.2);
        }

        public void ZoomOut()
        {
            PixelsPerFrame = Math.Max(MinPixelsPerFrame, PixelsPerFrame / 1.2);
        }

        // Track removal
        public void SetTrackForRemoval(TimelineTrackViewModel? track)
        {
            CurrentTrackForRemoval = track;
            (RemoveTrackCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        public void RemoveTrackInternal(TimelineTrackViewModel? track)
        {
            if (track == null || _tracks.Count <= 1) return;

            var modelTrack = _project.Tracks.FirstOrDefault(t => t.Name == track.Name);
            if (modelTrack != null)
            {
                _project.Tracks.Remove(modelTrack);
                _tracks.Remove(track);
                RecordUndo("RemoveTrack", new { Track = track, ModelTrack = modelTrack });
            }
        }

        // Block splitting
        public void SplitSelectedBlocks()
        {
            if (SelectedBlocks.Count == 0) return;

            var blocksToRemove = new List<(TimelineTrackViewModel Track, TimelineBlock Block)>();
            var blocksToAdd = new List<(TimelineTrackViewModel Track, TimelineBlock Left, TimelineBlock Right)>();

            foreach (var block in SelectedBlocks.ToList())
            {
                var track = _tracks.FirstOrDefault(t => t.Items.Contains(block));
                if (track == null) continue;

                // Split at the current frame position (playhead)
                var splitFrame = CurrentFrame - block.StartFrame;

                // Validate split position
                if (splitFrame <= 0 || splitFrame >= block.Duration) continue;

                var absoluteFrame = block.StartFrame + splitFrame;

                // Create left block
                var leftBlock = new TimelineBlock
                {
                    CharacterId = block.CharacterId,
                    StartFrame = block.StartFrame,
                    Duration = splitFrame,
                    Text = block.Text,
                    BackgroundColor = block.BackgroundColor,
                    Type = block.Type,
                    FontFamily = block.FontFamily,
                    FontSize = block.FontSize,
                    TextColor = block.TextColor,
                    AudioPath = block.AudioPath,
                    VideoPath = block.VideoPath,
                    TrackId = block.TrackId,
                    Name = block.Name + "_L",
                    Volume = block.Volume,
                    PlaybackSpeed = block.PlaybackSpeed,
                    Opacity = block.Opacity,
                    Loop = block.Loop,
                    TextPositionX = block.TextPositionX,
                    TextPositionY = block.TextPositionY,
                    FadeInFrames = block.FadeInFrames,
                    FadeOutFrames = block.FadeOutFrames,
                    AudioFadeInFrames = block.AudioFadeInFrames,
                    AudioFadeOutFrames = block.AudioFadeOutFrames
                };

                // Create right block
                var rightBlock = new TimelineBlock
                {
                    CharacterId = block.CharacterId,
                    StartFrame = absoluteFrame,
                    Duration = block.Duration - splitFrame,
                    Text = block.Text,
                    BackgroundColor = block.BackgroundColor,
                    Type = block.Type,
                    FontFamily = block.FontFamily,
                    FontSize = block.FontSize,
                    TextColor = block.TextColor,
                    AudioPath = block.AudioPath,
                    VideoPath = block.VideoPath,
                    TrackId = block.TrackId,
                    Name = block.Name + "_R",
                    Volume = block.Volume,
                    PlaybackSpeed = block.PlaybackSpeed,
                    Opacity = block.Opacity,
                    Loop = block.Loop,
                    TextPositionX = block.TextPositionX,
                    TextPositionY = block.TextPositionY,
                    FadeInFrames = block.FadeInFrames,
                    FadeOutFrames = block.FadeOutFrames,
                    AudioFadeInFrames = block.AudioFadeInFrames,
                    AudioFadeOutFrames = block.AudioFadeOutFrames
                };

                blocksToRemove.Add((track, block));
                blocksToAdd.Add((track, leftBlock, rightBlock));
            }

            // Apply changes
            foreach (var (track, block) in blocksToRemove)
            {
                track.Items.Remove(block);
            }

            foreach (var (track, left, right) in blocksToAdd)
            {
                track.Items.Add(left);
                track.Items.Add(right);
            }

            // Record undo
            if (blocksToRemove.Count > 0)
            {
                RecordUndo("SplitBlocks", new { Removed = blocksToRemove, Added = blocksToAdd });
            }

            // Clear selection
            SelectedBlocks.Clear();
        }

        // Multi-selection support
        public void SelectBlock(TimelineBlock block, bool additive)
        {
            if (!additive)
            {
                // Clear previous selection
                foreach (var b in SelectedBlocks.ToList())
                {
                    b.IsSelected = false;
                }
                SelectedBlocks.Clear();
            }

            if (!SelectedBlocks.Contains(block))
            {
                SelectedBlocks.Add(block);
                block.IsSelected = true;
                BlockSelected?.Invoke(this, block);
            }
        }

        public void ClearSelection()
        {
            foreach (var b in SelectedBlocks.ToList())
            {
                b.IsSelected = false;
            }
            SelectedBlocks.Clear();
        }

        private System.Threading.Timer? _playTimer;
        private HashSet<Guid> _playingAudioBlocks = new(); // 現在再生中の音声ブロック

        /// <summary>
        /// 再生/一時停止をトグル
        /// </summary>
        private void Play()
        {
            if (IsPlaying)
            {
                // 再生中なら一時停止
                Pause();
            }
            else
            {
                // 停止中なら再生
                StartPlayback();
            }
        }

        private void StartPlayback()
        {
            // 既に再生中の場合は何もしない
            if (IsPlaying && _playTimer != null)
            {
                return;
            }

            IsPlaying = true;
            _playingAudioBlocks.Clear(); // 再生開始時にクリア

            // 再生開始時に、現在のフレーム位置にある音声/動画ブロックを探して再生
            StartAudioBlocksAtCurrentFrame();

            // 再生開始時にプレイヘッド位置変更イベントを発火（スクロール用）
            PlayheadPositionChanged?.Invoke(this, CurrentFrame);

            _playTimer = new System.Threading.Timer(_ =>
            {
                var app = System.Windows.Application.Current;
                if (app == null) return;

                app.Dispatcher.Invoke(() =>
                {
                    if (CurrentFrame < TotalFrames)
                    {
                        // 現在のフレーム位置的な音声ブロックをチェック
                        CheckAndPlayAudioBlocksAtCurrentFrame();

                        CurrentFrame++;
                    }
                    else
                    {
                        Stop();
                    }
                });
            }, null, 0, 33); // Fixed at 33ms (30fps) for consistent playback speed
        }

        /// <summary>
        /// 現在のフレーム位置にある音声/動画ブロックを再生
        /// </summary>
        private void CheckAndPlayAudioBlocksAtCurrentFrame()
        {
            // Ensure at least one track exists when playing
            if (_tracks.Count == 0)
            {
                _project.Tracks.Add(new Models.TimelineTrack { Name = "トラック1", BlockColor = GetRandomColor() });
                _tracks.Add(new TimelineTrackViewModel(_project.Tracks[0]));
            }

            foreach (var track in _tracks)
            {
                foreach (var block in track.Items)
                {
                    // 音声ブロックで、現在のフレームが開始位置かチェック
                    if (block.Type == BlockType.Audio &&
                        block.StartFrame == CurrentFrame &&
                        !string.IsNullOrEmpty(block.AudioPath) &&
                        !_playingAudioBlocks.Contains(block.Id))
                    {
                        // 音声ファイルを最初から再生
                        _playingAudioBlocks.Add(block.Id);
                        _ = Services.AudioService.PlayAudioAsync(block.AudioPath, 0, block.PlaybackSpeed);

                        System.Diagnostics.Debug.WriteLine($"[Audio] Playing audio block at frame {CurrentFrame}: {block.AudioPath}");
                    }

                    // 音声ブロックの再生終了をクリーニング
                    if (block.Type == BlockType.Audio &&
                        _playingAudioBlocks.Contains(block.Id) &&
                        CurrentFrame >= block.StartFrame + block.Duration)
                    {
                        _playingAudioBlocks.Remove(block.Id);
                    }

                    // 動画ブロックの開始/終了検出
                    if (block.Type == BlockType.Video && !string.IsNullOrEmpty(block.AudioPath))
                    {
                        // 動画ブロックの開始位置に来たら通知
                        if (block.StartFrame == CurrentFrame && !_playingAudioBlocks.Contains(block.Id))
                        {
                            _playingAudioBlocks.Add(block.Id);
                            System.Diagnostics.Debug.WriteLine($"[Video] Video block started at frame {CurrentFrame}: {block.AudioPath}");

                            // MainWindow経由でプレビューの動画プレイヤーに通知
                            VideoBlockStarted?.Invoke(this, new VideoBlockEventArgs
                            {
                                VideoPath = block.AudioPath,
                                StartFrame = block.StartFrame,
                                Duration = block.Duration
                            });
                        }

                        // 動画ブロックの再生終了をクリーニング
                        if (_playingAudioBlocks.Contains(block.Id) && CurrentFrame >= block.StartFrame + block.Duration)
                        {
                            _playingAudioBlocks.Remove(block.Id);
                        }
                    }
                }
            }
        }

        public void Pause()
        {
            IsPlaying = false;
            _playTimer?.Dispose();
            _playTimer = null;
            _playingAudioBlocks.Clear(); // 一時停止時に再生中リストをクリア
        }

        private void Stop()
        {
            IsPlaying = false;
            _playTimer?.Dispose();
            _playTimer = null;
            CurrentFrame = 0;

            // Stop all audio playback
            Services.AudioService.StopAll();
            _playingAudioBlocks.Clear(); // 停止時に再生中リストをクリア
        }

        private void GoToStart()
        {
            CurrentFrame = 0;
        }

        public void AddTrackInternal()
        {
            var track = new TimelineTrack
            {
                Name = $"トラック{_tracks.Count + 1}",
                BlockColor = GetRandomColor()
            };

            _project.Tracks.Add(track);
            Tracks.Add(new TimelineTrackViewModel(track));
        }

        private void AddTrack()
        {
            var track = new TimelineTrack
            {
                Name = $"トラック{_tracks.Count + 1}",
                BlockColor = GetRandomColor()
            };

            _project.Tracks.Add(track);
            Tracks.Add(new TimelineTrackViewModel(track));
        }

        private string GetRandomColor()
        {
            var colors = new[] { "#3b82f6", "#ef4444", "#10b981", "#f59e0b", "#8b5cf6", "#ec4899" };
            var random = new Random();
            return colors[random.Next(colors.Length)];
        }

        public void Undo()
        {
            if (_undoStack.Count == 0) return;

            var action = _undoStack.Last();
            _undoStack.RemoveAt(_undoStack.Count - 1);

            switch (action.Action)
            {
                case "AddBlock":
                    if (action.Data is TimelineBlock block)
                    {
                        var track = Tracks.FirstOrDefault(t => t.Items.Contains(block));
                        track?.Items.Remove(block);
                    }
                    break;
                case "MoveBlock":
                    if (action.Data is BlockMoveData moveData)
                    {
                        moveData.Block.StartFrame = moveData.OldStartFrame;
                        moveData.Block.Duration = moveData.OldDuration;
                    }
                    break;
                case "DeleteBlock":
                    if (action.Data is DeleteBlockData deleteData)
                    {
                        deleteData.Track.Items.Insert(deleteData.Index, deleteData.Block);
                    }
                    break;
            }

            _redoStack.Add(action);
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        public void Redo()
        {
            if (_redoStack.Count == 0) return;

            var action = _redoStack.Last();
            _redoStack.RemoveAt(_redoStack.Count - 1);

            switch (action.Action)
            {
                case "AddBlock":
                    if (action.Data is TimelineBlock block)
                    {
                        var track = Tracks.FirstOrDefault(t => t.Items.Contains(block));
                        if (track == null && Tracks.Count > 0)
                        {
                            Tracks[0].Items.Add(block);
                        }
                    }
                    break;
                case "MoveBlock":
                    if (action.Data is BlockMoveData moveData)
                    {
                        moveData.Block.StartFrame = moveData.NewStartFrame;
                        moveData.Block.Duration = moveData.NewDuration;
                    }
                    break;
                case "DeleteBlock":
                    if (action.Data is DeleteBlockData deleteData)
                    {
                        deleteData.Track.Items.Remove(deleteData.Block);
                    }
                    break;
            }

            _undoStack.Add(action);
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        public void RecordUndo(string action, object data)
        {
            _undoStack.Add(new TimelineAction { Action = action, Data = data });
            _redoStack.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }
    }

    public class BlockMoveData
    {
        public TimelineBlock Block { get; set; } = null!;
        public int OldStartFrame { get; set; }
        public int NewStartFrame { get; set; }
        public int OldDuration { get; set; }
        public int NewDuration { get; set; }
    }

    public class DeleteBlockData
    {
        public TimelineTrackViewModel Track { get; set; } = null!;
        public TimelineBlock Block { get; set; } = null!;
        public int Index { get; set; }
    }

    public class TimelineAction
    {
        public string Action { get; set; } = string.Empty;
        public object? Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class GridLineItem : ViewModelBase
    {
        private double _x;
        private string _color;

        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        public string Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }
    }

    /// <summary>
    /// 動画ブロック開始イベント
    /// </summary>
    public class VideoBlockEventArgs : EventArgs
    {
        public string VideoPath { get; set; } = string.Empty;
        public int StartFrame { get; set; }
        public int Duration { get; set; }
    }

    public class TimelineTrackViewModel : ViewModelBase
    {
        private readonly TimelineTrack _track;
        private string _name;
        private bool _isVisible;
        private bool _isLocked;
        private bool _isEnabled = true;
        private double _volume = 100;
        private bool _isMuted;
        private bool _isEditingName;
        private string _editingName = string.Empty;

        public TimelineTrackViewModel(TimelineTrack track)
        {
            _track = track;
            _name = track.Name;
            _isVisible = track.IsVisible;
            _isLocked = track.IsLocked;
            _isEnabled = track.IsEnabled;
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public bool IsEditingName
        {
            get => _isEditingName;
            set => SetProperty(ref _isEditingName, value);
        }

        public string EditingName
        {
            get => _editingName;
            set => SetProperty(ref _editingName, value);
        }

        public void StartRename()
        {
            _editingName = _name;
            IsEditingName = true;
        }

        public void EndRename()
        {
            if (!string.IsNullOrWhiteSpace(_editingName))
            {
                Name = _editingName;
            }
            IsEditingName = false;
        }

        public string? BlockColor => _track.BlockColor;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (SetProperty(ref _isVisible, value))
                {
                    _track.IsVisible = value;
                }
            }
        }

        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                if (SetProperty(ref _isLocked, value))
                {
                    _track.IsLocked = value;
                }
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (SetProperty(ref _isEnabled, value))
                {
                    // When disabled, lock the track
                    _track.IsEnabled = value;
                    _isLocked = !value;
                    OnPropertyChanged(nameof(IsLocked));
                }
            }
        }

        public ObservableCollection<TimelineBlock> Items => _track.Items;

        public double Volume
        {
            get => _volume;
            set => SetProperty(ref _volume, value);
        }

        public bool IsMuted
        {
            get => _isMuted;
            set => SetProperty(ref _isMuted, value);
        }
    }
}
