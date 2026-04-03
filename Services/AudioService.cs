using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;

namespace VideoCreatorWPF.Services
{
    public class AudioService
    {
        private static ConcurrentDictionary<string, MediaPlayer> _players = new();

        public static Task PlayAudioAsync(string audioPath, double startPositionSeconds = 0)
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

                    // Stop current playback of this audio if any
                    StopAudio(audioPath);

                    var player = new MediaPlayer();
                    player.Open(new Uri(audioPath));

                    // Wait for media to open
                    var timeout = DateTime.Now.AddSeconds(5);
                    while (player.NaturalDuration.TimeSpan == TimeSpan.Zero && DateTime.Now < timeout)
                    {
                        System.Threading.Thread.Sleep(50);
                    }

                    // Seek to start position if specified
                    if (startPositionSeconds > 0)
                    {
                        var position = TimeSpan.FromSeconds(startPositionSeconds);
                        var duration = player.NaturalDuration.TimeSpan;
                        if (position < duration)
                        {
                            player.Position = position;
                            Debug.WriteLine($"[Audio] Seeking to {startPositionSeconds}s in {audioPath}");
                        }
                    }

                    _players[audioPath] = player;
                    player.Play();

                    Debug.WriteLine($"[Audio] Playing: {audioPath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Audio] Playback error: {ex.Message}");
                }
            });
        }

        public static void StopAudio(string audioPath)
        {
            try
            {
                if (_players.TryRemove(audioPath, out var player))
                {
                    player.Stop();
                    player.Close();
                    Debug.WriteLine($"[Audio] Stopped: {audioPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Audio] Stop error: {ex.Message}");
            }
        }

        public static void StopAll()
        {
            foreach (var kvp in _players)
            {
                try
                {
                    kvp.Value.Stop();
                    kvp.Value.Close();
                }
                catch { }
            }
            _players.Clear();
        }

        public static bool IsPlaying { get; private set; }
    }
}
