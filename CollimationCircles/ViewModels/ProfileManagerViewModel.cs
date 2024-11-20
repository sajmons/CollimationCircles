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
using CollimationCircles.Services;
using CollimationCirclesFeatures;
using System.Threading.Tasks;

namespace CollimationCircles.ViewModels
{
    public partial class ProfileManagerViewModel : BaseViewModel
    {
        private readonly SettingsViewModel settingsViewModel;
        private readonly ILicenseService licenseService;

        public ProfileManagerViewModel()
        {
            settingsViewModel = Ioc.Default.GetRequiredService<SettingsViewModel>();
            licenseService = Ioc.Default.GetRequiredService<ILicenseService>();
            ProfileName = ResSvc.TryGetString("DefaultProfileName");
        }

        [ObservableProperty]
        private ObservableCollection<Profile> profiles = [];

        [ObservableProperty]
        private Profile? selectedProfile;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddCurrentProfileCommand))]
        private string profileName;

        public bool CanExecuteAddCurrentProfile
        {
            get => !string.IsNullOrWhiteSpace(ProfileName);
        }

        [RelayCommand(CanExecute = nameof(CanExecuteAddCurrentProfile))]
        internal async Task AddCurrentProfile()
        {
            await CheckFeatureLicensed(FeatureList.ProfileManager, () =>
            {
                Profiles.Add(new Profile(ProfileName, settingsViewModel.Items));
            });
        }

        [RelayCommand]
        internal void RemoveProfile(Profile profile)
        {
            Profiles.Remove(profile);
        }

        partial void OnSelectedProfileChanged(Profile? oldValue, Profile? newValue)
        {
            if (newValue != null)
            {
                settingsViewModel.Items.Clear();
                settingsViewModel.Items.AddRange(newValue.Shapes);

                ProfileName = newValue.Name;

                WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(settingsViewModel));
            }
        }

        partial void OnProfileNameChanged(string? oldValue, string newValue)
        {
            if (SelectedProfile != null)
            {
                SelectedProfile.Name = newValue;
            }
        }
    }
}