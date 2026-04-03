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
        public static async Task PlayAudioAsync(string audioPath, string blockId, double startPositionSeconds = 0, double playbackSpeed = 1.0, double volume = 1.0, bool isMuted = false)
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
                    IsMuted = isMuted
                };
                _audioTracks[blockId] = track;

                var player = new MediaPlayer();
                player.Open(new Uri(audioPath));
                player.Volume = isMuted ? 0.0 : volume;

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
                    playbackTcs?.TrySetResult(true);
                    _audioTracks.TryRemove(blockId, out _);
                };

                player.Play();
                Debug.WriteLine($"[Audio] Playing: {Path.GetFileName(audioPath)} (Block {blockId})");

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
        }
    }
}
