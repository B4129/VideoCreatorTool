using System;
using System.Collections.Generic;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.ViewModels
{
    /// <summary>
    /// Data for block move undo/redo action
    /// </summary>
    public class BlockMoveData
    {
        public TimelineBlock Block { get; set; } = null!;
        public int OldStartFrame { get; set; }
        public int NewStartFrame { get; set; }
        public int OldDuration { get; set; }
        public int NewDuration { get; set; }
    }

    /// <summary>
    /// Data for block delete undo/redo action
    /// </summary>
    public class DeleteBlockData
    {
        public TimelineTrackViewModel Track { get; set; } = null!;
        public TimelineBlock Block { get; set; } = null!;
        public int Index { get; set; }
    }

    /// <summary>
    /// Data for property change undo/redo action
    /// </summary>
    public class PropertyChangeData
    {
        public object Target { get; set; } = null!;
        public string PropertyName { get; set; } = string.Empty;
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
    }

    /// <summary>
    /// Data for text edit undo/redo action
    /// </summary>
    public class TextEditData
    {
        public TimelineBlock Block { get; set; } = null!;
        public string OldText { get; set; } = string.Empty;
        public string NewText { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data for track operation undo/redo action
    /// </summary>
    public class TrackOperationData
    {
        public TimelineTrackViewModel? Track { get; set; }
        public Models.TimelineTrack? ModelTrack { get; set; }
        public int Index { get; set; }
    }

    /// <summary>
    /// Represents an undo/redo action in the timeline
    /// </summary>
    public class TimelineAction
    {
        public string Action { get; set; } = string.Empty;
        public object? Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Grid line item for timeline rendering
    /// </summary>
    public class GridLineItem : ViewModelBase
    {
        private double _x;
        private string _color = "#444444";

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
    /// Event args for video block start
    /// </summary>
    public class VideoBlockEventArgs : EventArgs
    {
        public string VideoPath { get; set; } = string.Empty;
        public int StartFrame { get; set; }
        public int Duration { get; set; }
    }

    /// <summary>
    /// 波形データポイント
    /// </summary>
    public class WaveformDataPoint
    {
        public double X { get; set; }
        public double Amplitude { get; set; }
    }

    /// <summary>
    /// 波形表示用データ
    /// </summary>
    public class WaveformDisplayData
    {
        public List<WaveformDataPoint> Points { get; set; } = new();
        public double MaxAmplitude { get; set; } = 1.0;
    }
}
