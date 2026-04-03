using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace VideoCreatorWPF.Services
{
    /// <summary>
    /// Auto-download ffmpeg binaries on first run
    /// Downloads from official builds only when needed
    /// </summary>
    public static class FFMpegDownloadService
    {
        private static readonly string AppDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string FFMpegExe = Path.Combine(AppDir, "ffmpeg.exe");
        private static readonly string FFMpegDir = Path.Combine(AppDir, "ffmpeg");

        // Using a lightweight static build (around 70MB for full features)
        private const string FFMpegDownloadUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

        /// <summary>
        /// Check if ffmpeg is available (either bundled or in PATH)
        /// </summary>
        public static bool IsFFMpegAvailable()
        {
            // Check bundled
            if (File.Exists(FFMpegExe))
            {
                Debug.WriteLine("[FFMpegDownload] Found bundled ffmpeg");
                return true;
            }

            // Check PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var directory in pathEnv.Split(';'))
                {
                    var fullPath = Path.Combine(directory, "ffmpeg.exe");
                    if (File.Exists(fullPath))
                    {
                        Debug.WriteLine($"[FFMpegDownload] Found ffmpeg in PATH: {fullPath}");
                        return true;
                    }
                }
            }

            Debug.WriteLine("[FFMpegDownload] ffmpeg not available");
            return false;
        }

        /// <summary>
        /// Auto-download ffmpeg if not available
        /// </summary>
        public static async Task<bool> EnsureFFMpegAsync()
        {
            if (IsFFMpegAvailable())
            {
                return true;
            }

            try
            {
                Debug.WriteLine("[FFMpegDownload] Downloading ffmpeg...");

                // Create download directory
                Directory.CreateDirectory(FFMpegDir);

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(5);

                    // Download zip
                    var zipPath = Path.Combine(FFMpegDir, "ffmpeg.zip");
                    var data = await client.GetByteArrayAsync(FFMpegDownloadUrl);
                    await File.WriteAllBytesAsync(zipPath, data);

                    Debug.WriteLine("[FFMpegDownload] Download complete, extracting...");

                    // Extract
                    ZipFile.ExtractToDirectory(zipPath, FFMpegDir, overwriteFiles: true);

                    // Find ffmpeg.exe in extracted structure
                    var extractedExe = FindFFMpegInExtracted(FFMpegDir);
                    if (extractedExe != null && File.Exists(extractedExe))
                    {
                        // Move to app directory
                        File.Copy(extractedExe, FFMpegExe, overwrite: true);

                        Debug.WriteLine($"[FFMpegDownload] ffmpeg installed to: {FFMpegExe}");
                        return true;
                    }

                    Debug.WriteLine("[FFMpegDownload] Could not find ffmpeg.exe in archive");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FFMpegDownload] Error: {ex.Message}");
                return false;
            }
        }

        private static string FindFFMpegInExtracted(string directory)
        {
            try
            {
                // Search recursively for ffmpeg.exe
                var files = Directory.GetFiles(directory, "ffmpeg.exe", SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    return files[0];
                }

                // Try ffprobe.exe as fallback
                files = Directory.GetFiles(directory, "ffprobe.exe", SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    return Path.Combine(Path.GetDirectoryName(files[0]), "ffmpeg.exe");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FFMpegDownload] Error searching: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get ffmpeg version info
        /// </summary>
        public static async Task<string> GetFFMpegVersionAsync()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = FFMpegExe,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        var output = await process.StandardOutput.ReadToEndAsync();
                        await process.WaitForExitAsync();

                        // Get first line which contains version
                        var lines = output.Split('\n');
                        return lines.Length > 0 ? lines[0] : "Unknown";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FFMpegDownload] Version check error: {ex.Message}");
            }

            return "Not available";
        }
    }
}
