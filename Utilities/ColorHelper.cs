using System;

namespace VideoCreatorWPF.Utilities
{
    /// <summary>
    /// Color utility functions
    /// </summary>
    public static class ColorHelper
    {
        private static readonly string[] DefaultColors = new[]
        {
            "#3b82f6", "#ef4444", "#10b981", "#f59e0b", "#8b5cf6", "#ec4899"
        };

        private static readonly Random _random = new Random();

        /// <summary>
        /// Get a random color from the default palette
        /// </summary>
        public static string GetRandomColor()
        {
            return DefaultColors[_random.Next(DefaultColors.Length)];
        }
    }
}
