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

        public static async Task PlayAudioAsync(string audioPath, double startPositionSeconds = 0, double playbackSpeed = 1.0)
        {
            try
            {
                Debug.WriteLine($"[Audio] PlayAudioAsync called: {audioPath} (speed: {playbackSpeed})");

                if (!File.Exists(audioPath))
                {
                    Debug.WriteLine($"[Audio] Audio file not found: {audioPath}");
                    return;
                }

                // Stop current playback of this audio if any
                StopAudio(audioPath);

                var player = new MediaPlayer();
                _players[audioPath] = player;

                // Set completed event
                var tcs = new TaskCompletionSource<bool>();
                bool completed = false;

                // Subscribe to MediaEnded
                player.MediaEnded += (s, e) =>
                {
                    Debug.WriteLine($"[Audio] MediaEnded: {Path.GetFileName(audioPath)}");
                    completed = true;
                    tcs.TrySetResult(true);
                };

                // Open media asynchronously
                player.MediaOpened += (s, e) =>
                {
                    Debug.WriteLine($"[Audio] MediaOpened, duration: {player.NaturalDuration.TimeSpan}");

                    // Set playback speed
                    player.SpeedRatio = playbackSpeed;
                    Debug.WriteLine($"[Audio] Set playback speed to {playbackSpeed}");

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

                    // Play the audio
                    player.Play();
                    Debug.WriteLine($"[Audio] Play() called for: {Path.GetFileName(audioPath)}");
                };

                player.Open(new Uri(audioPath));

                // Wait for media to end or be stopped
                var timeout = Task.Delay(TimeSpan.FromMinutes(30));
                await Task.WhenAny(tcs.Task, timeout);

                Debug.WriteLine($"[Audio] Playback completed or timed out: {Path.GetFileName(audioPath)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Audio] Playback error: {ex.Message}");
            }
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
