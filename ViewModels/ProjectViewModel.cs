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

        public ProjectViewModel(VideoProject project)
        {
            _project = project;
            _projectName = project.Name;
            _width = project.Width;
            _height = project.Height;
            _frameRate = project.FrameRate;
        }

        public string ProjectName
        {
            get => _projectName;
            set
            {
                if (SetProperty(ref _projectName, value))
                {
                    _project.Name = value;
                    _project.ModifiedAt = DateTime.Now;
                }
            }
        }

        public int Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        public int Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        public double FrameRate
        {
            get => _frameRate;
            set => SetProperty(ref _frameRate, value);
        }

        public ObservableCollection<Character> Characters => _project.Characters;
        public ObservableCollection<TimelineTrack> Tracks => _project.Tracks;

        public void AddCharacter(Character character)
        {
            Characters.Add(character);
        }

        public void RemoveCharacter(Guid characterId)
        {
            var character = Characters.ToList().FirstOrDefault(c => c.Id == characterId);
            if (character != null)
            {
                Characters.Remove(character);
            }
        }
    }
}
