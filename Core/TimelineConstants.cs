namespace VideoCreatorWPF.Core
{
    /// <summary>
    /// Timeline related constants
    /// </summary>
    public static class TimelineConstants
    {
        /// <summary>
        /// Frames per second for timeline calculation
        /// </summary>
        public const double FramesPerSecond = 30.0;

        /// <summary>
        /// Minimum pixels per frame for zoom (zoomed out)
        /// </summary>
        public const double MinPixelsPerFrame = 0.1;

        /// <summary>
        /// Maximum pixels per frame for zoom (zoomed in)
        /// </summary>
        public const double MaxPixelsPerFrame = 5.0;

        /// <summary>
        /// Default grid line interval in pixels
        /// </summary>
        public const double GridLinePixelInterval = 50.0;

        /// <summary>
        /// Track header height in pixels
        /// </summary>
        public const double TrackHeaderHeight = 24.0;

        /// <summary>
        /// Ruler height in pixels
        /// </summary>
        public const double RulerHeight = 32.0;

        /// <summary>
        /// Snap threshold in frames
        /// </summary>
        public const int SnapThresholdFrames = 5;

        /// <summary>
        /// Playback timer interval in milliseconds (33ms = 30fps)
        /// </summary>
        public const int PlaybackTimerIntervalMs = 33;
    }
}
