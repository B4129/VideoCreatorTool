using System;
using System.Windows.Input;
using System.Windows.Media;
using VideoCreatorWPF.Commands;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.ViewModels
{
    public class CharacterViewModel : ViewModelBase
    {
        private readonly Character _character;
        private string _name;
        private int _speakerId;
        private string? _imagePath;
        private string _color;
        private double _volume;
        private double _speed;
        private double _pitch;
        private bool _isMuted;

        public CharacterViewModel(Character character)
        {
            _character = character;
            _name = character.Name;
            _speakerId = character.SpeakerId;
            _imagePath = character.ImagePath;
            _color = character.Color ?? "#3b82f6";
            _volume = character.Volume;
            _speed = character.Speed;
            _pitch = character.Pitch;
            _isMuted = character.IsMuted;

            DeleteCommand = new RelayCommand(_ => OnDelete?.Invoke(this));
        }

        public Guid Id => _character.Id;

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    _character.Name = value;
                }
            }
        }

        public int SpeakerId
        {
            get => _speakerId;
            set => SetProperty(ref _speakerId, value);
        }

        public string? ImagePath
        {
            get => _imagePath;
            set => SetProperty(ref _imagePath, value);
        }

        public string Color
        {
            get => _color;
            set
            {
                if (SetProperty(ref _color, value))
                {
                    OnPropertyChanged(nameof(ColorBrush));
                }
            }
        }

        public SolidColorBrush ColorBrush => new SolidColorBrush((Color)ColorConverter.ConvertFromString(Color));

        public double Volume
        {
            get => _volume;
            set => SetProperty(ref _volume, value);
        }

        public double Speed
        {
            get => _speed;
            set => SetProperty(ref _speed, value);
        }

        public double Pitch
        {
            get => _pitch;
            set => SetProperty(ref _pitch, value);
        }

        public bool IsMuted
        {
            get => _isMuted;
            set => SetProperty(ref _isMuted, value);
        }

        public ICommand DeleteCommand { get; }
        public event Action<CharacterViewModel>? OnDelete;
    }
}
