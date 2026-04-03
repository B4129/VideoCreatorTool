using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Enums;

namespace VideoCreatorWPF.Services
{
    /// <summary>
    /// Audio stretching service using FFMpegCore library
    /// No external ffmpeg installation required - bundled with the app
    /// Stretches/compresses audio files to match target duration
    /// </summary>
    public static class AudioStretchService
    {
        private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "VideoCreator", "AudioStretch");
        private static bool _ffmpegConfigurationChecked;

        static AudioStretchService()
        {
            Directory.CreateDirectory(TempDir);
            ConfigureFFMpegPaths();
        }

        /// <summary>
        /// Stretch audio file to target duration using FFMpegCore (synchronous for progress support)
        /// </summary>
        /// <param name="audioPath">Original audio file path</param>
        /// <param name="originalDurationFrames">Original duration in frames (30fps)</param>
        /// <param name="targetDurationFrames">Target duration in frames (30fps)</param>
        /// <param name="progress">Optional progress callback (0-100)</param>
        /// <returns>Path to stretched audio file (temp file)</returns>
        public static string Stretch(string audioPath, int originalDurationFrames, int targetDurationFrames, IProgress<int>? progress = null)
        {
            try
            {
                Debug.WriteLine($"[AudioStretch] START: Original={originalDurationFrames}f, Target={targetDurationFrames}f");
                progress?.Report(10);

                if (!File.Exists(audioPath))
                {
                    Debug.WriteLine($"[AudioStretch] File not found: {audioPath}");
                    return audioPath;
                }

                // If durations are nearly identical, no stretching needed
                if (Math.Abs(originalDurationFrames - targetDurationFrames) < 2)
                {
                    Debug.WriteLine("[AudioStretch] Durations nearly identical, no stretching needed");
                    progress?.Report(100);
                    return audioPath;
                }

                progress?.Report(20);

                var originalDurationSeconds = originalDurationFrames / 30.0;
                var targetDurationSeconds = targetDurationFrames / 30.0;
                var stretchFactor = targetDurationSeconds / originalDurationSeconds;

                Debug.WriteLine($"[AudioStretch] Stretch factor: {stretchFactor:F3}x");
                progress?.Report(30);

                // Build audio filter for stretching
                var audioFilter = BuildAudioStretchFilter(stretchFactor);
                progress?.Report(40);

                var outputFileName = $"{Guid.NewGuid()}{Path.GetExtension(audioPath)}";
                var outputPath = Path.Combine(TempDir, outputFileName);

                Debug.WriteLine($"[AudioStretch] Using FFMpegCore filter: {audioFilter}");
                progress?.Report(50);

                // Use FFMpegCore to process audio (synchronous to support progress)
                var success = FFMpegCore.FFMpegArguments
                    .FromFileInput(audioPath)
                    .OutputToFile(outputPath, overwrite: true, options => options
                        .ForceFormat("wav")
                        .WithCustomArgument($"-af \"{audioFilter}\" -vn"))
                    .ProcessSynchronously();

                progress?.Report(90);

                if (success && File.Exists(outputPath))
                {
                    Debug.WriteLine($"[AudioStretch] SUCCESS: {outputPath}");
                    progress?.Report(100);
                    return outputPath;
                }
                else
                {
                    Debug.WriteLine("[AudioStretch] FFMpegCore processing failed, returning original file");
                    return audioPath;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioStretch] ERROR: {ex.Message}");
                return audioPath; // Return original on error
            }
        }

        /// <summary>
        /// Build audio filter string for stretching
        /// </summary>
        private static string BuildAudioStretchFilter(double stretchFactor)
        {
            // atempo filter: 1/stretchFactor because atempo > 1 speeds up, < 1 slows down
            var atempoValue = 1.0 / stretchFactor;

            // Single atempo filter for 0.5-2.0 range
            if (atempoValue >= 0.5 && atempoValue <= 2.0)
            {
                return $"atempo={atempoValue:F3}";
            }

            // Chain multiple atempo filters for wider range
            // Example: atempo=0.7,atempo=0.7 for 0.49x
            var singleFactor = Math.Sqrt(stretchFactor);
            var singleAtempo = 1.0 / singleFactor;

            // Validate chained atempo values
            if (singleAtempo >= 0.5 && singleAtempo <= 2.0)
            {
                return $"atempo={singleAtempo:F3},atempo={singleAtempo:F3}";
            }

            // Fallback to single filter (may have quality issues but will work)
            Debug.WriteLine($"[AudioStretch] Warning: Stretch factor {stretchFactor:F3}x is outside optimal range, using single atempo={atempoValue:F3}");
            return $"atempo={atempoValue:F3}";
        }

        /// <summary>
        /// Configure FFMpegCore to find bundled ffmpeg binaries
        /// </summary>
        private static void ConfigureFFMpegPaths()
        {
            if (_ffmpegConfigurationChecked) return;

            try
            {
                // Check if ffmpeg is in application directory
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var ffmpegExe = Path.Combine(appDir, "ffmpeg.exe");

                if (File.Exists(ffmpegExe))
                {
                    GlobalFFOptions.Configure(options => options.BinaryFolder = appDir);
                    Debug.WriteLine($"[AudioStretch] Using bundled ffmpeg from: {appDir}");
                }
                else
                {
                    // Try to use system ffmpeg if available
                    Debug.WriteLine("[AudioStretch] No bundled ffmpeg found, will attempt system PATH lookup");
                }

                _ffmpegConfigurationChecked = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioStretch] Failed to configure ffmpeg paths: {ex.Message}");
                _ffmpegConfigurationChecked = true;
            }
        }

        /// <summary>
        /// Clean up temporary stretched audio files
        /// </summary>
        public static void CleanupTempFiles()
        {
            try
            {
                if (Directory.Exists(TempDir))
                {
                    var files = Directory.GetFiles(TempDir);
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[AudioStretch] Failed to delete temp file: {file}, {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioStretch] Cleanup error: {ex.Message}");
            }
        }
    }
}
