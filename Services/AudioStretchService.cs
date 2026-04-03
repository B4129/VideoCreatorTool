using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace VideoCreatorWPF.Services
{
    /// <summary>
    /// Audio stretching service using ffmpeg
    /// Stretches/compresses audio files to match target duration
    /// </summary>
    public static class AudioStretchService
    {
        private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "VideoCreator", "AudioStretch");

        static AudioStretchService()
        {
            Directory.CreateDirectory(TempDir);
        }

        /// <summary>
        /// Stretch audio file to target duration
        /// </summary>
        /// <param name="audioPath">Original audio file path</param>
        /// <param name="originalDurationFrames">Original duration in frames (30fps)</param>
        /// <param name="targetDurationFrames">Target duration in frames (30fps)</param>
        /// <returns>Path to stretched audio file (temp file)</returns>
        public static async Task<string> StretchAsync(string audioPath, int originalDurationFrames, int targetDurationFrames)
        {
            try
            {
                Debug.WriteLine($"[AudioStretch] START: Original={originalDurationFrames}f, Target={targetDurationFrames}f");

                if (!File.Exists(audioPath))
                {
                    Debug.WriteLine($"[AudioStretch] File not found: {audioPath}");
                    return audioPath;
                }

                // If durations are nearly identical, no stretching needed
                if (Math.Abs(originalDurationFrames - targetDurationFrames) < 2)
                {
                    Debug.WriteLine("[AudioStretch] Durations nearly identical, no stretching needed");
                    return audioPath;
                }

                var originalDurationSeconds = originalDurationFrames / 30.0;
                var targetDurationSeconds = targetDurationFrames / 30.0;
                var stretchFactor = targetDurationSeconds / originalDurationSeconds;

                Debug.WriteLine($"[AudioStretch] Stretch factor: {stretchFactor:F3}x");

                // atempo filter supports 0.5-2.0 range
                // For factors outside this range, we need to chain filters
                if (stretchFactor < 0.5 || stretchFactor > 2.0)
                {
                    Debug.WriteLine("[AudioStretch] Factor outside 0.5-2.0 range, using chained atempo filters");
                    return await StretchWithChainedFilters(audioPath, stretchFactor);
                }

                return await StretchWithAtempo(audioPath, stretchFactor);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioStretch] ERROR: {ex.Message}");
                return audioPath; // Return original on error
            }
        }

        private static async Task<string> StretchWithAtempo(string audioPath, double stretchFactor)
        {
            var ffmpegPath = FindFFmpeg();
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                Debug.WriteLine("[AudioStretch] ffmpeg not found, returning original file");
                return audioPath;
            }

            var outputFileName = $"{Guid.NewGuid()}{Path.GetExtension(audioPath)}";
            var outputPath = Path.Combine(TempDir, outputFileName);

            // atempo filter: 1/stretchFactor because atempo > 1 speeds up, < 1 slows down
            var atempoValue = 1.0 / stretchFactor;

            var arguments = $"-y -i \"{audioPath}\" -filter:a \"atempo={atempoValue:F3}\" -vn \"{outputPath}\"";

            Debug.WriteLine($"[AudioStretch] Running: {ffmpegPath} {arguments}");

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    Debug.WriteLine("[AudioStretch] Failed to start ffmpeg");
                    return audioPath;
                }

                var stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    Debug.WriteLine($"[AudioStretch] ffmpeg failed: {stderr}");
                    return audioPath;
                }
            }

            Debug.WriteLine($"[AudioStretch] SUCCESS: {outputPath}");
            return outputPath;
        }

        private static async Task<string> StretchWithChainedFilters(string audioPath, double stretchFactor)
        {
            var ffmpegPath = FindFFmpeg();
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                return audioPath;
            }

            // Chain atempo filters to support wider range
            // Example: atempo=0.7,atempo=0.7 supports 0.49x
            string filterChain;
            if (stretchFactor < 0.5)
            {
                // Slowing down: need multiple atempo < 1
                var singleFactor = Math.Pow(stretchFactor, 0.5);
                filterChain = $"atempo={singleFactor:F3},atempo={singleFactor:F3}";
            }
            else
            {
                // Speeding up: need multiple atempo > 1
                var singleFactor = Math.Pow(stretchFactor, 0.5);
                filterChain = $"atempo={singleFactor:F3},atempo={singleFactor:F3}";
            }

            var outputFileName = $"{Guid.NewGuid()}{Path.GetExtension(audioPath)}";
            var outputPath = Path.Combine(TempDir, outputFileName);

            var arguments = $"-y -i \"{audioPath}\" -filter:a \"{filterChain}\" -vn \"{outputPath}\"";

            Debug.WriteLine($"[AudioStretch] Running chained: {ffmpegPath} {arguments}");

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    return audioPath;
                }

                var stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    Debug.WriteLine($"[AudioStretch] ffmpeg chained failed: {stderr}");
                    return audioPath;
                }
            }

            Debug.WriteLine($"[AudioStretch] CHAINED SUCCESS: {outputPath}");
            return outputPath;
        }

        private static string FindFFmpeg()
        {
            // Check PATH first
            var ffmpegPath = FindExecutableInPath("ffmpeg.exe");
            if (!string.IsNullOrEmpty(ffmpegPath))
            {
                return ffmpegPath;
            }

            // Check common locations
            var commonPaths = new[]
            {
                @"C:\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ffmpeg", "ffmpeg.exe")
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    Debug.WriteLine($"[AudioStretch] Found ffmpeg at: {path}");
                    return path;
                }
            }

            Debug.WriteLine("[AudioStretch] ffmpeg not found in common locations");
            return null;
        }

        private static string FindExecutableInPath(string executableName)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
            {
                return null;
            }

            foreach (var directory in pathEnv.Split(';'))
            {
                var fullPath = Path.Combine(directory, executableName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
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
