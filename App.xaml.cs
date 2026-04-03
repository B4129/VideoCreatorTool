using System.Windows;
using VideoCreatorWPF.Models;
using VideoCreatorWPF.Services;
using VideoCreatorWPF.ViewModels;

namespace VideoCreatorWPF
{
    public partial class App : Application
    {
        private static App? _instance;
        public static App Instance => _instance;

        public object? CurrentProject => null; // TODO: Implement when Project class exists
        public MainWindowViewModel? MainViewModel => MainWindow?.DataContext as MainWindowViewModel;

        protected override void OnStartup(StartupEventArgs e)
        {
            _instance = this;
            base.OnStartup(e);

            // VOICEVOXをバックグラウンドで起動
            VoiceVoxService.StartVoiceVoxInBackground();
        }
    }
}
