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

        public VoiceSynthesisDialog()
        {
            InitializeComponent();
            LoadCharacters();
        }

        private void LoadCharacters()
        {
            // TODO: Load characters from current project
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
            // TODO: Load styles for current character from VOICEVOX
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
                    AudioService.PlayAudioAsync(audioPath);
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
    }
}
