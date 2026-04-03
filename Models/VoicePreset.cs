using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace VideoCreatorWPF.Models
{
    /// <summary>
    /// 音声合成プリセット
    /// </summary>
    public class VoicePreset : INotifyPropertyChanged
    {
        private string _name = "";
        private double _speed = 1.0;
        private double _pitch = 0.0;
        private double _intonation = 1.0;
        private double _volume = 1.0;
        private int _startSilenceMs = 200;
        private int _endSilenceMs = 200;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public double Speed
        {
            get => _speed;
            set => SetProperty(ref _speed, value);
        }

        public double Pitch
        {
            get => _pitch;
            set => SetProperty(ref _pitch, value);
        }

        public double Intonation
        {
            get => _intonation;
            set => SetProperty(ref _intonation, value);
        }

        public double Volume
        {
            get => _volume;
            set => SetProperty(ref _volume, value);
        }

        public int StartSilenceMs
        {
            get => _startSilenceMs;
            set => SetProperty(ref _startSilenceMs, value);
        }

        public int EndSilenceMs
        {
            get => _endSilenceMs;
            set => SetProperty(ref _endSilenceMs, value);
        }

        public VoicePreset Clone()
        {
            return new VoicePreset
            {
                Name = this.Name,
                Speed = this.Speed,
                Pitch = this.Pitch,
                Intonation = this.Intonation,
                Volume = this.Volume,
                StartSilenceMs = this.StartSilenceMs,
                EndSilenceMs = this.EndSilenceMs
            };
        }

        public void CopyFrom(VoicePreset source)
        {
            Name = source.Name;
            Speed = source.Speed;
            Pitch = source.Pitch;
            Intonation = source.Intonation;
            Volume = source.Volume;
            StartSilenceMs = source.StartSilenceMs;
            EndSilenceMs = source.EndSilenceMs;
        }
    }

    /// <summary>
    /// プリセットマネージャー
    /// </summary>
    public static class PresetManager
    {
        private static string PresetFilePath
        {
            get
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(appDir, "presets.json");
            }
        }

        private static List<VoicePreset> _presets = new();

        /// <summary>
        /// 全プリセットを取得
        /// </summary>
        public static List<VoicePreset> GetAll()
        {
            if (_presets.Count == 0)
            {
                LoadPresets();
            }
            return new List<VoicePreset>(_presets);
        }

        /// <summary>
        /// プリセットを追加
        /// </summary>
        public static void Add(VoicePreset preset)
        {
            _presets.Add(preset);
            SavePresets();
        }

        /// <summary>
        /// プリセットを削除
        /// </summary>
        public static void Remove(VoicePreset preset)
        {
            _presets.Remove(preset);
            SavePresets();
        }

        /// <summary>
        /// プリセットを更新
        /// </summary>
        public static void Update(VoicePreset preset)
        {
            var existing = _presets.Find(p => p.Name == preset.Name);
            if (existing != null)
            {
                existing.CopyFrom(preset);
                SavePresets();
            }
        }

        /// <summary>
        /// プリセットを保存
        /// </summary>
        public static void SavePresets()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_presets, options);
                File.WriteAllText(PresetFilePath, json, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"プリセット保存エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// プリセットを読み込み
        /// </summary>
        private static void LoadPresets()
        {
            try
            {
                if (File.Exists(PresetFilePath))
                {
                    var json = File.ReadAllText(PresetFilePath, System.Text.Encoding.UTF8);
                    _presets = JsonSerializer.Deserialize<List<VoicePreset>>(json) ?? new List<VoicePreset>();
                }
                else
                {
                    // デフォルトプリセットを作成
                    _presets = new List<VoicePreset>
                    {
                        new VoicePreset { Name = "デフォルト", Speed = 1.0, Pitch = 0.0, Intonation = 1.0, Volume = 1.0, StartSilenceMs = 200, EndSilenceMs = 200 },
                        new VoicePreset { Name = "速口", Speed = 1.5, Pitch = 0.05, Intonation = 1.2, Volume = 1.0, StartSilenceMs = 100, EndSilenceMs = 100 },
                        new VoicePreset { Name = "ゆっくり", Speed = 0.8, Pitch = -0.05, Intonation = 0.8, Volume = 1.0, StartSilenceMs = 300, EndSilenceMs = 300 },
                        new VoicePreset { Name = "ロボット", Speed = 1.0, Pitch = 0.0, Intonation = 0.1, Volume = 1.0, StartSilenceMs = 200, EndSilenceMs = 200 }
                    };
                    SavePresets();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"プリセット読み込みエラー: {ex.Message}");
                _presets = new List<VoicePreset>();
            }
        }
    }
}
