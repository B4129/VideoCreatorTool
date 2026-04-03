using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VideoCreatorWPF.Models;
using VideoCreatorWPF.Services;

namespace VideoCreatorWPF.Views
{
    public partial class VoiceSynthesisDialog : Window
    {
        private Character? _currentCharacter;
        private List<Character> _characters = new();
        private List<VoicePreset> _presets = new();
        private bool _isPresetListUpdating = false;

        public VoiceSynthesisDialog()
        {
            InitializeComponent();
            LoadCharacters();
            LoadPresets();
        }

        private void LoadCharacters()
        {
            // Load characters from current project (uses test data until full project implementation)
            _characters = new List<Character>
            {
                new Character { Name = "ずんだもん", SpeakerId = 2, StyleId = 0 },
                new Character { Name = "四国めたん", SpeakerId = 1, StyleId = 0 },
                new Character { Name = "春日部つむぎ", SpeakerId = 8, StyleId = 0 }
            };

            SpeakerCombo.Items.Clear();
            foreach (var chara in _characters)
            {
                SpeakerCombo.Items.Add(new ComboBoxItem { Content = chara.Name, Tag = chara.Id });
            }

            if (SpeakerCombo.Items.Count > 0)
            {
                SpeakerCombo.SelectedIndex = 0;
            }
        }

        private void LoadPresets()
        {
            _presets = PresetManager.GetAll();
            RefreshPresetCombo();
        }

        private void RefreshPresetCombo()
        {
            _isPresetListUpdating = true;
            var currentSelection = PresetCombo.SelectedItem?.ToString();
            PresetCombo.Items.Clear();
            foreach (var preset in _presets)
            {
                PresetCombo.Items.Add(new ComboBoxItem { Content = preset.Name, Tag = preset });
            }
            // 以前の選択を復元
            if (!string.IsNullOrEmpty(currentSelection))
            {
                for (int i = 0; i < PresetCombo.Items.Count; i++)
                {
                    if (PresetCombo.Items[i] is ComboBoxItem item && item.Content.ToString() == currentSelection)
                    {
                        PresetCombo.SelectedIndex = i;
                        break;
                    }
                }
            }
            else if (PresetCombo.Items.Count > 0)
            {
                PresetCombo.SelectedIndex = 0;
            }
            _isPresetListUpdating = false;
        }

        private void SpeakerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SpeakerCombo.SelectedItem is ComboBoxItem item && item.Tag is Guid id)
            {
                _currentCharacter = _characters.FirstOrDefault(c => c.Id == id);
                if (_currentCharacter != null)
                {
                    LoadCharacterParameters();
                }
            }
        }

        private void LoadCharacterParameters()
        {
            if (_currentCharacter == null) return;

            // Load style
            StyleCombo.Items.Clear();
            // Load styles for current character from VOICEVOX (to be implemented)
            StyleCombo.Items.Add(new ComboBoxItem { Content = "ノーマル", IsSelected = true });

            // Load parameters
            SpeedSlider.Value = _currentCharacter.Speed;
            SpeedBox.Text = _currentCharacter.Speed.ToString("F1");

            PitchSlider.Value = _currentCharacter.Pitch;
            PitchBox.Text = _currentCharacter.Pitch.ToString("F2");

            IntonationSlider.Value = _currentCharacter.Intonation;
            IntonationBox.Text = _currentCharacter.Intonation.ToString("F1");

            VolumeSlider.Value = _currentCharacter.Volume;
            VolumeBox.Text = _currentCharacter.Volume.ToString("F1");

            StartSilenceSlider.Value = _currentCharacter.StartSilenceMs;
            StartSilenceBox.Text = _currentCharacter.StartSilenceMs.ToString();

            EndSilenceSlider.Value = _currentCharacter.EndSilenceMs;
            EndSilenceBox.Text = _currentCharacter.EndSilenceMs.ToString();
        }

        private async void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCharacter == null)
            {
                MessageBox.Show("キャラクターを選択してください。", "警告",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Preview with current settings
            var previewText = "これはテスト音声です。";
            var speakerId = _currentCharacter.SpeakerId;

            try
            {
                // Generate audio with current parameters
                var audioPath = await VoiceVoxService.GenerateAudioWithParams(
                    previewText,
                    speakerId,
                    _currentCharacter.StyleId,
                    _currentCharacter.Speed,
                    _currentCharacter.Pitch,
                    _currentCharacter.Intonation
                );

                if (!string.IsNullOrEmpty(audioPath))
                {
                    AudioService.PlayAudioAsync(audioPath, $"voice_{Guid.NewGuid()}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"音声生成に失敗しました: {ex.Message}", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ParameterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider)
            {
                switch (slider.Name)
                {
                    case nameof(SpeedSlider):
                        SpeedBox.Text = slider.Value.ToString("F1");
                        if (_currentCharacter != null) _currentCharacter.Speed = slider.Value;
                        break;
                    case nameof(PitchSlider):
                        PitchBox.Text = slider.Value.ToString("F2");
                        if (_currentCharacter != null) _currentCharacter.Pitch = slider.Value;
                        break;
                    case nameof(IntonationSlider):
                        IntonationBox.Text = slider.Value.ToString("F1");
                        if (_currentCharacter != null) _currentCharacter.Intonation = slider.Value;
                        break;
                    case nameof(VolumeSlider):
                        VolumeBox.Text = slider.Value.ToString("F1");
                        if (_currentCharacter != null) _currentCharacter.Volume = slider.Value;
                        break;
                }
            }
        }

        private void ParameterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox box && _currentCharacter != null)
            {
                switch (box.Name)
                {
                    case nameof(SpeedBox):
                        if (double.TryParse(box.Text, out var val))
                        {
                            SpeedSlider.Value = val;
                            _currentCharacter.Speed = val;
                        }
                        break;
                    case nameof(PitchBox):
                        if (double.TryParse(box.Text, out val))
                        {
                            PitchSlider.Value = val;
                            _currentCharacter.Pitch = val;
                        }
                        break;
                    case nameof(IntonationBox):
                        if (double.TryParse(box.Text, out val))
                        {
                            IntonationSlider.Value = val;
                            _currentCharacter.Intonation = val;
                        }
                        break;
                    case nameof(VolumeBox):
                        if (double.TryParse(box.Text, out val))
                        {
                            VolumeSlider.Value = val;
                            _currentCharacter.Volume = val;
                        }
                        break;
                }
            }
        }

        private void TimingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider)
            {
                switch (slider.Name)
                {
                    case nameof(StartSilenceSlider):
                        StartSilenceBox.Text = ((int)slider.Value).ToString();
                        if (_currentCharacter != null) _currentCharacter.StartSilenceMs = (int)slider.Value;
                        break;
                    case nameof(EndSilenceSlider):
                        EndSilenceBox.Text = ((int)slider.Value).ToString();
                        if (_currentCharacter != null) _currentCharacter.EndSilenceMs = (int)slider.Value;
                        break;
                }
            }
        }

        private void TimingBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox box && _currentCharacter != null)
            {
                switch (box.Name)
                {
                    case nameof(StartSilenceBox):
                        if (int.TryParse(box.Text, out var val))
                        {
                            StartSilenceSlider.Value = val;
                            _currentCharacter.StartSilenceMs = val;
                        }
                        break;
                    case nameof(EndSilenceBox):
                        if (int.TryParse(box.Text, out val))
                        {
                            EndSilenceSlider.Value = val;
                            _currentCharacter.EndSilenceMs = val;
                        }
                        break;
                }
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCharacter != null)
            {
                _currentCharacter.Speed = 1.0;
                _currentCharacter.Pitch = 0.0;
                _currentCharacter.Intonation = 1.0;
                _currentCharacter.Volume = 1.0;
                _currentCharacter.StartSilenceMs = 200;
                _currentCharacter.EndSilenceMs = 200;

                LoadCharacterParameters();
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            // Save parameters to character
            if (_currentCharacter != null)
            {
                // Parameters are already updated in real-time
                MessageBox.Show("設定を保存しました。", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // === Preset management event handlers ===

        private void LoadPreset_Click(object sender, RoutedEventArgs e)
        {
            if (PresetCombo.SelectedItem is ComboBoxItem item && item.Tag is VoicePreset preset)
            {
                if (_currentCharacter == null)
                {
                    MessageBox.Show("キャラクターを選択してください。", "警告",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Apply preset to current character
                _currentCharacter.Speed = preset.Speed;
                _currentCharacter.Pitch = preset.Pitch;
                _currentCharacter.Intonation = preset.Intonation;
                _currentCharacter.Volume = preset.Volume;
                _currentCharacter.StartSilenceMs = preset.StartSilenceMs;
                _currentCharacter.EndSilenceMs = preset.EndSilenceMs;

                // Update UI
                LoadCharacterParameters();

                MessageBox.Show($"プリセット「{preset.Name}」を適用しました。", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("プリセットを選択してください。", "警告",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SavePreset_Click(object sender, RoutedEventArgs e)
        {
            // Show preset save panel
            PresetSavePanel.Visibility = Visibility.Visible;
            PresetNameBox.Text = "";
            PresetNameBox.Focus();
        }

        private void PresetConfirmSave_Click(object sender, RoutedEventArgs e)
        {
            var name = PresetNameBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("プリセット名を入力してください。", "警告",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if preset with same name exists
            var existing = _presets.Find(p => p.Name == name);
            if (existing != null)
            {
                var result = MessageBox.Show(
                    $"プリセット「{name}」は既に存在します。上書きしますか？",
                    "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                // Update existing preset
                existing.Speed = _currentCharacter?.Speed ?? 1.0;
                existing.Pitch = _currentCharacter?.Pitch ?? 0.0;
                existing.Intonation = _currentCharacter?.Intonation ?? 1.0;
                existing.Volume = _currentCharacter?.Volume ?? 1.0;
                existing.StartSilenceMs = _currentCharacter?.StartSilenceMs ?? 200;
                existing.EndSilenceMs = _currentCharacter?.EndSilenceMs ?? 200;

                PresetManager.Update(existing);
            }
            else
            {
                // Create new preset
                var newPreset = new VoicePreset
                {
                    Name = name,
                    Speed = _currentCharacter?.Speed ?? 1.0,
                    Pitch = _currentCharacter?.Pitch ?? 0.0,
                    Intonation = _currentCharacter?.Intonation ?? 1.0,
                    Volume = _currentCharacter?.Volume ?? 1.0,
                    StartSilenceMs = _currentCharacter?.StartSilenceMs ?? 200,
                    EndSilenceMs = _currentCharacter?.EndSilenceMs ?? 200
                };

                PresetManager.Add(newPreset);
                _presets.Add(newPreset);
            }

            RefreshPresetCombo();
            PresetSavePanel.Visibility = Visibility.Collapsed;

            MessageBox.Show("プリセットを保存しました。", "成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PresetCancelSave_Click(object sender, RoutedEventArgs e)
        {
            PresetSavePanel.Visibility = Visibility.Collapsed;
        }
    }
}
