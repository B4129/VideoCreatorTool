using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using VideoCreatorWPF.Commands;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.ViewModels
{
    public class CharacterListViewModel : ViewModelBase
    {
        private readonly ProjectViewModel _project;
        private ObservableCollection<CharacterViewModel> _characters;

        public CharacterListViewModel(ProjectViewModel project)
        {
            _project = project;
            _characters = new ObservableCollection<CharacterViewModel>(
                _project.Characters.Select(c => new CharacterViewModel(c)));

            AddCharacterCommand = new RelayCommand(_ => AddCharacter());
        }

        public ObservableCollection<CharacterViewModel> Characters
        {
            get => _characters;
            set => SetProperty(ref _characters, value);
        }

        public ICommand AddCharacterCommand { get; }

        private void AddCharacter()
        {
            var character = new Character
            {
                Name = $"キャラクター{_characters.Count + 1}",
                SpeakerId = 2,
                Color = GetRandomColor()
            };

            var viewModel = new CharacterViewModel(character);
            viewModel.OnDelete += (vm) => DeleteCharacter(vm);

            _project.AddCharacter(character);
            Characters.Add(viewModel);
        }

        private void DeleteCharacter(CharacterViewModel viewModel)
        {
            viewModel.OnDelete -= (vm) => DeleteCharacter(vm);
            _project.RemoveCharacter(viewModel.Id);
            Characters.Remove(viewModel);
        }

        private string GetRandomColor()
        {
            var colors = new[] { "#3b82f6", "#ef4444", "#10b981", "#f59e0b", "#8b5cf6", "#ec4899" };
            var random = new Random();
            return colors[random.Next(colors.Length)];
        }
    }
}
