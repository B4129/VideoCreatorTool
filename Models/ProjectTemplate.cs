using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VideoCreatorWPF.Models
{
    /// <summary>
    /// プロジェクトテンプレート
    /// </summary>
    public class ProjectTemplate
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public double FrameRate { get; set; } = 30;
        public string BackgroundColor { get; set; } = "#000000";
        public List<TemplateTrack> Tracks { get; set; } = new();
        public List<TemplateCharacter> Characters { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// テンプレートをJSONファイルに保存
        /// </summary>
        public static void SaveTemplate(string filePath, ProjectTemplate template)
        {
            var json = JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// JSONファイルからテンプレートを読み込み
        /// </summary>
        public static ProjectTemplate? LoadTemplate(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ProjectTemplate>(json);
        }

        /// <summary>
        /// テンプレートディレクトリを取得
        /// </summary>
        public static string GetTemplateDirectory()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VideoCreator", "Templates");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        /// <summary>
        /// 組み込みテンプレートを作成
        /// </summary>
        public static ProjectTemplate CreateDefaultTemplate()
        {
            return new ProjectTemplate
            {
                Name = "デフォルト",
                Description = "標準的な動画プロジェクト設定",
                Width = 1920,
                Height = 1080,
                FrameRate = 30,
                BackgroundColor = "#000000",
                Tracks = new List<TemplateTrack>
                {
                    new TemplateTrack { Name = "トラック1", Color = "#3b82f6" },
                    new TemplateTrack { Name = "トラック2", Color = "#10b981" },
                    new TemplateTrack { Name = "トラック3", Color = "#f59e0b" }
                }
            };
        }
    }

    /// <summary>
    /// テンプレートトラック
    /// </summary>
    public class TemplateTrack
    {
        public string Name { get; set; } = "";
        public string Color { get; set; } = "#3b82f6";
    }

    /// <summary>
    /// テンプレートキャラクター
    /// </summary>
    public class TemplateCharacter
    {
        public string Name { get; set; } = "";
        public int SpeakerId { get; set; } = 2;
        public int StyleId { get; set; } = 0;
        public string Color { get; set; } = "#3b82f6";
    }
}
