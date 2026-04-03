using System;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.Services
{
    public class AutoSaveService : IDisposable
    {
        private readonly System.Timers.Timer _timer;
        private VideoProject? _currentProject;
        private AppSettings _settings;
        private bool _disposed = false;

        public event Action<string>? AutoSaved;
        public TimeSpan Interval
        {
            get => TimeSpan.FromMinutes(_settings.AutoSaveInterval);
            set
            {
                _settings.AutoSaveInterval = (int)value.TotalMinutes;
                _timer.Interval = value.TotalMilliseconds;
            }
        }

        public bool IsEnabled
        {
            get => _settings.AutoSave;
            set
            {
                _settings.AutoSave = value;
                if (value)
                {
                    _timer.Start();
                }
                else
                {
                    _timer.Stop();
                }
            }
        }

        public AutoSaveService(AppSettings settings)
        {
            _settings = settings;
            _timer = new Timer(settings.AutoSaveInterval * 60 * 1000);
            _timer.Elapsed += async (s, e) => await AutoSave();
            _timer.AutoReset = true;

            if (settings.AutoSave)
            {
                _timer.Start();
            }
        }

        public void SetCurrentProject(VideoProject? project)
        {
            _currentProject = project;
        }

        private async Task AutoSave()
        {
            if (!_settings.AutoSave || _currentProject == null) return;

            try
            {
                var backupPath = GetBackupPath(_currentProject);
                if (!string.IsNullOrEmpty(backupPath))
                {
                    var result = await ProjectService.SaveProject(_currentProject, backupPath);
                    if (result)
                    {
                        AutoSaved?.Invoke(backupPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"自動保存エラー: {ex.Message}");
            }
        }

        private string GetBackupPath(VideoProject project)
        {
            var directory = _settings.OutputDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var backupDir = Path.Combine(directory, "AutoBackups");

            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            var fileName = $"{project.Name.Replace(" ", "_")}_backup_{DateTime.Now:yyyyMMdd_HHmmss}.vcp";
            return Path.Combine(backupDir, fileName);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _timer?.Dispose();
                _disposed = true;
            }
        }
    }
}
