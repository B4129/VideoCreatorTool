using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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
        private int _selectionStartFrame = -1;
        private int _selectionEndFrame = -1;
        private bool _isSelectingRange = false;

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
                    if (block.Type == BlockType.Audio &&
                        CurrentFrame >= block.StartFrame &&
                        CurrentFrame < block.StartFrame + block.Duration &&
                        !string.IsNullOrEmpty(block.AudioPath) &&
                        !_playingAudioBlocks.Contains(block.Id))
                    {
                        var offsetSeconds = (CurrentFrame - block.StartFrame) / Core.TimelineConstants.FramesPerSecond;
                        _playingAudioBlocks.Add(block.Id);
                        _ = PlayAudioBlockAsync(block);

                        System.Diagnostics.Debug.WriteLine($"[Audio] Starting audio block at frame {CurrentFrame}: {block.AudioPath} (offset: {offsetSeconds:F2}s)");
                    }

                    if (block.Type == BlockType.Video &&
                        CurrentFrame >= block.StartFrame &&
                        CurrentFrame < block.StartFrame + block.Duration &&
                        !string.IsNullOrEmpty(block.AudioPath) &&
                        !_playingAudioBlocks.Contains(block.Id))
                    {
                        _playingAudioBlocks.Add(block.Id);
                        System.Diagnostics.Debug.WriteLine($"[Video] Starting video block at frame {CurrentFrame}: {block.AudioPath} (block start: {block.StartFrame})");

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

        /// <summary>
        /// 範囲選択の開始フレーム
        /// </summary>
        public int SelectionStartFrame
        {
            get => _selectionStartFrame;
            set => SetProperty(ref _selectionStartFrame, value);
        }

        /// <summary>
        /// 範囲選択の終了フレーム
        /// </summary>
        public int SelectionEndFrame
        {
            get => _selectionEndFrame;
            set => SetProperty(ref _selectionEndFrame, value);
        }

        /// <summary>
        /// 範囲選択中かどうか
        /// </summary>
        public bool IsSelectingRange
        {
            get => _isSelectingRange;
            set => SetProperty(ref _isSelectingRange, value);
        }

        public double MinPixelsPerFrameConst => Core.TimelineConstants.MinPixelsPerFrame;
        public double MaxPixelsPerFrameConst => Core.TimelineConstants.MaxPixelsPerFrame;

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
            var pixelStep = Core.TimelineConstants.GridLinePixelInterval;
            var frameStep = pixelStep / PixelsPerFrame;

            var currentX = 0.0;
            var currentFrame = 0;

            while (currentX < totalWidth)
            {
                _gridLines.Add(new GridLineItem
                {
                    X = currentX,
                    Color = currentFrame % Core.TimelineConstants.FramesPerSecond == 0 ? "#555555" : "#444444"
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

                // Add 5 seconds buffer
                return lastEndFrame + (int)(5 * Core.TimelineConstants.FramesPerSecond);
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
                var seconds = _currentFrame / Core.TimelineConstants.FramesPerSecond;
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
                var seconds = totalFrames / Core.TimelineConstants.FramesPerSecond;
                var minutes = (int)(seconds / 60);
                var secs = (int)(seconds % 60);
                return $"{minutes:D2}:{secs:D2}";
            }
        }

        // Zoom methods
        public void ZoomIn()
        {
            PixelsPerFrame = Math.Min(Core.TimelineConstants.MaxPixelsPerFrame, PixelsPerFrame * 1.2);
        }

        public void ZoomOut()
        {
            PixelsPerFrame = Math.Max(Core.TimelineConstants.MinPixelsPerFrame, PixelsPerFrame / 1.2);
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

            var index = _tracks.IndexOf(track);
            var modelTrack = _project.Tracks.FirstOrDefault(t => t.Name == track.Name);
            if (modelTrack != null)
            {
                _project.Tracks.Remove(modelTrack);
                _tracks.Remove(track);
                RecordTrackRemove(track, modelTrack, index);
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

        /// <summary>
        /// ブロック追加を記録
        /// </summary>
        public void RecordBlockAdd(TimelineTrackViewModel track, TimelineBlock block)
        {
            RecordUndo("AddBlock", block);
        }

        /// <summary>
        /// ブロック削除を記録
        /// </summary>
        public void RecordBlockDelete(TimelineTrackViewModel track, TimelineBlock block)
        {
            var index = track.Items.IndexOf(block);
            var data = new DeleteBlockData
            {
                Track = track,
                Block = block,
                Index = index
            };
            RecordUndo("DeleteBlock", data);
        }

        /// <summary>
        /// テキスト編集を記録
        /// </summary>
        public void RecordTextEdit(TimelineBlock block, string oldText, string newText)
        {
            var data = new TextEditData
            {
                Block = block,
                OldText = oldText,
                NewText = newText
            };
            RecordUndo("TextEdit", data);
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
            SelectionStartFrame = -1;
            SelectionEndFrame = -1;
            IsSelectingRange = false;
        }

        /// <summary>
        /// 範囲選択を開始
        /// </summary>
        public void StartRangeSelection(int frame)
        {
            SelectionStartFrame = frame;
            SelectionEndFrame = frame;
            IsSelectingRange = true;
        }

        /// <summary>
        /// 範囲選択を更新
        /// </summary>
        public void UpdateRangeSelection(int frame)
        {
            if (IsSelectingRange)
            {
                SelectionEndFrame = frame;
                SelectBlocksInRange(SelectionStartFrame, SelectionEndFrame);
            }
        }

        /// <summary>
        /// 範囲選択を完了
        /// </summary>
        public void EndRangeSelection()
        {
            IsSelectingRange = false;
        }

        /// <summary>
        /// 指定範囲内のブロックを選択
        /// </summary>
        public void SelectBlocksInRange(int startFrame, int endFrame)
        {
            var minFrame = Math.Min(startFrame, endFrame);
            var maxFrame = Math.Max(startFrame, endFrame);

            // 前の選択をクリア
            foreach (var b in SelectedBlocks.ToList())
            {
                b.IsSelected = false;
            }
            SelectedBlocks.Clear();

            // 範囲内のブロックを選択
            foreach (var track in _tracks)
            {
                foreach (var block in track.Items)
                {
                    var blockEnd = block.StartFrame + block.Duration;
                    // ブロックが範囲内にあるかチェック
                    if (block.StartFrame < maxFrame && blockEnd > minFrame)
                    {
                        SelectedBlocks.Add(block);
                        block.IsSelected = true;
                    }
                }
            }
        }

        /// <summary>
        /// 選択したブロックを一括削除
        /// </summary>
        public void DeleteSelectedBlocks()
        {
            if (SelectedBlocks.Count == 0) return;

            var blocksToDelete = SelectedBlocks.ToList();
            foreach (var block in blocksToDelete)
            {
                var track = _tracks.FirstOrDefault(t => t.Items.Contains(block));
                if (track != null)
                {
                    track.Items.Remove(block);
                }
            }

            SelectedBlocks.Clear();
            SelectionStartFrame = -1;
            SelectionEndFrame = -1;
            IsSelectingRange = false;
        }

        /// <summary>
        /// 選択したブロックを一括移動
        /// </summary>
        public void MoveSelectedBlocks(int framesToMove)
        {
            if (SelectedBlocks.Count == 0) return;

            foreach (var block in SelectedBlocks.ToList())
            {
                var newFrame = Math.Max(0, block.StartFrame + framesToMove);
                block.StartFrame = newFrame;
            }
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
                Pause();
            }
            else
            {
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
            _playingAudioBlocks.Clear();

            // 再生開始時に、現在のフレーム位置にある音声/動画ブロックを探して再生
            StartAudioBlocksAtCurrentFrame();

            PlayheadPositionChanged?.Invoke(this, CurrentFrame);

            // タイマー間隔を33ms（30fps）に修正
            _playTimer = new System.Threading.Timer(_ =>
            {
                var app = System.Windows.Application.Current;
                if (app == null) return;

                app.Dispatcher.BeginInvoke(() =>
                {
                    if (CurrentFrame < TotalFrames)
                    {
                        CheckAndPlayAudioBlocksAtCurrentFrame();
                        CurrentFrame++;
                    }
                    else
                    {
                        Stop();
                    }
                });
            }, null, 0, Core.TimelineConstants.PlaybackTimerIntervalMs); // 33ms = 30fps
        }

        /// <summary>
        /// 現在のフレーム位置にある音声/動画ブロックを再生（再生中は開始位置のみチェック）
        /// </summary>
        private void CheckAndPlayAudioBlocksAtCurrentFrame()
        {
            System.Diagnostics.Debug.WriteLine($"[Audio] CheckAndPlayAudioBlocksAtCurrentFrame: CurrentFrame={CurrentFrame}");

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
                    // 音声ブロックの開始位置チェック（厳密一致）
                    // 既に再生中でない場合のみ
                    if (block.Type == BlockType.Audio &&
                        block.StartFrame == CurrentFrame &&
                        !string.IsNullOrEmpty(block.AudioPath) &&
                        !_playingAudioBlocks.Contains(block.Id))
                    {
                        _playingAudioBlocks.Add(block.Id);
                        System.Diagnostics.Debug.WriteLine($"[Audio] STARTING audio block: Id={block.Id}, StartFrame={block.StartFrame}, Path={block.AudioPath}");
                        _ = PlayAudioBlockAsync(block);
                    }

                    // 音声ブロックの再生終了クリーニング
                    if (block.Type == BlockType.Audio &&
                        _playingAudioBlocks.Contains(block.Id) &&
                        CurrentFrame >= block.StartFrame + block.Duration)
                    {
                        _playingAudioBlocks.Remove(block.Id);
                        System.Diagnostics.Debug.WriteLine($"[Audio] Finished audio block at frame {CurrentFrame}: {block.AudioPath}");
                    }

                    // 動画ブロックの開始/終了検出
                    if (block.Type == BlockType.Video && !string.IsNullOrEmpty(block.AudioPath))
                    {
                        if (block.StartFrame == CurrentFrame && !_playingAudioBlocks.Contains(block.Id))
                        {
                            _playingAudioBlocks.Add(block.Id);
                            System.Diagnostics.Debug.WriteLine($"[Video] Video block started at frame {CurrentFrame}: {block.AudioPath}");

                            VideoBlockStarted?.Invoke(this, new VideoBlockEventArgs
                            {
                                VideoPath = block.AudioPath,
                                StartFrame = block.StartFrame,
                                Duration = block.Duration
                            });
                        }

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

            // Stop all audio playback on pause
            Services.AudioService.StopAll();
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
            var trackVm = new TimelineTrackViewModel(track);
            Tracks.Add(trackVm);
            RecordTrackAdd(trackVm, track);
        }

        /// <summary>
        /// 音声ブロックを再生し、完了時に_playingAudioBlocksから削除する
        /// </summary>
        private async Task PlayAudioBlockAsync(Models.TimelineBlock block)
        {
            System.Diagnostics.Debug.WriteLine($"[Audio] PlayAudioBlockAsync START: Id={block.Id}, Path={block.AudioPath}");
            try
            {
                // Get track for this block
                var track = _tracks.FirstOrDefault(t => t.Items.Contains(block));
                var trackVm = track != null ? Tracks.FirstOrDefault(t => t == track) : null;

                // Get volume and mute status from track
                var volume = trackVm?.Volume ?? 1.0;
                var isMuted = trackVm?.IsMuted ?? false;

                await Services.AudioService.PlayAudioAsync(block.AudioPath, block.Id.ToString(), 0, block.PlaybackSpeed, volume, isMuted);
                System.Diagnostics.Debug.WriteLine($"[Audio] PlayAudioBlockAsync COMPLETED: Id={block.Id}, Volume={volume}, Muted={isMuted}");
            }
            finally
            {
                // 再生終了後にリストから削除
                _playingAudioBlocks.Remove(block.Id);
                System.Diagnostics.Debug.WriteLine($"[Audio] Audio block REMOVED from playing list: Id={block.Id}");
            }
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
            return Utilities.ColorHelper.GetRandomColor();
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
                case "PropertyChange":
                    if (action.Data is PropertyChangeData propData)
                    {
                        var prop = propData.Target.GetType().GetProperty(propData.PropertyName);
                        prop?.SetValue(propData.Target, propData.OldValue);
                    }
                    break;
                case "TextEdit":
                    if (action.Data is TextEditData textData)
                    {
                        textData.Block.Text = textData.OldText;
                    }
                    break;
                case "AddTrack":
                    if (action.Data is TrackOperationData trackAddData)
                    {
                        if (trackAddData.Track != null)
                        {
                            _tracks.Remove(trackAddData.Track);
                            if (trackAddData.ModelTrack != null)
                            {
                                _project.Tracks.Remove(trackAddData.ModelTrack);
                            }
                        }
                    }
                    break;
                case "RemoveTrack":
                    if (action.Data is TrackOperationData trackRemoveData)
                    {
                        if (trackRemoveData.Track != null && trackRemoveData.ModelTrack != null)
                        {
                            var index = trackRemoveData.Index;
                            if (index >= 0 && index <= _tracks.Count)
                            {
                                _tracks.Insert(index, trackRemoveData.Track);
                                _project.Tracks.Insert(index, trackRemoveData.ModelTrack);
                            }
                        }
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
                case "PropertyChange":
                    if (action.Data is PropertyChangeData propData)
                    {
                        var prop = propData.Target.GetType().GetProperty(propData.PropertyName);
                        prop?.SetValue(propData.Target, propData.NewValue);
                    }
                    break;
                case "TextEdit":
                    if (action.Data is TextEditData textData)
                    {
                        textData.Block.Text = textData.NewText;
                    }
                    break;
                case "AddTrack":
                    if (action.Data is TrackOperationData trackAddData)
                    {
                        if (trackAddData.Track != null && trackAddData.ModelTrack != null)
                        {
                            var index = trackAddData.Index;
                            if (index >= 0 && index <= _tracks.Count)
                            {
                                _tracks.Insert(index, trackAddData.Track);
                                _project.Tracks.Insert(index, trackAddData.ModelTrack);
                            }
                        }
                    }
                    break;
                case "RemoveTrack":
                    if (action.Data is TrackOperationData trackRemoveData)
                    {
                        if (trackRemoveData.Track != null)
                        {
                            _tracks.Remove(trackRemoveData.Track);
                            if (trackRemoveData.ModelTrack != null)
                            {
                                _project.Tracks.Remove(trackRemoveData.ModelTrack);
                            }
                        }
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

        /// <summary>
        /// プロパティ変更を記録
        /// </summary>
        public void RecordPropertyChange(object target, string propertyName, object? oldValue, object? newValue)
        {
            var data = new PropertyChangeData
            {
                Target = target,
                PropertyName = propertyName,
                OldValue = oldValue,
                NewValue = newValue
            };
            RecordUndo("PropertyChange", data);
        }

        /// <summary>
        /// トラック追加を記録
        /// </summary>
        public void RecordTrackAdd(TimelineTrackViewModel trackVm, Models.TimelineTrack modelTrack)
        {
            var data = new TrackOperationData
            {
                Track = trackVm,
                ModelTrack = modelTrack,
                Index = _tracks.Count - 1
            };
            RecordUndo("AddTrack", data);
        }

        /// <summary>
        /// トラック削除を記録
        /// </summary>
        public void RecordTrackRemove(TimelineTrackViewModel trackVm, Models.TimelineTrack modelTrack, int index)
        {
            var data = new TrackOperationData
            {
                Track = trackVm,
                ModelTrack = modelTrack,
                Index = index
            };
            RecordUndo("RemoveTrack", data);
        }
    }
}
