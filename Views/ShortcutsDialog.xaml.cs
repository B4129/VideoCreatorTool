using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System;

namespace VideoCreatorWPF.Views
{
    public partial class ShortcutsDialog : Window
    {
        public ObservableCollection<ShortcutConfig> Shortcuts { get; } = new();

        public ShortcutsDialog()
        {
            InitializeComponent();
            InitializeShortcuts();
            ShortcutsList.ItemsSource = Shortcuts;
        }

        private void InitializeShortcuts()
        {
            Shortcuts.Add(new ShortcutConfig { Name = "新規プロジェクト", KeySequence = "Ctrl+N" });
            Shortcuts.Add(new ShortcutConfig { Name = "プロジェクトを開く", KeySequence = "Ctrl+O" });
            Shortcuts.Add(new ShortcutConfig { Name = "保存", KeySequence = "Ctrl+S" });
            Shortcuts.Add(new ShortcutConfig { Name = "再生/停止", KeySequence = "Space" });
            Shortcuts.Add(new ShortcutConfig { Name = "スナップON/OFF", KeySequence = "S" });
            Shortcuts.Add(new ShortcutConfig { Name = "削除", KeySequence = "Delete" });
            Shortcuts.Add(new ShortcutConfig { Name = "コピー", KeySequence = "Ctrl+C" });
            Shortcuts.Add(new ShortcutConfig { Name = "ペースト", KeySequence = "Ctrl+V" });

            LoadShortcutsFromConfig();
        }

        private void Shortcut_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                var modifiers = Keyboard.Modifiers;
                var key = e.Key == Key.System ? e.SystemKey : e.Key;

                var keySequence = "";
                if ((modifiers & ModifierKeys.Control) != 0)
                    keySequence += "Ctrl+";
                if ((modifiers & ModifierKeys.Alt) != 0)
                    keySequence += "Alt+";
                if ((modifiers & ModifierKeys.Shift) != 0)
                    keySequence += "Shift+";

                if (key != Key.LeftCtrl && key != Key.RightCtrl &&
                    key != Key.LeftAlt && key != Key.RightAlt &&
                    key != Key.LeftShift && key != Key.RightShift)
                {
                    keySequence += key.ToString();
                }

                textBox.Text = keySequence;
                e.Handled = true;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveShortcutsToConfig();
            DialogResult = true;
            Close();
        }

        private void SaveShortcutsToConfig()
        {
            var config = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var shortcut in Shortcuts)
            {
                config[shortcut.Name] = shortcut.KeySequence;
            }

            var json = System.Text.Json.JsonSerializer.Serialize(config);
            var configPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VideoCreatorWPF", "shortcuts.json");

            var configDir = System.IO.Path.GetDirectoryName(configPath);
            if (!System.IO.Directory.Exists(configDir))
            {
                System.IO.Directory.CreateDirectory(configDir);
            }

            System.IO.File.WriteAllText(configPath, json);
        }

        private void LoadShortcutsFromConfig()
        {
            var configPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VideoCreatorWPF", "shortcuts.json");

            if (System.IO.File.Exists(configPath))
            {
                try
                {
                    var json = System.IO.File.ReadAllText(configPath);
                    var config = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(json);
                    if (config != null)
                    {
                        foreach (var kvp in config)
                        {
                            var shortcut = Shortcuts.FirstOrDefault(s => s.Name == kvp.Key);
                            if (shortcut != null)
                            {
                                shortcut.KeySequence = kvp.Value;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore errors, use defaults
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class ShortcutConfig
    {
        public string Name { get; set; } = "";
        public string KeySequence { get; set; } = "";
    }
}
