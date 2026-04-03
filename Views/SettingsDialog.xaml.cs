using System.Windows;
using System.Windows.Controls;
using VideoCreatorWPF.ViewModels;

namespace VideoCreatorWPF.Views
{
    public partial class SettingsDialog : Window
    {
        public SettingsDialog()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.SaveCommand.Execute(null);
            }
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.CancelCommand.Execute(null);
            }
            this.Close();
        }
    }
}