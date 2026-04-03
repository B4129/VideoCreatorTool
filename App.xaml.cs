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

        public ProjectViewModel? CurrentProject { get; set; }
        public MainWindowViewModel? MainViewModel => MainWindow?.DataContext as MainWindowViewModel;

        protected override void OnStartup(StartupEventArgs e)
        {
            _instance = this;
            base.OnStartup(e);

            // VOICEVOXをバックグラウンドで起動
            VoiceVoxService.StartVoiceVoxInBackground();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            // クリーンアップ：一時音声ファイルを削除
            Services.AudioStretchService.CleanupTempFiles();

            System.Diagnostics.Debug.WriteLine("[App] Cleanup completed on exit");
        }
    }
}
