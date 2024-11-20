using CollimationCircles.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CollimationCircles.Extensions;
using CollimationCircles.Messages;
using CommunityToolkit.Mvvm.Messaging;

namespace CollimationCircles.ViewModels
{
    public partial class ProfileManagerViewModel : BaseViewModel
    {
        private readonly SettingsViewModel _settingsViewModel;

        public ProfileManagerViewModel()
        {
            _settingsViewModel = Ioc.Default.GetRequiredService<SettingsViewModel>();
            SelectedProfile = new() { Name = ResSvc.TryGetString("DefaultProfileName"), ScopeShapes = [] };
        }

        [ObservableProperty]
        private ObservableCollection<Profile> profiles = [];

        [ObservableProperty]
        private Profile selectedProfile;

        [RelayCommand]
        internal void AddCurrentProfile()
        {
            Profiles.Add(new Profile { Name = SelectedProfile.Name, ScopeShapes = _settingsViewModel.Items.ToList() });
        }

        [RelayCommand]
        internal void RemoveProfile(Profile profile)
        {
            Profiles.Remove(profile);
        }

        partial void OnSelectedProfileChanged(Profile? oldValue, Profile newValue)
        {
            if (newValue != null)
            {
                _settingsViewModel.Items.Clear();
                _settingsViewModel.Items.AddRange(newValue.ScopeShapes!);

                WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(_settingsViewModel));
            }
        }
    }
}