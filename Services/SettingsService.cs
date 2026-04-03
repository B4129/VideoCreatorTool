using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.Services
{
    public class SettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VideoCreatorWPF",
            "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private static AppSettings? _cachedSettings;

        public static async Task<AppSettings> LoadSettings()
        {
            if (_cachedSettings != null) return _cachedSettings;

            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = await File.ReadAllTextAsync(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null)
                    {
                        _cachedSettings = settings;
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"設定読み込みエラー: {ex.Message}");
            }

            _cachedSettings = new AppSettings
            {
                VoiceVoxPath = FindVoiceVoxPath(),
                OutputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Theme = "Dark",
                AutoSave = true,
                AutoSaveInterval = 5
            };
            return _cachedSettings;
        }

        public static async Task SaveSettings(AppSettings settings)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                await File.WriteAllTextAsync(SettingsPath, json);
                _cachedSettings = settings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"設定保存エラー: {ex.Message}");
            }
        }

        private static string FindVoiceVoxPath()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"AppData\Local\VOICEVOX\VOICEVOX.exe"),
                @"C:\Program Files\VOICEVOX\VOICEVOX.exe",
                @"C:\Program Files (x86)\VOICEVOX\VOICEVOX.exe"
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return string.Empty;
        }
    }
}
