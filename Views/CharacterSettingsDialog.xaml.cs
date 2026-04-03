using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using VideoCreatorWPF.Models;
using VideoCreatorWPF.Services;

namespace VideoCreatorWPF.Views
{
    public partial class CharacterSettingsDialog : Window
    {
        private ObservableCollection<Character> _characters = new();
        private Character? _currentCharacter;

        public CharacterSettingsDialog()
        {
            InitializeComponent();
            CharacterList.ItemsSource = _characters;
            LoadCharacters();
        }

        private void LoadCharacters()
        {
            // TODO: Load from project
            _characters.Clear();
            _characters.Add(new Character
            {
                Name = "ずんだもん",
                SpeakerId = 2,
                Color = "#7CFC00",
                Speed = 1.1,
                Pitch = 0.0,
                Intonation = 1.0,
                Volume = 1.0
            });
            _characters.Add(new Character
            {
                Name = "四国めたん",
                SpeakerId = 1,
                Color = "#FF69B4",
                Speed = 1.0,
                Pitch = 0.05,
                Intonation = 1.0,
                Volume = 1.0
            });
            _characters.Add(new Character
            {
                Name = "春日部つむぎ",
                SpeakerId = 8,
                Color = "#FFA500",
                Speed = 1.0,
                Pitch = 0.0,
                Intonation = 1.2,
                Volume = 1.0
            });

            if (_characters.Count > 0)
            {
                CharacterList.SelectedIndex = 0;
            }
        }

        private void CharacterList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CharacterList.SelectedItem is Character character)
            {
                _currentCharacter = character;
                LoadCharacterData();
            }
        }

        private void LoadCharacterData()
        {
            if (_currentCharacter == null) return;

            // Load voice settings
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

            // Load speakers
            SpeakerCombo.Items.Clear();
            SpeakerCombo.Items.Add(new ComboBoxItem { Content = "ずんだもん (Speaker 2)", Tag = 2 });
            SpeakerCombo.Items.Add(new ComboBoxItem { Content = "四国めたん (Speaker 1)", Tag = 1 });
            SpeakerCombo.Items.Add(new ComboBoxItem { Content = "春日部つむぎ (Speaker 8)", Tag = 8 });

            // Select current speaker
            for (int i = 0; i < SpeakerCombo.Items.Count; i++)
            {
                if (SpeakerCombo.Items[i] is ComboBoxItem item && item.Tag is int speakerId && speakerId == _currentCharacter.SpeakerId)
                {
                    SpeakerCombo.SelectedIndex = i;
                    break;
                }
            }
        }

        private async void PreviewVoice_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCharacter == null)
            {
                MessageBox.Show("キャラクターを選択してください。", "警告",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var previewText = "これはテスト音声です。";

            try
            {
                var audioPath = await VoiceVoxService.GenerateAudioWithParams(
                    previewText,
                    _currentCharacter.SpeakerId,
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

        private void VoiceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider && _currentCharacter != null)
            {
                switch (slider.Name)
                {
                    case nameof(SpeedSlider):
                        SpeedBox.Text = slider.Value.ToString("F1");
                        _currentCharacter.Speed = slider.Value;
                        break;
                    case nameof(PitchSlider):
                        PitchBox.Text = slider.Value.ToString("F2");
                        _currentCharacter.Pitch = slider.Value;
                        break;
                    case nameof(IntonationSlider):
                        IntonationBox.Text = slider.Value.ToString("F1");
                        _currentCharacter.Intonation = slider.Value;
                        break;
                    case nameof(VolumeSlider):
                        VolumeBox.Text = slider.Value.ToString("F1");
                        _currentCharacter.Volume = slider.Value;
                        break;
                }
            }
        }

        private void VoiceBox_TextChanged(object sender, TextChangedEventArgs e)
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
            if (sender is Slider slider && _currentCharacter != null)
            {
                switch (slider.Name)
                {
                    case nameof(StartSilenceSlider):
                        StartSilenceBox.Text = ((int)slider.Value).ToString();
                        _currentCharacter.StartSilenceMs = (int)slider.Value;
                        break;
                    case nameof(EndSilenceSlider):
                        EndSilenceBox.Text = ((int)slider.Value).ToString();
                        _currentCharacter.EndSilenceMs = (int)slider.Value;
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

        private void SpeakerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SpeakerCombo.SelectedItem is ComboBoxItem item && item.Tag is int speakerId && _currentCharacter != null)
            {
                _currentCharacter.SpeakerId = speakerId;
            }
        }

        private void AddCharacter_Click(object sender, RoutedEventArgs e)
        {
            var newChar = new Character
            {
                Name = $"キャラクター{_characters.Count + 1}",
                Color = "#3b82f6"
            };
            _characters.Add(newChar);
            CharacterList.SelectedItem = newChar;
        }

        private void BrowseStandingImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "画像ファイル|*.png;*.jpg;*.jpeg;*.bmp;*.gif|すべてのファイル|*.*",
                Title = "立ち絵画像を選択"
            };

            if (dialog.ShowDialog() == true)
            {
                StandingImagePath.Text = dialog.FileName;

                // Preview
                try
                {
                    var bitmap = new BitmapImage(new Uri(dialog.FileName));
                    StandingImagePreview.Source = bitmap;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"画像の読み込みに失敗しました: {ex.Message}", "エラー",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PickFontColor_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement color picker
            FontColorBox.Text = "#FF0000";
            FontColorPreview.Background = new SolidColorBrush(Color.FromRgb(255, 0, 0));
        }

        private void PickOutlineColor_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement color picker
            OutlineColorBox.Text = "#000000";
            OutlineColorPreview.Background = new SolidColorBrush(Color.FromRgb(0, 0, 0));
        }

        private void BrowseDictionaryImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "画像ファイル|*.png;*.jpg;*.jpeg;*.bmp;*.gif|すべてのファイル|*.*",
                Title = "置換画像を選択"
            };

            if (dialog.ShowDialog() == true)
            {
                // Set path to selected row in dictionary grid
                if (DictionaryGrid.SelectedItem is IconAlias alias)
                {
                    alias.ImagePath = dialog.FileName;
                }
            }
        }

        private void AddDictionaryRow_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Add new row to dictionary
        }

        private void DeleteDictionaryRow_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Delete selected row
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

                LoadCharacterData();
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("設定を保存しました。", "成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
