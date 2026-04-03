using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;

namespace VideoCreatorWPF.Services
{
    /// <summary>
    /// Multi-track audio service supporting simultaneous playback
    /// Manages MediaPlayer instances by block ID
    /// </summary>
    public class AudioService
    {
        private static readonly ConcurrentDictionary<string, AudioTrack> _audioTracks = new();

        /// <summary>
        /// Play audio file asynchronously for a specific block
        /// </summary>
        public static async Task PlayAudioAsync(string audioPath, string blockId, double startPositionSeconds = 0, double playbackSpeed = 1.0, double volume = 1.0, bool isMuted = false, int audioFadeInFrames = 0, int audioFadeOutFrames = 0)
        {
            try
            {
                Debug.WriteLine($"[Audio] PlayAudioAsync START: BlockId={blockId}, Path={audioPath}");

                if (string.IsNullOrEmpty(audioPath))
                {
                    Debug.WriteLine($"[Audio] No audio path provided for block {blockId}");
                    return;
                }

                if (!File.Exists(audioPath))
                {
                    Debug.WriteLine($"[Audio] File not found: {audioPath}");
                    return;
                }

                // Stop existing playback for this block if any
                StopAudio(blockId);

                // Create new audio track
                var track = new AudioTrack
                {
                    Volume = volume,
                    IsMuted = isMuted,
                    FadeInFrames = audioFadeInFrames,
                    FadeOutFrames = audioFadeOutFrames
                };
                _audioTracks[blockId] = track;

                var player = new MediaPlayer();
                player.Open(new Uri(audioPath));
                player.Volume = isMuted ? 0.0 : (audioFadeInFrames > 0 ? 0.0 : volume);

                // Wait for MediaOpened
                var tcsOpen = new TaskCompletionSource<bool>();
                player.MediaOpened += (s, e) => tcsOpen.TrySetResult(true);

                await Task.WhenAny(tcsOpen.Task, Task.Delay(3000));

                if (!tcsOpen.Task.IsCompleted)
                {
                    Debug.WriteLine("[Audio] MediaOpened timeout");
                    player.Close();
                    _audioTracks.TryRemove(blockId, out _);
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

                // Store player and start playback
                track.Player = player;
                var playbackTcs = new TaskCompletionSource<bool>();
                track.PlaybackTcs = playbackTcs;

                player.MediaEnded += (s, e) =>
                {
                    // Fade out before ending
                    if (track.FadeOutFrames > 0)
                    {
                        ApplyFadeOut(player, track, track.FadeOutFrames);
                    }
                    playbackTcs?.TrySetResult(true);
                    _audioTracks.TryRemove(blockId, out _);
                };

                player.Play();
                Debug.WriteLine($"[Audio] Playing: {Path.GetFileName(audioPath)} (Block {blockId})");

                // Apply fade in if specified
                if (track.FadeInFrames > 0)
                {
                    ApplyFadeIn(player, track, track.FadeInFrames);
                }

                // Wait for completion
                await playbackTcs.Task;
                Debug.WriteLine($"[Audio] Playback ended: Block {blockId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Audio] Error for block {blockId}: {ex.Message}");
            }
            finally
            {
                StopAudio(blockId);
            }
        }

        /// <summary>
        /// Stop audio playback for a specific block
        /// </summary>
        public static void StopAudio(string blockId)
        {
            if (_audioTracks.TryRemove(blockId, out var track))
            {
                Debug.WriteLine($"[Audio] Stopping block {blockId}");
                track.Player?.Stop();
                track.Player?.Close();
                track.PlaybackTcs?.TrySetResult(true);
            }
        }

        /// <summary>
        /// Stop all audio playback immediately
        /// </summary>
        public static void StopAll()
        {
            Debug.WriteLine($"[Audio] StopAll - stopping {_audioTracks.Count} tracks");

            foreach (var kvp in _audioTracks)
            {
                kvp.Value.Player?.Stop();
                kvp.Value.Player?.Close();
                kvp.Value.PlaybackTcs?.TrySetResult(true);
            }

            _audioTracks.Clear();
        }

        /// <summary>
        /// Check if a specific block is playing
        /// </summary>
        public static bool IsPlaying(string blockId)
        {
            return _audioTracks.ContainsKey(blockId);
        }

        /// <summary>
        /// Apply fade in effect
        /// </summary>
        private static void ApplyFadeIn(MediaPlayer player, AudioTrack track, int fadeFrames)
        {
            var fadeDurationMs = fadeFrames * 33; // 30fps = 33ms per frame
            var startTime = DateTime.Now;
            var startVolume = 0.0;
            var endVolume = track.Volume;

            var timer = new System.Threading.Timer(state =>
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                var progress = Math.Min(1.0, elapsed / fadeDurationMs);
                var currentVolume = startVolume + (endVolume - startVolume) * progress;

                player.Volume = currentVolume;

                if (progress >= 1.0)
                {
                    ((System.Threading.Timer)state).Dispose();
                }
            }, null, 0, 16); // Update every 16ms for smooth fade
        }

        /// <summary>
        /// Apply fade out effect
        /// </summary>
        private static void ApplyFadeOut(MediaPlayer player, AudioTrack track, int fadeFrames)
        {
            var fadeDurationMs = fadeFrames * 33; // 30fps = 33ms per frame
            var startTime = DateTime.Now;
            var startVolume = player.Volume;
            var endVolume = 0.0;

            var timer = new System.Threading.Timer(state =>
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                var progress = Math.Min(1.0, elapsed / fadeDurationMs);
                var currentVolume = startVolume - (startVolume - endVolume) * progress;

                player.Volume = currentVolume;

                if (progress >= 1.0)
                {
                    ((System.Threading.Timer)state).Dispose();
                    player.Volume = 0.0;
                }
            }, null, 0, 16); // Update every 16ms for smooth fade
        }

        /// <summary>
        /// Stop playback at a specific frame (called when timeline position changes)
        /// </summary>
        public static void StopAudiosAtFrame(int frame)
        {
            Debug.WriteLine($"[Audio] Stopping audios at frame {frame}");

            foreach (var kvp in _audioTracks)
            {
                kvp.Value.Player?.Stop();
                kvp.Value.Player?.Close();
                kvp.Value.PlaybackTcs?.TrySetResult(true);
            }

            _audioTracks.Clear();
        }

        /// <summary>
        /// Internal audio track representation
        /// </summary>
        private class AudioTrack
        {
            public MediaPlayer Player { get; set; } = null!;
            public TaskCompletionSource<bool> PlaybackTcs { get; set; } = null!;
            public double Volume { get; set; } = 1.0;
            public bool IsMuted { get; set; }
            public int FadeInFrames { get; set; } = 0;
            public int FadeOutFrames { get; set; } = 0;
            public bool IsFadingIn { get; set; } = false;
            public bool IsFadingOut { get; set; } = false;
        }
    }
}
