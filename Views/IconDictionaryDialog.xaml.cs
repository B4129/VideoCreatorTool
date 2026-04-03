using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VideoCreatorWPF.Models;
using VideoCreatorWPF.Services;

namespace VideoCreatorWPF.Views
{
    public partial class IconDictionaryDialog : Window
    {
        private Guid _currentCharacterId;
        private ObservableCollection<IconAlias> _icons = new();
        private List<Character> _characters = new();

        public IconDictionaryDialog()
        {
            InitializeComponent();
            IconList.ItemsSource = _icons;
            LoadCharacters();
        }

        private void LoadCharacters()
        {
            // Load characters from current project
            // TODO: Load from ProjectService or singleton
            var mainWindow = Application.Current.MainWindow?.DataContext as ViewModels.MainWindowViewModel;
            if (mainWindow?.CurrentProjectViewModel != null)
            {
                // For now, use test data
                // TODO: Get from actual project when Project class is implemented
                CreateTestCharacters();
            }
            else
            {
                CreateTestCharacters();
            }

            if (CharacterCombo.Items.Count > 0)
            {
                CharacterCombo.SelectedIndex = 0;
            }
        }

        private void CreateTestCharacters()
        {
            _characters = new List<Character>
            {
                new Character { Name = "ずんだもん", Id = Guid.NewGuid(), SpeakerId = 2 },
                new Character { Name = "四国めたん", Id = Guid.NewGuid(), SpeakerId = 1 },
                new Character { Name = "春日部つむぎ", Id = Guid.NewGuid(), SpeakerId = 8 }
            };

            CharacterCombo.Items.Clear();
            foreach (var character in _characters)
            {
                CharacterCombo.Items.Add(new ComboBoxItem
                {
                    Content = character.Name,
                    Tag = character.Id
                });
            }
        }

        private void CharacterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CharacterCombo.SelectedItem is ComboBoxItem item && item.Tag is Guid characterId)
            {
                _currentCharacterId = characterId;

                // Load icons for selected character
                _icons.Clear();
                var character = _characters.FirstOrDefault(c => c.Id == characterId);
                if (character != null)
                {
                    foreach (var alias in character.IconAliases)
                    {
                        _icons.Add(alias);
                    }
                }
            }
        }

        private void BrowseImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "画像ファイル|*.png;*.jpg;*.jpeg;*.bmp;*.gif|すべてのファイル|*.*",
                Title = "画像を選択"
            };

            if (dialog.ShowDialog() == true)
            {
                ImagePathBox.Text = dialog.FileName;
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var searchKey = SearchKeyBox.Text.Trim();
            var imagePath = ImagePathBox.Text.Trim();

            if (string.IsNullOrEmpty(searchKey) || string.IsNullOrEmpty(imagePath))
            {
                MessageBox.Show("検索キーと画像パスを入力してください。", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var alias = new IconAlias
            {
                SearchKey = searchKey,
                ImagePath = imagePath,
                HeightScale = double.TryParse(HeightScaleBox.Text, out var scale) ? scale : 1.0,
                OffsetX = double.TryParse(OffsetXBox.Text, out var offsetX) ? offsetX : 0.0,
                OffsetY = double.TryParse(OffsetYBox.Text, out var offsetY) ? offsetY : 0.0,
                IsEnabled = EnabledCheck.IsChecked ?? true,
                Description = DescriptionBox.Text
            };

            _icons.Add(alias);
            IconDictionaryService.Instance.AddIconAlias(_currentCharacterId, alias);

            // Clear form
            SearchKeyBox.Clear();
            ImagePathBox.Clear();
            HeightScaleBox.Text = "1.0";
            OffsetXBox.Text = "0";
            OffsetYBox.Text = "0";
            DescriptionBox.Clear();
            EnabledCheck.IsChecked = true;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (IconList.SelectedItem is IconAlias selected)
            {
                var result = MessageBox.Show($"「{selected.SearchKey}」を削除しますか？", "確認",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _icons.Remove(selected);
                    IconDictionaryService.Instance.RemoveIconAlias(_currentCharacterId, selected.SearchKey);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Save settings
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            IconDictionaryService.Instance.SaveSettings(basePath);

            Close();
        }
    }
}
