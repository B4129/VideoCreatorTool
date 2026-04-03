using System;
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

        public IconDictionaryDialog()
        {
            InitializeComponent();
            IconList.ItemsSource = _icons;
            LoadCharacters();
        }

        private void LoadCharacters()
        {
            // TODO: Load characters from project
            // For now, just show placeholder
            CharacterCombo.Items.Add(new ComboBoxItem { Content = "キャラクターを選択" });
            CharacterCombo.SelectedIndex = 0;
        }

        private void CharacterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TODO: Load icons for selected character
            // IconDictionaryService.Instance.GetCharacterIcons(characterId)
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
