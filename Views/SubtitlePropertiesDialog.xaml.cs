using System.Windows;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.Views
{
    public partial class SubtitlePropertiesDialog : Window
    {
        public TimelineBlock? SelectedBlock { get; private set; }

        public SubtitlePropertiesDialog()
        {
            InitializeComponent();
        }

        public SubtitlePropertiesDialog(TimelineBlock block) : this()
        {
            SelectedBlock = block;
            DataContext = block;
            Owner = Application.Current.MainWindow;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
