using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;

namespace VideoCreatorWPF.Services
{
    /// <summary>
    /// Simplified audio service - plays one sound at a time
    /// </summary>
    public class AudioService
    {
        private static MediaPlayer? _currentPlayer;
        private static TaskCompletionSource<bool>? _currentPlaybackTcs;

        /// <summary>
        /// Play audio file asynchronously
        /// </summary>
        public static async Task PlayAudioAsync(string audioPath, double startPositionSeconds = 0, double playbackSpeed = 1.0)
        {
            try
            {
                Debug.WriteLine($"[Audio] PlayAudioAsync: {audioPath}");

                if (!File.Exists(audioPath))
                {
                    Debug.WriteLine($"[Audio] File not found: {audioPath}");
                    return;
                }

                // Stop previous playback
                StopAll();

                var player = new MediaPlayer();
                player.Open(new Uri(audioPath));

                // Wait for MediaOpened
                var tcsOpen = new TaskCompletionSource<bool>();
                player.MediaOpened += (s, e) => tcsOpen.TrySetResult(true);

                await Task.WhenAny(tcsOpen.Task, Task.Delay(3000));

                if (!tcsOpen.Task.IsCompleted)
                {
                    Debug.WriteLine("[Audio] MediaOpened timeout");
                    player.Close();
                    return;
                }

                // Set initial position
                if (startPositionSeconds > 0)
                {
                    var pos = TimeSpan.FromSeconds(startPositionSeconds);
                    if (pos < player.NaturalDuration.TimeSpan)
                    {
                        player.Position = pos;
                    }
                }

                // Set playback speed
                player.SpeedRatio = playbackSpeed;

                // Start playback
                _currentPlayer = player;
                _currentPlaybackTcs = new TaskCompletionSource<bool>();
                player.MediaEnded += (s, e) => _currentPlaybackTcs?.TrySetResult(true);

                player.Play();
                Debug.WriteLine($"[Audio] Playing: {Path.GetFileName(audioPath)}");

                // Wait for completion
                await _currentPlaybackTcs.Task;
                Debug.WriteLine("[Audio] Playback ended");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Audio] Error: {ex.Message}");
            }
            finally
            {
                _currentPlayer?.Stop();
                _currentPlayer?.Close();
                _currentPlayer = null;
                _currentPlaybackTcs = null;
            }
        }

        /// <summary>
        /// Stop all playback immediately
        /// </summary>
        public static void StopAll()
        {
            Debug.WriteLine("[Audio] StopAll");
            _currentPlayer?.Stop();
            _currentPlaybackTcs?.TrySetResult(true);
        }
    }
}
