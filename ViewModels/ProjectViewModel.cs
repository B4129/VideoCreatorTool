using System;
using System.Collections.ObjectModel;
using System.Linq;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.ViewModels
{
    public class ProjectViewModel : ViewModelBase
    {
        private readonly VideoProject _project;
        private string _projectName;
        private int _width;
        private int _height;
        private double _frameRate;
        private bool _isDirty;

        public ProjectViewModel(VideoProject project)
        {
            _project = project;
            _projectName = project.Name;
            _width = project.Width;
            _height = project.Height;
            _frameRate = project.FrameRate;
            _isDirty = false;
        }

        public string ProjectName
        {
            get => _projectName;
            set
            {
                if (SetProperty(ref _projectName, value))
                {
                    _project.Name = value;
                    MarkAsDirty();
                }
            }
        }

        public int Width
        {
            get => _width;
            set
            {
                if (SetProperty(ref _width, value))
                {
                    MarkAsDirty();
                }
            }
        }

        public int Height
        {
            get => _height;
            set
            {
                if (SetProperty(ref _height, value))
                {
                    MarkAsDirty();
                }
            }
        }

        public double FrameRate
        {
            get => _frameRate;
            set
            {
                if (SetProperty(ref _frameRate, value))
                {
                    MarkAsDirty();
                }
            }
        }

        /// <summary>
        /// プロジェクトに変更があるかどうか
        /// </summary>
        public bool IsDirty
        {
            get => _isDirty;
            set => SetProperty(ref _isDirty, value);
        }

        /// <summary>
        /// 変更ありフラグを設定
        /// </summary>
        public void MarkAsDirty()
        {
            IsDirty = true;
            _project.ModifiedAt = DateTime.Now;
        }

        /// <summary>
        /// 変更なしフラグにリセット
        /// </summary>
        public void MarkAsClean()
        {
            IsDirty = false;
        }

        public ObservableCollection<Character> Characters => _project.Characters;
        public ObservableCollection<TimelineTrack> Tracks => _project.Tracks;

        public void AddCharacter(Character character)
        {
            Characters.Add(character);
            MarkAsDirty();
        }

        public void RemoveCharacter(Guid characterId)
        {
            var character = Characters.ToList().FirstOrDefault(c => c.Id == characterId);
            if (character != null)
            {
                Characters.Remove(character);
                MarkAsDirty();
            }
        }
    }
}
