using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.Services
{
    /// <summary>
    /// SRT字幕ファイルのインポート・エクスポートサービス
    /// </summary>
    public static class SubtitleService
    {
        public class SubtitleEntry
        {
            public int Index { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
            public string Text { get; set; } = string.Empty;
        }

        /// <summary>
        /// SRTファイルを解析して字幕エントリリストを返す
        /// </summary>
        public static List<SubtitleEntry>? ImportSrt(string srtPath)
        {
            try
            {
                if (!File.Exists(srtPath))
                {
                    throw new FileNotFoundException($"SRTファイルが見つかりません: {srtPath}");
                }

                var content = File.ReadAllText(srtPath, Encoding.UTF8);
                return ParseSrtContent(content);
            }
            catch (Exception ex)
            {
                throw new Exception($"SRTインポートエラー: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// SRT文字列を解析
        /// </summary>
        public static List<SubtitleEntry> ParseSrtContent(string content)
        {
            var entries = new List<SubtitleEntry>();

            // SRT形式: 番号\n開始 --> 終了\nテキスト\n\n
            var pattern = @"(\d+)\s+(\d{2}:\d{2}:\d{2},\d{3})\s+-->\s+(\d{2}:\d{2}:\d{2},\d{3})\s+([\s\S]*?)(?=\n\s*\d+\s+\d{2}:\d{2}:\d{2},\d{3}\s+-->|\z)";
            var matches = Regex.Matches(content, pattern);

            foreach (Match match in matches)
            {
                if (match.Groups.Count < 4) continue;

                var index = int.Parse(match.Groups[1].Value);
                var startTime = ParseTimecode(match.Groups[2].Value);
                var endTime = ParseTimecode(match.Groups[3].Value);
                var text = match.Groups[4].Value.Trim();

                entries.Add(new SubtitleEntry
                {
                    Index = index,
                    StartTime = startTime,
                    EndTime = endTime,
                    Text = text
                });
            }

            return entries;
        }

        /// <summary>
        /// SRT形式の時間文字列をTimeSpanに変換
        /// "00:00:01,000" -> TimeSpan
        /// </summary>
        private static TimeSpan ParseTimecode(string tc)
        {
            // SRT形式: HH:MM:SS,mmm
            var parts = tc.Split(',');
            var timePart = parts[0];
            var msPart = parts.Length > 1 ? parts[1] : "0";

            var timeComponents = timePart.Split(':');
            var hours = int.Parse(timeComponents[0]);
            var minutes = int.Parse(timeComponents[1]);
            var seconds = int.Parse(timeComponents[2]);
            var milliseconds = int.Parse(msPart);

            return new TimeSpan(0, hours, minutes, seconds, milliseconds);
        }

        /// <summary>
        /// 字幕エントリをタイムラインブロックに変換
        /// </summary>
        public static List<TimelineBlock> ConvertToTimelineBlocks(
            List<SubtitleEntry> subtitles,
            double frameRate,
            string trackName = "字幕トラック")
        {
            var blocks = new List<TimelineBlock>();

            foreach (var sub in subtitles)
            {
                var startFrame = (int)(sub.StartTime.TotalSeconds * frameRate);
                var endFrame = (int)(sub.EndTime.TotalSeconds * frameRate);
                var duration = endFrame - startFrame;

                if (duration <= 0) continue;

                var block = new TimelineBlock
                {
                    Type = BlockType.Subtitle,
                    StartFrame = startFrame,
                    Duration = duration,
                    Text = sub.Text.Replace("\n", " "), // 改行をスペースに
                    FontSize = 48,
                    TextColor = "#FFFFFF",
                    TextPositionX = 50,
                    TextPositionY = 85,
                    TextOutlineWidth = 2,
                    TextOutlineColor = "#000000",
                    HasShadow = true,
                    FadeInFrames = 0,
                    FadeOutFrames = 0
                };

                blocks.Add(block);
            }

            return blocks;
        }

        /// <summary>
        /// タイムラインブロックをSRT形式にエクスポート
        /// </summary>
        public static string ExportToSrt(IEnumerable<TimelineBlock> blocks, double frameRate)
        {
            var sb = new StringBuilder();
            var index = 1;

            // 字幕ブロックのみを抽出して時間順にソート
            var subtitleBlocks = blocks
                .Where(b => b.Type == BlockType.Subtitle)
                .OrderBy(b => b.StartFrame)
                .ToList();

            foreach (var block in subtitleBlocks)
            {
                var startTime = FrameToTimecode(block.StartFrame, frameRate);
                var endTime = FrameToTimecode(block.StartFrame + block.Duration, frameRate);

                sb.AppendLine(index.ToString());
                sb.AppendLine($"{startTime} --> {endTime}");
                sb.AppendLine(block.Text);
                sb.AppendLine();

                index++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// フレーム数をSRT形式の時間文字列に変換
        /// </summary>
        private static string FrameToTimecode(int frame, double frameRate)
        {
            var ts = TimeSpan.FromSeconds(frame / frameRate);
            // SRT形式: HH:MM:SS,mmm
            return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}";
        }

        /// <summary>
        /// SRTファイルをプロジェクトにインポート
        /// </summary>
        public static bool ImportToProject(
            VideoProject project,
            string srtPath,
            int targetTrackIndex = -1)
        {
            try
            {
                var subtitles = ImportSrt(srtPath);
                if (subtitles == null || subtitles.Count == 0)
                {
                    return false;
                }

                var frameRate = project.FrameRate;
                var blocks = ConvertToTimelineBlocks(subtitles, frameRate);

                // ターゲットトラックを取得または作成
                TimelineTrack? targetTrack;
                if (targetTrackIndex >= 0 && targetTrackIndex < project.Tracks.Count)
                {
                    targetTrack = project.Tracks[targetTrackIndex];
                }
                else
                {
                    targetTrack = new TimelineTrack
                    {
                        Name = "字幕トラック",
                        IsVisible = true,
                        IsEnabled = true
                    };
                    project.Tracks.Add(targetTrack);
                }

                // ブロックを追加
                foreach (var block in blocks)
                {
                    block.TrackId = targetTrack.Id;
                    targetTrack.Items.Add(block);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SRTインポートエラー: {ex.Message}");
                return false;
            }
        }
    }
}
