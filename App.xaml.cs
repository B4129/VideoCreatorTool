using System.Windows;
using VideoCreatorWPF.Services;

namespace VideoCreatorWPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // VOICEVOXをバックグラウンドで起動
            VoiceVoxService.StartVoiceVoxInBackground();
        }
    }
}
