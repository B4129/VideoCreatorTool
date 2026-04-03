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
                    Debug.WriteLine($"[Audio] PlayAudioAsync called: {audioPath}");

                    if (!File.Exists(audioPath))
                    {
                        Debug.WriteLine($"[Audio] Audio file not found: {audioPath}");
                        return;
                    }

                    // Stop current playback of this audio if any
                    StopAudio(audioPath);

                    var player = new MediaPlayer();

                    // Open and wait for media
                    player.Open(new Uri(audioPath));

                    var timeout = DateTime.Now.AddSeconds(5);
                    while (player.NaturalDuration.TimeSpan == TimeSpan.Zero && DateTime.Now < timeout)
                    {
                        System.Threading.Thread.Sleep(100);
                    }

                    Debug.WriteLine($"[Audio] Media opened, duration: {player.NaturalDuration.TimeSpan}");

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

                    // Play the audio
                    player.Play();
                    Debug.WriteLine($"[Audio] Play() called for: {Path.GetFileName(audioPath)}");

                    // Keep the task alive until playback completes
                    while (player.Position < player.NaturalDuration.TimeSpan)
                    {
                        System.Threading.Thread.Sleep(100);
                    }

                    Debug.WriteLine($"[Audio] Playback completed: {Path.GetFileName(audioPath)}");
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
