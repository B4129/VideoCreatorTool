using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace VideoCreatorWPF.Services
{
    public class VoiceVoxService
    {
        private static readonly string[] VoiceVoxPaths = new[]
        {
            @"C:\Users\neko3\Desktop\youtube\youtube\VOICEVOX\VOICEVOX.exe",
            @"C:\Program Files\VOICEVOX\VOICEVOX.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"AppData\Local\VOICEVOX\VOICEVOX.exe")
        };

        private static readonly string VoiceVoxUrl = "http://127.0.0.1:50021";
        private static bool _isStarting = false;
        private static bool _isConnected = false;
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        public static async Task<bool> CheckHealth()
        {
            try
            {
                var response = await HttpClient.GetAsync($"{VoiceVoxUrl}/version");
                _isConnected = response.IsSuccessStatusCode;
                return _isConnected;
            }
            catch
            {
                _isConnected = false;
                return false;
            }
        }

        public static void StartVoiceVoxInBackground()
        {
            Task.Run(async () =>
            {
                await EnsureVoiceVoxRunning();
            });
        }

        public static async Task<bool> EnsureVoiceVoxRunning()
        {
            // 既に接続できれば何もしない
            if (await CheckHealth())
            {
                return true;
            }

            // 起動中でなければ起動
            if (!_isStarting)
            {
                _isStarting = true;
                Debug.WriteLine("Starting VOICEVOX in background...");

                // VOICEVOXのパスを探す
                string? voicevoxPath = null;
                foreach (var path in VoiceVoxPaths)
                {
                    if (File.Exists(path))
                    {
                        voicevoxPath = path;
                        Debug.WriteLine($"Found VOICEVOX at: {voicevoxPath}");
                        break;
                    }
                }

                if (voicevoxPath == null)
                {
                    Debug.WriteLine("VOICEVOX not found in any default location");
                    _isStarting = false;
                    return false;
                }

                // 既に起動していないかチェック
                if (IsVoiceVoxRunning())
                {
                    Debug.WriteLine("VOICEVOX is already running");
                    _isStarting = false;
                    _isConnected = true;
                    return true;
                }

                try
                {
                    // VOICEVOXをバックグラウンドで起動（画面に表示しない）
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = voicevoxPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    Process.Start(processStartInfo);
                    Debug.WriteLine("VOICEVOX launch command sent");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to start VoiceVox: {ex.Message}");
                    _isStarting = false;
                    return false;
                }
            }

            // 起動を待機（最大30秒）
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(500);
                if (await CheckHealth())
                {
                    Debug.WriteLine("VOICEVOX started successfully");
                    _isStarting = false;
                    return true;
                }
            }

            Debug.WriteLine("VOICEVOX failed to start within timeout");
            _isStarting = false;
            return false;
        }

        private static bool IsVoiceVoxRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName("VOICEVOX");
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<string?> GenerateAudioFromText(string text, int speakerId = 2)
        {
            try
            {
                if (!await CheckHealth())
                {
                    Debug.WriteLine("VOICEVOX not connected");
                    return null;
                }

                // Step 1: Create audio query
                var queryUrl = $"{VoiceVoxUrl}/audio_query?text={Uri.EscapeDataString(text)}&speaker={speakerId}";
                var queryResponse = await HttpClient.PostAsync(queryUrl, null);
                if (!queryResponse.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Audio query failed: {queryResponse.StatusCode}");
                    return null;
                }

                var queryJson = await queryResponse.Content.ReadAsStringAsync();

                // Step 2: Synthesize audio
                var synthesisUrl = $"{VoiceVoxUrl}/synthesis?speaker={speakerId}";
                var content = new StringContent(queryJson, System.Text.Encoding.UTF8, "application/json");
                var synthesisResponse = await HttpClient.PostAsync(synthesisUrl, content);
                if (!synthesisResponse.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Synthesis failed: {synthesisResponse.StatusCode}");
                    return null;
                }

                // Step 3: Save to temp file
                var tempFile = Path.Combine(Path.GetTempPath(), $"voice_{Guid.NewGuid()}.wav");
                using (var fs = new FileStream(tempFile, FileMode.Create))
                {
                    await synthesisResponse.Content.CopyToAsync(fs);
                }

                Debug.WriteLine($"Audio generated: {tempFile}");
                return tempFile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Audio generation error: {ex.Message}");
                return null;
            }
        }

        public static bool IsConnected => _isConnected;
        public static bool IsStarting => _isStarting;
    }
}
