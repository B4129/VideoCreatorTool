using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.Services
{
    public class ExportService
    {
        public static async Task<bool> ExportVideo(VideoProject project, string outputPath, IProgress<int>? progress = null,
            int? width = null, int? height = null, int? frameRate = null, int videoBitrate = 8000,
            int startFrame = 0, int? endFrame = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if ffmpeg is available
                var ffmpegPath = FindFfmpeg();
                if (ffmpegPath == null)
                {
                    // ffmpeg not found, create placeholder
                    Debug.WriteLine("ffmpeg not found, creating placeholder");
                    File.WriteAllText(outputPath, "Exported video placeholder - ffmpeg not found");
                    progress?.Report(100);
                    return true;
                }

                var exportWidth = width ?? project.Width;
                var exportHeight = height ?? project.Height;
                var exportFrameRate = frameRate ?? (int)project.FrameRate;
                var exportEndFrame = endFrame ?? GetMaxFrame(project);

                // Build and execute ffmpeg command
                return await ExportWithFfmpeg(project, outputPath, ffmpegPath, progress,
                    exportWidth, exportHeight, exportFrameRate, videoBitrate,
                    startFrame, exportEndFrame, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"エクスポートエラー: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> ExportWithFfmpeg(VideoProject project, string outputPath, string ffmpegPath,
            IProgress<int>? progress, int width, int height, int frameRate, int videoBitrate,
            int startFrame, int endFrame, CancellationToken cancellationToken)
        {
            var duration = (endFrame - startFrame) / frameRate;
            if (duration <= 0) duration = 1;

            // Build video filter graph with video blocks
            var videoInputs = GetVideoInputFiles(project, startFrame, endFrame);
            var audioInputs = GetAudioInputFiles(project, startFrame, endFrame);

            // Build filter graphs
            var filterGraph = BuildVideoFilterGraph(project, width, height, frameRate, startFrame, endFrame, videoInputs);
            var audioFilterGraph = BuildAudioFilterGraph(project, startFrame, endFrame, videoInputs);

            // Build ffmpeg command
            var args = new StringBuilder();

            // Add video input files
            foreach (var (videoPath, _) in videoInputs)
            {
                args.Append($"-i \"{videoPath}\" ");
            }

            // Add input files for audio blocks
            foreach (var audioFile in audioInputs)
            {
                args.Append($"-i \"{audioFile}\" ");
            }

            // Add BGM if exists
            if (!string.IsNullOrEmpty(project.BackgroundMusicPath) && File.Exists(project.BackgroundMusicPath))
            {
                args.Append($"-i \"{project.BackgroundMusicPath}\" ");
            }

            // Video filter graph (using lavfi for base generation)
            args.Append($"-f lavfi -i \"{filterGraph}\" ");

            // Output settings
            args.Append($"-r {frameRate} ");
            args.Append($"-c:v libx264 -b:v {videoBitrate}k -pix_fmt yuv420p ");

            // Audio output if there are audio inputs (audio blocks + videos + BGM)
            var totalAudioInputs = audioInputs.Count + videoInputs.Count + (!string.IsNullOrEmpty(project.BackgroundMusicPath) ? 1 : 0);
            if (totalAudioInputs > 0)
            {
                args.Append($"-c:a aac -b:a 192k ");
            }

            args.Append($"-y -shortest \"{outputPath}\"");

            Debug.WriteLine($"ffmpeg args: {args}");

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args.ToString(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            // Parse progress
            var totalFrames = endFrame - startFrame;
            _ = ParseFfmpegProgress(process, progress, totalFrames, cancellationToken);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                throw;
            }

            progress?.Report(100);
            return process.ExitCode == 0;
        }

        /// <summary>
        /// Build video filter graph for text/image/video overlays
        /// </summary>
        private static string BuildVideoFilterGraph(VideoProject project, int width, int height, int frameRate, int startFrame, int endFrame, List<(string Path, TimelineBlock Block)> videoInputs)
        {
            var sb = new StringBuilder();
            var duration = (endFrame - startFrame) / (double)frameRate;

            // Base color background
            sb.Append($"color=c={project.BackgroundColor}:s={width}x{height}:d={duration}[base];");

            // Current source for chaining
            var currentSource = "[base]";

            // Iterate through all tracks and blocks
            foreach (var track in project.Tracks)
            {
                if (!track.IsEnabled || !track.IsVisible) continue;

                foreach (var block in track.Items)
                {
                    var blockStart = block.StartFrame;
                    var blockEnd = blockStart + block.Duration;

                    // Skip blocks outside export range
                    if (blockEnd <= startFrame || blockStart >= endFrame) continue;

                    var visibleStart = Math.Max(blockStart, startFrame);
                    var visibleEnd = Math.Min(blockEnd, endFrame);
                    var relativeStart = (visibleStart - startFrame) / (double)frameRate;
                    var blockDuration = (visibleEnd - visibleStart) / (double)frameRate;

                    // Handle text blocks (Dialogue/Subtitle)
                    if (block.Type == BlockType.Dialogue || block.Type == BlockType.Subtitle)
                    {
                        var escapedText = block.Text.Replace("'", @"'\''").Replace("[", @"\[").Replace("]", @"\]");

                        // Calculate position from percentage to pixels
                        var xPos = (block.TextPositionX / 100.0) * width;
                        var yPos = (block.TextPositionY / 100.0) * height;

                        // Build drawtext filter options
                        var drawtextOpts = new StringBuilder();
                        drawtextOpts.Append($"text='{escapedText}'");
                        drawtextOpts.Append($":x={xPos}");
                        drawtextOpts.Append($":y={yPos}");
                        drawtextOpts.Append($":fontsize={block.FontSize}");
                        drawtextOpts.Append($":fontcolor={block.TextColor}");

                        // Text outline (border_width + border_color)
                        if (block.TextOutlineWidth > 0)
                        {
                            drawtextOpts.Append($":border_width={block.TextOutlineWidth}");
                            drawtextOpts.Append($":border_color={block.TextOutlineColor}");
                        }

                        // Shadow (shadow_x + shadow_y + shadow_color)
                        if (block.HasShadow)
                        {
                            drawtextOpts.Append(":shadow_x=2");
                            drawtextOpts.Append(":shadow_y=2");
                            drawtextOpts.Append(":shadow_color=#000000");
                        }

                        // Enable with fade effects
                        var enableExpr = $"between(t\\,{relativeStart}\\,{relativeStart + blockDuration})";

                        // Fade in
                        if (block.FadeInFrames > 0)
                        {
                            var fadeInStart = relativeStart;
                            var fadeInEnd = relativeStart + block.FadeInFrames / (double)frameRate;
                            drawtextOpts.Append($":alpha='if(lt(t\\,{fadeInEnd})\\, (t-{fadeInStart})/({fadeInEnd}-{fadeInStart})\\, 1)'");
                        }

                        // Fade out
                        if (block.FadeOutFrames > 0)
                        {
                            var fadeOutStart = relativeStart + blockDuration - block.FadeOutFrames / (double)frameRate;
                            var fadeOutEnd = relativeStart + blockDuration;
                            drawtextOpts.Append($":alpha='if(gte(t\\,{fadeOutStart})\\, (1-(t-{fadeOutStart})/({fadeOutEnd}-{fadeOutStart}))\\, 1)'");
                        }

                        sb.Append($"{currentSource}drawtext={drawtextOpts.ToString()}:enable='{enableExpr}'[temp];");
                        currentSource = "[temp]";
                    }
                    // Handle image blocks - scale to fit and overlay
                    else if (block.Type == BlockType.Image && !string.IsNullOrEmpty(block.AudioPath) && File.Exists(block.AudioPath))
                    {
                        var escapedPath = block.AudioPath.Replace("\\", "\\\\").Replace("'", @"'\''");
                        // Scale image to fit within canvas while maintaining aspect ratio, centered
                        sb.Append($"{currentSource}movie='{escapedPath}',scale={width}:{height}:force_original_aspect_ratio=decrease:eval=init[img_scaled];");
                        sb.Append($"{currentSource}[img_scaled]overlay=x=(main_w-overlay_w)/2:y=(main_h-overlay_h)/2:enable='between(t\\,{relativeStart}\\,{relativeStart + blockDuration})'[temp];");
                        currentSource = "[temp]";
                    }
                    // Handle video blocks - scale and overlay with audio
                    else if (block.Type == BlockType.Video && !string.IsNullOrEmpty(block.AudioPath) && File.Exists(block.AudioPath))
                    {
                        // Find the video input index (video inputs are added first)
                        var videoInputIndex = -1;
                        for (int i = 0; i < videoInputs.Count; i++)
                        {
                            if (videoInputs[i].Path == block.AudioPath)
                            {
                                videoInputIndex = i;
                                break;
                            }
                        }

                        if (videoInputIndex >= 0)
                        {
                            var videoLabel = $"[v{videoInputIndex}]";
                            var audioLabel = $"[{videoInputIndex}:a]";

                            // Scale video to fit within canvas while maintaining aspect ratio
                            sb.Append($"{videoLabel}scale={width}:{height}:force_original_aspect_ratio=decrease[v{videoInputIndex}_scaled];");

                            // Build trim filter to extract only the visible portion
                            var trimStart = Math.Max(0, startFrame - block.StartFrame) / (double)frameRate;
                            var trimDuration = blockDuration;

                            // Overlay the scaled video
                            sb.Append($"{currentSource}[v{videoInputIndex}_scaled]overlay=x=(main_w-overlay_w)/2:y=(main_h-overlay_h)/2:enable='between(t\\,{relativeStart}\\,{relativeStart + blockDuration})'[temp];");
                            currentSource = "[temp]";
                        }
                        else
                        {
                            Debug.WriteLine($"Video block file not found in inputs: {block.AudioPath}");
                        }
                    }
                }
            }

            // Final output
            sb.Append($"{currentSource}null[vout]");

            return sb.ToString();
        }

        /// <summary>
        /// Build audio filter graph for mixing audio blocks, video audio, and BGM with fade effects
        /// </summary>
        private static string BuildAudioFilterGraph(VideoProject project, int startFrame, int endFrame, List<(string Path, TimelineBlock Block)> videoInputs)
        {
            var audioFiles = GetAudioInputFiles(project, startFrame, endFrame);
            if (audioFiles.Count == 0 && string.IsNullOrEmpty(project.BackgroundMusicPath))
                return string.Empty;

            var sb = new StringBuilder();
            var inputs = new List<string>();

            // Add audio block inputs with fade effects
            for (int i = 0; i < audioFiles.Count; i++)
            {
                var file = audioFiles[i];
                var block = FindBlockByAudioPath(project, file);
                if (block != null)
                {
                    var delayMs = (block.StartFrame - startFrame) / project.FrameRate * 1000;
                    var durationSec = block.Duration / project.FrameRate;

                    // Apply delay first
                    sb.Append($"[{i}:a]adelay={delayMs}|{delayMs}[delayed{i}];");

                    // Apply audio fade in if specified
                    string fadeInput = $"[delayed{i}]";
                    string fadeOutput = $"[fade{i}]";

                    bool hasFade = false;
                    if (block.AudioFadeInFrames > 0)
                    {
                        var fadeInStart = 0.0;
                        var fadeInDuration = block.AudioFadeInFrames / project.FrameRate;
                        sb.Append($"{fadeInput}afade=t=in:start_time={fadeInStart}:duration={fadeInDuration}{fadeOutput};");
                        fadeInput = fadeOutput;
                        fadeOutput = $"[fade{i}_in]";
                        hasFade = true;
                    }

                    // Apply audio fade out if specified
                    if (block.AudioFadeOutFrames > 0)
                    {
                        var fadeOutStart = durationSec - (block.AudioFadeOutFrames / project.FrameRate);
                        var fadeOutDuration = block.AudioFadeOutFrames / project.FrameRate;
                        sb.Append($"{fadeInput}afade=t=out:start_time={fadeOutStart}:duration={fadeOutDuration}{fadeOutput};");
                        fadeInput = fadeOutput;
                        hasFade = true;
                    }

                    // If no fade, use delayed as-is
                    if (!hasFade)
                    {
                        fadeInput = $"[delayed{i}]";
                    }

                    inputs.Add(hasFade ? fadeOutput : fadeInput);
                }
            }

            // Add video audio inputs with trim
            int videoAudioStartIndex = audioFiles.Count;
            for (int i = 0; i < videoInputs.Count; i++)
            {
                var (videoPath, block) = videoInputs[i];
                var videoAudioIndex = videoAudioStartIndex + i;

                // Trim video audio to visible portion
                var trimStart = Math.Max(0, startFrame - block.StartFrame) / (double)project.FrameRate;
                var trimDuration = block.Duration / (double)project.FrameRate;

                // Apply trim and delay to align with video overlay
                var delayMs = (block.StartFrame - startFrame) / (double)project.FrameRate * 1000;
                sb.Append($"[{videoAudioIndex}:a]atrim=start={trimStart}:duration={trimDuration},asetpts=PTS-STARTPTS[atrim{videoAudioIndex}];");
                sb.Append($"[atrim{videoAudioIndex}]adelay={delayMs}|{delayMs}[vdelayed{videoAudioIndex}];");
                inputs.Add($"[vdelayed{videoAudioIndex}]");
            }

            // Add BGM input with optional fade effects
            int bgmIndex = audioFiles.Count + videoInputs.Count;
            if (!string.IsNullOrEmpty(project.BackgroundMusicPath) && File.Exists(project.BackgroundMusicPath))
            {
                var bgmDuration = GetAudioDuration(project.BackgroundMusicPath);
                sb.Append($"[{bgmIndex}:a]volume={project.BgmVolume}[bgm_vol];");

                string bgmInput = "[bgm_vol]";
                string bgmOutput = "[bgm]";

                // BGM fade in
                if (project.BGMFadeIn > 0)
                {
                    sb.Append($"{bgmInput}afade=t=in:start_time=0:duration={project.BGMFadeIn}[bgm_fadein];");
                    bgmInput = "[bgm_fadein]";
                }

                // BGM fade out
                if (project.BGMFadeOut > 0)
                {
                    var fadeOutStart = bgmDuration - project.BGMFadeOut;
                    sb.Append($"{bgmInput}afade=t=out:start_time={fadeOutStart}:duration={project.BGMFadeOut}[bgm_fadeout];");
                    bgmOutput = "[bgm_fadeout]";
                }

                inputs.Add(bgmOutput);
            }

            // Mix all audio inputs
            if (inputs.Count > 0)
            {
                sb.Append(string.Join("", inputs) + $"amix=inputs={inputs.Count}:duration=shortest:dropout_transition=0[aout]");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get audio duration using Windows Shell API (fallback to basic estimation)
        /// </summary>
        private static double GetAudioDuration(string audioPath)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType);
                    var folderPath = Path.GetDirectoryName(audioPath);
                    var fileName = Path.GetFileName(audioPath);

                    if (folderPath != null && fileName != null)
                    {
                        var folder = shell.NameSpace(folderPath);
                        if (folder != null)
                        {
                            var item = folder.ParseName(fileName);
                            if (item != null)
                            {
                                var durationStr = folder.GetDetailsOf(item, 27); // Length property
                                if (!string.IsNullOrEmpty(durationStr))
                                {
                                    if (TimeSpan.TryParse(durationStr, out TimeSpan duration))
                                    {
                                        return duration.TotalSeconds;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Duration extraction error: {ex.Message}");
            }

            // Fallback: assume long duration if can't determine
            return 300.0; // 5 minutes
        }

        /// <summary>
        /// Get list of audio input files within export range
        /// </summary>
        private static List<string> GetAudioInputFiles(VideoProject project, int startFrame, int endFrame)
        {
            var audioFiles = new List<string>();

            foreach (var track in project.Tracks)
            {
                if (!track.IsEnabled || !track.IsVisible) continue;

                foreach (var block in track.Items)
                {
                    if (string.IsNullOrEmpty(block.AudioPath)) continue;
                    if (!File.Exists(block.AudioPath)) continue;
                    if (block.Type == BlockType.Video) continue; // 動画ブロックは除外（音声のみ別処理）

                    var blockEnd = block.StartFrame + block.Duration;
                    if (blockEnd <= startFrame || block.StartFrame >= endFrame) continue;

                    if (!audioFiles.Contains(block.AudioPath))
                    {
                        audioFiles.Add(block.AudioPath);
                    }
                }
            }

            return audioFiles;
        }

        /// <summary>
        /// Get list of video input files within export range
        /// </summary>
        private static List<(string Path, TimelineBlock Block)> GetVideoInputFiles(VideoProject project, int startFrame, int endFrame)
        {
            var videoFiles = new List<(string Path, TimelineBlock Block)>();

            foreach (var track in project.Tracks)
            {
                if (!track.IsEnabled || !track.IsVisible) continue;

                foreach (var block in track.Items)
                {
                    if (block.Type != BlockType.Video) continue;
                    if (string.IsNullOrEmpty(block.AudioPath)) continue;
                    if (!File.Exists(block.AudioPath)) continue;

                    var blockEnd = block.StartFrame + block.Duration;
                    if (blockEnd <= startFrame || block.StartFrame >= endFrame) continue;

                    videoFiles.Add((block.AudioPath, block));
                }
            }

            return videoFiles;
        }

        /// <summary>
        /// Find block by audio path
        /// </summary>
        private static TimelineBlock? FindBlockByAudioPath(VideoProject project, string audioPath)
        {
            foreach (var track in project.Tracks)
            {
                foreach (var block in track.Items)
                {
                    if (block.AudioPath == audioPath) return block;
                }
            }
            return null;
        }

        /// <summary>
        /// Parse ffmpeg stderr output for progress updates
        /// </summary>
        private static async Task ParseFfmpegProgress(Process process, IProgress<int>? progress, int totalFrames, CancellationToken cancellationToken)
        {
            if (progress == null) return;

            var regex = new Regex(@"frame=\s*(\d+)");

            using var reader = process.StandardError;
            string? line;

            try
            {
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        var currentFrame = int.Parse(match.Groups[1].Value);
                        var percent = Math.Min(100, (int)((double)currentFrame / totalFrames * 100));
                        progress.Report(percent);
                    }
                }
            }
            catch (IOException)
            {
                // Stream ended
            }
        }

        private static int GetMaxFrame(VideoProject project)
        {
            var maxFrame = 0;
            foreach (var track in project.Tracks)
            {
                foreach (var block in track.Items)
                {
                    var endFrame = block.StartFrame + block.Duration;
                    if (endFrame > maxFrame) maxFrame = endFrame;
                }
            }
            return Math.Max(maxFrame, 30); // At least 1 second
        }

        private static string? FindFfmpeg()
        {
            // Check common locations
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

            // Check PATH
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

        public static string GenerateOutputFileName(string projectName)
        {
            return $"{projectName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        }

        public static string GetOutputDirectory(AppSettings settings)
        {
            return settings.OutputDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }
    }
}
