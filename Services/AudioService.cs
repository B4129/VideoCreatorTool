using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace VideoCreatorWPF.Services
{
    public class AudioService
    {
        private static SoundPlayer? _currentPlayer;
        private static string? _currentAudioPath;

        public static Task PlayAudioAsync(string audioPath)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(audioPath))
                    {
                        Debug.WriteLine($"Audio file not found: {audioPath}");
                        return;
                    }

                    // Stop current playback if any
                    StopAudio();

                    _currentPlayer = new SoundPlayer(audioPath);
                    _currentAudioPath = audioPath;
                    _currentPlayer.Play();

                    Debug.WriteLine($"Playing audio: {audioPath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Audio playback error: {ex.Message}");
                }
            });
        }

        public static void StopAudio()
        {
            try
            {
                if (_currentPlayer != null)
                {
                    _currentPlayer.Stop();
                    _currentPlayer.Dispose();
                    _currentPlayer = null;
                    _currentAudioPath = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Audio stop error: {ex.Message}");
            }
        }

        public static bool IsPlaying { get; private set; }
    }
}
