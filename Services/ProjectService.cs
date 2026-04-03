using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.Services
{
    public class ProjectService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static async Task<VideoProject?> LoadProject(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var project = JsonSerializer.Deserialize<VideoProject>(json, JsonOptions);
                if (project != null)
                {
                    project.FilePath = filePath;
                }
                return project;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"プロジェクト読み込みエラー: {ex.Message}");
                return null;
            }
        }

        public static async Task<bool> SaveProject(VideoProject project, string filePath)
        {
            try
            {
                project.ModifiedAt = DateTime.Now;
                var json = JsonSerializer.Serialize(project, JsonOptions);
                await File.WriteAllTextAsync(filePath, json);
                project.FilePath = filePath;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"プロジェクト保存エラー: {ex.Message}");
                return false;
            }
        }

        public static VideoProject CreateNewProject(string name = "新規プロジェクト")
        {
            return new VideoProject
            {
                Name = name,
                CreatedAt = DateTime.Now,
                ModifiedAt = DateTime.Now
            };
        }

        public static string GenerateDefaultFileName(string projectName)
        {
            return $"{projectName.Replace(" ", "_")}.vcp";
        }
    }
}
