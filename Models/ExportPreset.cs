using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VideoCreatorWPF.Models
{
    /// <summary>
    /// エクスポートプリセット設定
    /// </summary>
    public class ExportPreset
    {
        public string Name { get; set; } = string.Empty;
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public int FrameRate { get; set; } = 30;
        public int VideoBitrate { get; set; } = 8000; // kbps
        public string AudioBitrate { get; set; } = "192k";
        public string Format { get; set; } = "mp4";

        /// <summary>
        /// JSONファイルとしてプリセットを保存
        /// </summary>
        public static void SavePreset(string presetPath, ExportPreset preset)
        {
            var json = JsonSerializer.Serialize(preset, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(presetPath, json);
        }

        /// <summary>
        /// JSONファイルからプリセットを読み込み
        /// </summary>
        public static ExportPreset? LoadPreset(string presetPath)
        {
            if (!File.Exists(presetPath)) return null;

            var json = File.ReadAllText(presetPath);
            return JsonSerializer.Deserialize<ExportPreset>(json);
        }

        /// <summary>
        /// プリセットディレクトリを取得
        /// </summary>
        public static string GetPresetDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var presetDir = Path.Combine(appData, "VideoCreatorWPF", "Presets");

            if (!Directory.Exists(presetDir))
            {
                Directory.CreateDirectory(presetDir);
            }

            return presetDir;
        }

        /// <summary>
        /// 利用可能なプリセット一覧を取得
        /// </summary>
        public static List<string> GetPresetNames()
        {
            var presetDir = GetPresetDirectory();
            var files = Directory.GetFiles(presetDir, "*.json");
            var names = new List<string>();

            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                names.Add(name);
            }

            return names;
        }
    }
}
