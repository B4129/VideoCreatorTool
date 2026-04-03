using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.Views
{
    public partial class TimelineView : UserControl
    {
        private TimelineBlock? _selectedBlock;
        private bool _isDragging;
        private int _dragStartX;
        private int _dragStartY;
        private int _dragStartFrame;
        private bool _isResizing;
        private ResizeEdge _resizeEdge;
        private int _resizeStartDuration;
        private bool _isDraggingPlayhead;
        private bool _isUserScrolling;
        private System.Windows.Threading.DispatcherTimer? _scrollTimer;
        private ViewModels.TimelineTrackViewModel? _dragSourceTrack;
        private ViewModels.TimelineTrackViewModel? _dragTargetTrack;

        public TimelineView()
        {
            InitializeComponent();
            UpdatePlayheadPosition();

            // DataContext の変更を監視してイベント購読
            DataContextChanged += TimelineView_DataContextChanged;
        }

        private void TimelineView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ViewModels.TimelineViewModel oldVm)
            {
                oldVm.PlayheadPositionChanged -= OnPlayheadPositionChanged;
            }

            if (e.NewValue is ViewModels.TimelineViewModel newVm)
            {
                newVm.PlayheadPositionChanged += OnPlayheadPositionChanged;
            }
        }

        private void OnPlayheadPositionChanged(object? sender, int frame)
        {
            // 再生中は常にプレイヘッド位置に自動スクロール
            if (sender is ViewModels.TimelineViewModel vm && vm.IsPlaying)
            {
                // ユーザースクロール中でも再生中は強制的にスクロール
                ScrollToFrame(frame);
            }
        }

        // Sync vertical scrolling between headers and timeline
        private void TimelineScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
            {
                HeadersScrollViewer.ScrollToVerticalOffset(TimelineScrollViewer.VerticalOffset);
            }

            // Detect manual horizontal scrolling
            if (e.HorizontalChange != 0)
            {
                _isUserScrolling = true;
                _scrollTimer?.Stop();
                _scrollTimer = new System.Windows.Threading.DispatcherTimer();
                _scrollTimer.Interval = TimeSpan.FromMilliseconds(500);
                _scrollTimer.Tick += (s, args) =>
                {
                    _isUserScrolling = false;
                    _scrollTimer.Stop();
                    _scrollTimer = null;
                };
                _scrollTimer.Start();
            }
        }

        private void Block_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // プレイヘッドのドラッグ中はブロックのドラッグを開始しない
            if (_isDraggingPlayhead) return;

            if (sender is not Border border || border.DataContext is not TimelineBlock block) return;

            var parentGrid = border.Parent as Grid;
            var trackVm = parentGrid?.DataContext as ViewModels.TimelineTrackViewModel;
            if (trackVm?.IsLocked == true) return;

            e.Handled = true;

            // Handle multi-selection with Ctrl/Shift
            bool additive = Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == ModifierKeys.Shift;

            if (DataContext is ViewModels.TimelineViewModel timelineVm)
            {
                timelineVm.SelectBlock(block, additive);
            }

            _selectedBlock = block;
            var mousePos = e.GetPosition(this);
            _dragStartX = (int)mousePos.X;
            _dragStartY = (int)mousePos.Y;
            _dragStartFrame = block.StartFrame;
            _dragSourceTrack = trackVm;
            _dragTargetTrack = trackVm;
            _isResizing = false;
            _resizeEdge = ResizeEdge.None;

            // 左クリックでドラッグ開始（リサイズハンドルでない場合のみ）
            if (e.LeftButton == MouseButtonState.Pressed && !_isResizing)
            {
                _isDragging = true;
                border.CaptureMouse();
            }
        }

        private void Block_MouseMove(object sender, MouseEventArgs e)
        {
            // Handle resize (text, audio, video blocks) - priority first
            if (_isResizing && _selectedBlock != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentX = (int)e.GetPosition(this).X;
                var deltaX = currentX - _dragStartX;
                System.Diagnostics.Debug.WriteLine($"[Resize MouseMove] Block Type={_selectedBlock.Type}, deltaX={deltaX}, Duration={_selectedBlock.Duration}");

                // ドラッグ中はプレイヘッドをマウス位置に追従
                var currentMousePos = e.GetPosition(this);
                UpdatePlayheadToMousePosition(currentMousePos);

                if (DataContext is not ViewModels.TimelineViewModel timelineVm) return;
                var framesDelta = (int)(deltaX / timelineVm.PixelsPerFrame);

                if (_resizeEdge == ResizeEdge.Right)
                {
                    var newDuration = Math.Max(1, _resizeStartDuration + framesDelta);

                    // Apply snap to end frame
                    if (timelineVm.IsSnapEnabled)
                    {
                        var endFrame = _selectedBlock.StartFrame + newDuration;
                        endFrame = ApplySnap(endFrame, _selectedBlock);
                        newDuration = endFrame - _selectedBlock.StartFrame;
                        newDuration = Math.Max(1, newDuration);
                    }

                    _selectedBlock.Duration = newDuration;

                    // リサイズ中にプレイヘッドをブロックの終端に追従
                    var resizeEndFrame = _selectedBlock.StartFrame + newDuration;
                    timelineVm.CurrentFrame = resizeEndFrame;
                    UpdatePlayheadPosition();
                }
                else if (_resizeEdge == ResizeEdge.Left)
                {
                    var newStart = _dragStartFrame + framesDelta;
                    var durationChange = -framesDelta;

                    if (newStart >= 0 && (_resizeStartDuration + durationChange) >= 1)
                    {
                        // Apply snap to start frame
                        if (timelineVm.IsSnapEnabled)
                        {
                            newStart = ApplySnap(newStart, _selectedBlock);
                            durationChange = newStart - _dragStartFrame;
                        }

                        if ((_resizeStartDuration + durationChange) >= 1)
                        {
                            _selectedBlock.StartFrame = newStart;
                            _selectedBlock.Duration = _resizeStartDuration + durationChange;

                            // リサイズ中にプレイヘッドをブロックの開始位置に追従
                            timelineVm.CurrentFrame = newStart;
                            UpdatePlayheadPosition();
                        }
                    }
                }
                return; // Exit early if resizing
            }

            // Handle drag move (not resizing) - only when dragging and not resizing
            if (_isDragging && _selectedBlock != null && !_isResizing && e.LeftButton == MouseButtonState.Pressed)
            {
                var mousePos = e.GetPosition(this);
                var currentX = (int)mousePos.X;
                var currentY = (int)mousePos.Y;
                var deltaX = currentX - _dragStartX;

                if (DataContext is not ViewModels.TimelineViewModel timelineVm) return;
                var framesDelta = (int)(deltaX / timelineVm.PixelsPerFrame);

                var newFrame = Math.Max(0, _dragStartFrame + framesDelta);

                // Determine target track based on Y position
                _dragTargetTrack = GetTrackFromYPosition(currentY, _dragSourceTrack);

                // Apply snap if enabled
                if (timelineVm.IsSnapEnabled)
                {
                    newFrame = ApplySnap(newFrame, _selectedBlock);
                }

                _selectedBlock.StartFrame = newFrame;

                // Update UI to show drag target (optional visual feedback)
                UpdateDragDropFeedback(currentY);
            }
        }

        private void Block_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
            {
                border.ReleaseMouseCapture();
            }

            // Move block to target track if different from source
            if (_isDragging && _selectedBlock != null && _dragSourceTrack != null && _dragTargetTrack != null)
            {
                if (_dragSourceTrack != _dragTargetTrack)
                {
                    // Remove from source track
                    _dragSourceTrack.Items.Remove(_selectedBlock);
                    // Add to target track
                    _selectedBlock.TrackId = _dragTargetTrack.Items.Count > 0 ?
                        _dragTargetTrack.Items.Last().TrackId : Guid.NewGuid();
                    _dragTargetTrack.Items.Add(_selectedBlock);
                }

                // Record undo for block move
                if (DataContext is ViewModels.TimelineViewModel timelineVmForUndo)
                {
                    // ドロップ後にブロックをフォーカス（選択状態に）
                    timelineVmForUndo.ClearSelection();
                    timelineVmForUndo.SelectBlock(_selectedBlock, false);

                    var moveData = new ViewModels.BlockMoveData
                    {
                        Block = _selectedBlock,
                        OldStartFrame = _dragStartFrame,
                        NewStartFrame = _selectedBlock.StartFrame,
                        OldDuration = _resizeStartDuration,
                        NewDuration = _selectedBlock.Duration
                    };
                    timelineVmForUndo.RecordUndo("MoveBlock", moveData);

                    // If audio/video block was resized, handle audio stretch
                    if (_resizeStartDuration != _selectedBlock.Duration &&
                        (_selectedBlock.Type == BlockType.Audio || _selectedBlock.Type == BlockType.Video))
                    {
                        HandleBlockResized(_selectedBlock, _resizeStartDuration);
                    }
                }

                // Clear drag feedback
                ClearDragDropFeedback();
            }

            _isDragging = false;
            _isResizing = false;
            _resizeEdge = ResizeEdge.None;
            _selectedBlock = null;
            _dragSourceTrack = null;
            _dragTargetTrack = null;
        }

        private void Block_ResizeLeft_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.DataContext is not TimelineBlock block) return;

            var parentGrid = border.Parent as Grid;
            var trackVm = parentGrid?.DataContext as ViewModels.TimelineTrackViewModel;
            if (trackVm?.IsLocked == true) return;

            e.Handled = true;
            _selectedBlock = block;
            _isResizing = true;
            _resizeEdge = ResizeEdge.Left;
            _dragStartX = (int)e.GetPosition(this).X;
            _dragStartFrame = block.StartFrame;
            _resizeStartDuration = block.Duration;
            System.Diagnostics.Debug.WriteLine($"[Resize] Left resize started: Type={block.Type}, Duration={block.Duration}");
            border.CaptureMouse();
        }

        private void Block_ResizeRight_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.DataContext is not TimelineBlock block) return;

            var parentGrid = border.Parent as Grid;
            var trackVm = parentGrid?.DataContext as ViewModels.TimelineTrackViewModel;
            if (trackVm?.IsLocked == true) return;

            e.Handled = true;
            _selectedBlock = block;
            _isResizing = true;
            _resizeEdge = ResizeEdge.Right;
            _dragStartX = (int)e.GetPosition(this).X;
            _resizeStartDuration = block.Duration;
            System.Diagnostics.Debug.WriteLine($"[Resize] Right resize started: Type={block.Type}, Duration={block.Duration}");
            border.CaptureMouse();
        }

        private void Block_Resize_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isResizing && _selectedBlock != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentX = (int)e.GetPosition(this).X;
                var deltaX = currentX - _dragStartX;

                if (DataContext is not ViewModels.TimelineViewModel timelineVm) return;
                var framesDelta = (int)(deltaX / timelineVm.PixelsPerFrame);

                if (_resizeEdge == ResizeEdge.Right)
                {
                    _selectedBlock.Duration = Math.Max(1, _resizeStartDuration + framesDelta);
                }
                else if (_resizeEdge == ResizeEdge.Left)
                {
                    var newStart = _dragStartFrame + framesDelta;
                    var durationChange = -framesDelta;

                    if (newStart >= 0 && (_resizeStartDuration + durationChange) >= 1)
                    {
                        _selectedBlock.StartFrame = newStart;
                        _selectedBlock.Duration = _resizeStartDuration + durationChange;
                    }
                }
            }
        }

        private void Block_Resize_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
            {
                border.ReleaseMouseCapture();
            }
            _isDragging = false;
            _isResizing = false;
            _resizeEdge = ResizeEdge.None;
            _selectedBlock = null;
        }

        private void TimelineView_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.TimelineViewModel timelineVm) return;

            var selectedBlocks = GetSelectedBlocks();
            if (selectedBlocks.Count == 0) return;

            switch (e.Key)
            {
                case Key.Delete:
                    DeleteSelectedBlocks(selectedBlocks, timelineVm);
                    e.Handled = true;
                    break;
                case Key.C when Keyboard.Modifiers == ModifierKeys.Control:
                    CopySelectedBlocks(selectedBlocks);
                    e.Handled = true;
                    break;
                case Key.V when Keyboard.Modifiers == ModifierKeys.Control:
                    PasteBlocks(timelineVm);
                    e.Handled = true;
                    break;
                case Key.Z when Keyboard.Modifiers == ModifierKeys.Control:
                    timelineVm.Undo();
                    e.Handled = true;
                    break;
                case Key.Y when Keyboard.Modifiers == ModifierKeys.Control:
                    timelineVm.Redo();
                    e.Handled = true;
                    break;
                case Key.S:
                    timelineVm.SplitSelectedBlocks();
                    e.Handled = true;
                    break;
                case Key.OemPlus when Keyboard.Modifiers == ModifierKeys.Control:
                    timelineVm.ZoomIn();
                    e.Handled = true;
                    break;
                case Key.OemMinus when Keyboard.Modifiers == ModifierKeys.Control:
                    timelineVm.ZoomOut();
                    e.Handled = true;
                    break;
                case Key.P when Keyboard.Modifiers == ModifierKeys.Control:
                    OpenSubtitleProperties();
                    e.Handled = true;
                    break;
            }
        }

        private void OpenSubtitleProperties()
        {
            var selectedBlocks = GetSelectedBlocks();
            if (selectedBlocks.Count == 0) return;

            // Open properties dialog for the first selected block
            var block = selectedBlocks[0];
            var dialog = new SubtitlePropertiesDialog(block);
            var result = dialog.ShowDialog();

            if (result == true)
            {
                // Properties updated - WPF binding handles the rest
            }
        }

        private List<TimelineBlock> GetSelectedBlocks()
        {
            var selected = new List<TimelineBlock>();
            if (DataContext is ViewModels.TimelineViewModel timelineVm)
            {
                foreach (var track in timelineVm.Tracks)
                {
                    foreach (var block in track.Items.Where(b => b.IsSelected))
                    {
                        selected.Add(block);
                    }
                }
            }
            return selected;
        }

        private void DeselectAllExcept(TimelineBlock exceptBlock)
        {
            if (DataContext is not ViewModels.TimelineViewModel timelineVm) return;

            foreach (var track in timelineVm.Tracks)
            {
                foreach (var block in track.Items)
                {
                    if (block != exceptBlock && !timelineVm.SelectedBlocks.Contains(block))
                    {
                        block.IsSelected = false;
                    }
                }
            }
        }

        private int ApplySnap(int proposedFrame, TimelineBlock movingBlock)
        {
            if (DataContext is not ViewModels.TimelineViewModel timelineVm) return proposedFrame;

            const int snapThreshold = 5; // frames
            int closestFrame = proposedFrame;
            int minDistance = int.MaxValue;

            foreach (var track in timelineVm.Tracks)
            {
                foreach (var block in track.Items)
                {
                    // Skip the block being moved
                    if (block == movingBlock) continue;

                    // Check block start
                    var distToStart = Math.Abs(proposedFrame - block.StartFrame);
                    if (distToStart < snapThreshold && distToStart < minDistance)
                    {
                        minDistance = distToStart;
                        closestFrame = block.StartFrame;
                    }

                    // Check block end
                    var blockEnd = block.StartFrame + block.Duration;
                    var distToEnd = Math.Abs(proposedFrame - blockEnd);
                    if (distToEnd < snapThreshold && distToEnd < minDistance)
                    {
                        minDistance = distToEnd;
                        closestFrame = blockEnd;
                    }
                }
            }

            return closestFrame;
        }

        // Get track from Y position for drag-drop between tracks
        private ViewModels.TimelineTrackViewModel? GetTrackFromYPosition(int yPosition, ViewModels.TimelineTrackViewModel? sourceTrack)
        {
            if (DataContext is not ViewModels.TimelineViewModel timelineVm) return sourceTrack;

            // Calculate which track row the mouse is over
            // Track headers are 24px height, ruler is 32px
            int timelineStartY = 32 + 24; // Approximate - adjust based on actual layout
            int trackHeight = 24;

            if (yPosition < timelineStartY + trackHeight / 2)
            {
                // Above or in first track
                return timelineVm.Tracks.FirstOrDefault();
            }

            // Calculate track index
            int trackIndex = (yPosition - timelineStartY + trackHeight / 2) / trackHeight;
            if (trackIndex >= 0 && trackIndex < timelineVm.Tracks.Count)
            {
                return timelineVm.Tracks[trackIndex];
            }

            // Default to last track if beyond
            return timelineVm.Tracks.LastOrDefault();
        }

        // Visual feedback for drag-drop (optional - can add highlight overlay)
        private void UpdateDragDropFeedback(int currentY)
        {
            // TODO: Add visual feedback like track highlighting
            // This would require adding an overlay element to the XAML
        }

        private void ClearDragDropFeedback()
        {
            // TODO: Clear visual feedback
        }

        private void DeleteSelectedBlocks(List<TimelineBlock> blocks, ViewModels.TimelineViewModel timelineVm)
        {
            foreach (var block in blocks)
            {
                foreach (var track in timelineVm.Tracks)
                {
                    if (track.Items.Contains(block))
                    {
                        var index = track.Items.IndexOf(block);
                        var deleteData = new ViewModels.DeleteBlockData
                        {
                            Track = track,
                            Block = block,
                            Index = index
                        };
                        timelineVm.RecordUndo("DeleteBlock", deleteData);
                        track.Items.Remove(block);
                        // Also remove from SelectedBlocks
                        timelineVm.SelectedBlocks.Remove(block);
                        break;
                    }
                }
            }
        }

        private static readonly List<TimelineBlock> Clipboard = new();

        private void CopySelectedBlocks(List<TimelineBlock> blocks)
        {
            Clipboard.Clear();
            foreach (var block in blocks)
            {
                Clipboard.Add(new TimelineBlock
                {
                    CharacterId = block.CharacterId,
                    StartFrame = block.StartFrame,
                    Duration = block.Duration,
                    Text = block.Text,
                    BackgroundColor = block.BackgroundColor,
                    Type = block.Type
                });
            }
        }

        private void PasteBlocks(ViewModels.TimelineViewModel timelineVm)
        {
            if (Clipboard.Count == 0) return;

            foreach (var block in Clipboard)
            {
                var newBlock = new TimelineBlock
                {
                    CharacterId = block.CharacterId,
                    StartFrame = block.StartFrame + 10,
                    Duration = block.Duration,
                    Text = block.Text,
                    BackgroundColor = block.BackgroundColor,
                    Type = block.Type,
                    IsSelected = true
                };

                if (timelineVm.Tracks.Count > 0)
                {
                    timelineVm.Tracks[0].Items.Add(newBlock);
                }
            }
        }

        private enum ResizeEdge
        {
            Left,
            Right,
            None
        }

        /// <summary>
        /// ブロックリサイズ時の処理。音声ブロックの場合、 stretched audio を生成
        /// </summary>
        private async void HandleBlockResized(TimelineBlock block, int originalDuration)
        {
            if (block.Type == BlockType.Audio || block.Type == BlockType.Video)
            {
                if (!string.IsNullOrEmpty(block.AudioPath))
                {
                    var newDurationSeconds = block.Duration / 30.0;
                    var originalDurationSeconds = originalDuration / 30.0;
                    var stretchFactor = newDurationSeconds / originalDurationSeconds;

                    // 伸長率が1でない場合、音声を再配置
                    if (Math.Abs(stretchFactor - 1.0) > 0.01)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AudioStretch] Resizing audio: {block.AudioPath}, factor: {stretchFactor:F2}x");
                        // TODO: Implement actual audio stretching using ffmpeg
                        // For now, just update the block duration
                        // In production, you would use ffmpeg to create stretched audio:
                        // ffmpeg -i input.wav -filter:a "atempo=<stretchFactor>" output.wav
                    }
                }
            }
        }

        // Track name double-click to start rename
        private void TrackName_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is Grid grid && grid.DataContext is ViewModels.TimelineTrackViewModel trackVm)
            {
                // Show edit box directly
                ShowTrackNameEditBox(grid, trackVm);
            }
        }

        // Show inline edit box for track name
        private void ShowTrackNameEditBox(Grid trackNameGrid, ViewModels.TimelineTrackViewModel trackVm)
        {
            // Find existing controls
            var textBlock = trackNameGrid.FindName("TrackNameTextBlock") as TextBlock;
            var editBox = trackNameGrid.FindName("TrackNameEditBox") as TextBox;

            if (textBlock == null || editBox == null)
            {
                // Create edit box dynamically
                if (editBox == null)
                {
                    editBox = new TextBox
                    {
                        Text = trackVm.Name,
                        FontSize = 12,
                        Padding = new Thickness(2, 4, 2, 4),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    // Store reference
                    trackNameGrid.RegisterName("TrackNameEditBox", editBox);
                    trackNameGrid.Children.Add(editBox);
                }

                editBox.Visibility = Visibility.Visible;
                editBox.Text = trackVm.Name;
                editBox.Focus();
                editBox.SelectAll();

                // Handle lost focus and key down
                editBox.LostFocus += (s, e) => FinishTrackNameEdit(editBox, trackVm, trackNameGrid);
                editBox.KeyDown += (s, e) =>
                {
                    if (e.Key == System.Windows.Input.Key.Enter)
                    {
                        FinishTrackNameEdit(editBox, trackVm, trackNameGrid);
                    }
                    else if (e.Key == System.Windows.Input.Key.Escape)
                    {
                        CancelTrackNameEdit(editBox, trackNameGrid);
                    }
                };
            }
            else
            {
                editBox.Visibility = Visibility.Visible;
                editBox.Text = trackVm.Name;
                editBox.Focus();
                editBox.SelectAll();
            }

            if (textBlock != null)
                textBlock.Visibility = Visibility.Collapsed;
        }

        private void FinishTrackNameEdit(TextBox editBox, ViewModels.TimelineTrackViewModel trackVm, Grid trackNameGrid)
        {
            if (!string.IsNullOrWhiteSpace(editBox.Text))
            {
                trackVm.Name = editBox.Text;
            }
            CancelTrackNameEdit(editBox, trackNameGrid);
        }

        private void CancelTrackNameEdit(TextBox editBox, Grid trackNameGrid)
        {
            editBox.Visibility = Visibility.Collapsed;
            var textBlock = trackNameGrid.FindName("TrackNameTextBlock") as TextBlock;
            if (textBlock != null)
                textBlock.Visibility = Visibility.Visible;
        }

        // Rename button click
        private void RenameTrack_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement button && button.DataContext is ViewModels.TimelineTrackViewModel trackVm)
            {
                trackVm.StartRename();
            }
        }

        // Remove track button click
        private void RemoveTrack_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement button && button.DataContext is ViewModels.TimelineTrackViewModel trackVm)
            {
                if (DataContext is ViewModels.TimelineViewModel timelineVm)
                {
                    var result = MessageBox.Show($"トラック「{trackVm.Name}」を削除してもよろしいですか？",
                        "トラック削除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.Yes)
                    {
                        timelineVm.RemoveTrackInternal(trackVm);
                    }
                }
            }
        }

        private void TrackMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.TimelineViewModel timelineVm) return;

            var trackVm = (sender as FrameworkElement)?.DataContext as ViewModels.TimelineTrackViewModel;
            if (trackVm == null) return;

            var index = timelineVm.Tracks.IndexOf(trackVm);
            if (index > 0)
            {
                timelineVm.Tracks.Move(index, index - 1);
            }
        }

        private void TrackMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.TimelineViewModel timelineVm) return;

            var trackVm = (sender as FrameworkElement)?.DataContext as ViewModels.TimelineTrackViewModel;
            if (trackVm == null) return;

            var index = timelineVm.Tracks.IndexOf(trackVm);
            if (index < timelineVm.Tracks.Count - 1)
            {
                timelineVm.Tracks.Move(index, index + 1);
            }
        }

        private async void Block_PlayAudio_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not TimelineBlock block) return;

            // If audio already exists, play it
            if (!string.IsNullOrEmpty(block.AudioPath) && File.Exists(block.AudioPath))
            {
                _ = Services.AudioService.PlayAudioAsync(block.AudioPath);
                return;
            }

            // Otherwise generate audio from text
            if (!string.IsNullOrEmpty(block.Text))
            {
                if (DataContext is ViewModels.TimelineViewModel timelineVm)
                {
                    // Find character/speaker from track context
                    int speakerId = 2; // default
                    var parentGrid = (sender as FrameworkElement)?.TemplatedParent as Grid;
                    if (parentGrid?.DataContext is ViewModels.TimelineTrackViewModel trackVm)
                    {
                        // Get speaker from project characters
                        var track = timelineVm._project.Tracks.FirstOrDefault(t => t.Name == trackVm.Name);
                        if (track?.CharacterId != null)
                        {
                            var character = timelineVm._project.Characters.FirstOrDefault(c => c.Id == track.CharacterId);
                            if (character != null)
                            {
                                speakerId = character.SpeakerId;
                            }
                        }
                    }
                    _ = GenerateAndPlayAudio(block, speakerId);
                }
            }
        }

        private async System.Threading.Tasks.Task GenerateAndPlayAudio(TimelineBlock block, int speakerId)
        {
            var audioPath = await Services.VoiceVoxService.GenerateAudioFromText(block.Text, speakerId);
            if (audioPath != null)
            {
                block.AudioPath = audioPath;
                await Services.AudioService.PlayAudioAsync(audioPath);
            }
        }

        // Playhead event handlers
        private void PlayheadCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Pause playback when touching playhead
                if (Window.GetWindow(this)?.DataContext is ViewModels.MainWindowViewModel mainVm)
                {
                    mainVm.Pause();
                }

                _isDraggingPlayhead = true;
                UpdateCurrentFrameFromMouse(e);
                (sender as UIElement)?.CaptureMouse();
            }
        }

        private void PlayheadCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingPlayhead && e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateCurrentFrameFromMouse(e);
            }
        }

        private void PlayheadCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingPlayhead = false;
            (sender as UIElement)?.ReleaseMouseCapture();
        }

        private void UpdateCurrentFrameFromMouse(MouseEventArgs e)
        {
            if (DataContext is not ViewModels.TimelineViewModel timelineVm) return;

            // 再生中に手動でプレイヘッドを移動したら停止
            bool wasPlaying = timelineVm.IsPlaying;
            if (wasPlaying)
            {
                timelineVm.Pause();
            }

            var pos = e.GetPosition(PlayheadCanvas);
            var x = pos.X;
            if (x < 0) x = 0;

            var frame = (int)(x / timelineVm.PixelsPerFrame);
            timelineVm.CurrentFrame = Math.Max(0, frame);
            UpdatePlayheadPosition();

            // Auto-scroll timeline to keep playhead visible
            ScrollToFrame(frame);

            // Pause video and update position
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                // Update video position
                var fps = 30.0;
                var positionSeconds = frame / fps;
                var player = mainWindow.PreviewViewControl.VideoPlayer;

                // Scrub to position to update frame
                player.ScrubbingEnabled = true;
                player.Position = TimeSpan.FromSeconds(positionSeconds);
                player.Pause();

                // Force rendering update
                System.Windows.Media.CompositionTarget.Rendering += ForceFrameUpdate;
            }
        }

        private void ForceFrameUpdate(object? sender, EventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.PreviewViewControl.VideoPlayer.Pause();
            }
            System.Windows.Media.CompositionTarget.Rendering -= ForceFrameUpdate;
        }

        private void UpdatePlayheadToMousePosition(Point mousePos)
        {
            if (DataContext is not ViewModels.TimelineViewModel timelineVm) return;

            // Convert mouse position to playhead canvas coordinates
            var playheadPos = Mouse.GetPosition(PlayheadCanvas);
            var x = playheadPos.X;
            if (x < 0) x = 0;

            var frame = (int)(x / timelineVm.PixelsPerFrame);
            timelineVm.CurrentFrame = Math.Max(0, frame);
            UpdatePlayheadPosition();
        }

        private void UpdatePlayheadPosition()
        {
            if (DataContext is not ViewModels.TimelineViewModel timelineVm) return;

            var x = timelineVm.CurrentFrame * timelineVm.PixelsPerFrame;
            Canvas.SetLeft(PlayheadLine, x);
        }

        // Called when current frame changes
        public void RefreshPlayhead()
        {
            UpdatePlayheadPosition();
        }

        public void UpdateRuler(int totalFrames)
        {
            // Update ruler with time markers based on total frames
            var rulerCanvas = FindName("RulerCanvas") as Canvas;
            if (rulerCanvas == null) return;

            var pixelsPerFrame = (DataContext as ViewModels.TimelineViewModel)?.PixelsPerFrame ?? 1.0;
            var totalWidth = totalFrames * pixelsPerFrame;
            var fps = 30.0;
            var durationSeconds = totalFrames / fps;

            // Clear existing markers
            rulerCanvas.Children.Clear();

            // Determine appropriate interval based on video duration (1/3 decreased spacing for finer granularity)
            int intervalSeconds; // Marker interval in seconds
            if (durationSeconds <= 60)  // Up to 1 minute: ~2 second intervals
                intervalSeconds = 2;
            else if (durationSeconds <= 300)  // Up to 5 minutes: ~3 second intervals
                intervalSeconds = 3;
            else if (durationSeconds <= 1800)  // Up to 30 minutes: 10 second intervals
                intervalSeconds = 10;
            else if (durationSeconds <= 7200)  // Up to 2 hours: 20 second intervals
                intervalSeconds = 20;
            else  // Over 2 hours: 100 second intervals
                intervalSeconds = 100;

            // Add time markers at calculated intervals
            for (int i = 0; i <= durationSeconds; i += intervalSeconds)
            {
                var x = i * fps * pixelsPerFrame;
                var minutes = i / 60;
                var seconds = i % 60;
                var timeText = $"{minutes:00}:{seconds:00}";

                // Vertical line
                var line = new System.Windows.Shapes.Line
                {
                    X1 = x, Y1 = 0, X2 = x, Y2 = 28,
                    Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0x66, 0x66, 0x66)),
                    StrokeThickness = 2
                };
                rulerCanvas.Children.Add(line);

                // Time text
                var text = new TextBlock
                {
                    Text = timeText,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0xe0, 0xe0, 0xe0)),
                    FontSize = 10
                };
                Canvas.SetLeft(text, x + 5);
                Canvas.SetTop(text, 7);
                rulerCanvas.Children.Add(text);
            }
        }

        public void ScrollToFrame(int frame)
        {
            // Skip auto-scroll if user is manually scrolling
            if (_isUserScrolling) return;

            // Scroll timeline to keep playhead visible
            var timelineScrollViewer = FindName("TimelineScrollViewer") as ScrollViewer;
            if (timelineScrollViewer != null)
            {
                var pixelsPerFrame = (DataContext as ViewModels.TimelineViewModel)?.PixelsPerFrame ?? 1.0;
                var playheadPosition = frame * pixelsPerFrame;
                var viewportWidth = timelineScrollViewer.ViewportWidth;
                var currentOffset = timelineScrollViewer.HorizontalOffset;

                // Auto-scroll with margin (scroll when playhead goes beyond visible area)
                var scrollMargin = 50.0;

                // Check if playhead is outside visible area
                if (playheadPosition < currentOffset + scrollMargin)
                {
                    // Playhead is off-screen to the left, scroll left
                    timelineScrollViewer.ScrollToHorizontalOffset(Math.Max(0, playheadPosition - scrollMargin));
                }
                else if (playheadPosition > currentOffset + viewportWidth - scrollMargin)
                {
                    // Playhead is off-screen to the right, scroll right
                    timelineScrollViewer.ScrollToHorizontalOffset(Math.Min(playheadPosition - viewportWidth + scrollMargin, timelineScrollViewer.ExtentWidth - viewportWidth));
                }
            }
        }

        private void TimelineGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Set playhead position when clicking on empty timeline area
            // Get position relative to the PlayheadCanvas to account for the track header offset
            var playheadCanvas = FindName("PlayheadCanvas") as UIElement;
            if (playheadCanvas == null) return;

            if (DataContext is ViewModels.TimelineViewModel timelineVm)
            {
                var mousePos = e.GetPosition(playheadCanvas);
                var pixelsPerFrame = timelineVm.PixelsPerFrame;
                var frame = (int)(mousePos.X / pixelsPerFrame);
                if (frame >= 0)
                {
                    timelineVm.CurrentFrame = frame;
                    UpdatePlayheadPosition();
                }
            }
        }

        private bool _isDraggingOnTracksArea = false;

        private void TracksArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Set playhead position when clicking on tracks area (empty track space)
            if (DataContext is not ViewModels.TimelineViewModel timelineVm) return;

            // Get position relative to the PlayheadCanvas to account for the 100px header offset
            var playheadCanvas = FindName("PlayheadCanvas") as UIElement;
            if (playheadCanvas == null) return;

            var mousePos = e.GetPosition(playheadCanvas);
            var pixelsPerFrame = timelineVm.PixelsPerFrame;
            var frame = (int)(mousePos.X / pixelsPerFrame);
            if (frame >= 0)
            {
                timelineVm.CurrentFrame = frame;
                UpdatePlayheadPosition();
                _isDraggingOnTracksArea = true;
                (sender as UIElement)?.CaptureMouse();
            }
        }

        private void TracksArea_MouseMove(object sender, MouseEventArgs e)
        {
            // ドラッグ中はプレイヘッドをマウス位置に追従させる
            if (_isDraggingOnTracksArea && e.LeftButton == MouseButtonState.Pressed)
            {
                if (DataContext is ViewModels.TimelineViewModel timelineVm)
                {
                    var playheadCanvas = FindName("PlayheadCanvas") as UIElement;
                    if (playheadCanvas == null) return;

                    var mousePos = e.GetPosition(playheadCanvas);
                    var pixelsPerFrame = timelineVm.PixelsPerFrame;
                    var frame = (int)(mousePos.X / pixelsPerFrame);
                    if (frame >= 0)
                    {
                        timelineVm.CurrentFrame = frame;
                        UpdatePlayheadPosition();
                    }
                }
            }
        }

        private void TracksArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingOnTracksArea = false;
            (sender as UIElement)?.ReleaseMouseCapture();
        }
    }
}
