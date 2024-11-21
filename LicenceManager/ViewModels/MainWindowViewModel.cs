using CollimationCirclesFeatures;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using Standard.Licensing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LicenceManager.ViewModels
{
    partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateKeysCommand))]
        private string? passPhrase;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateLicenceCommand))]
        private string? privateKey;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateLicenceCommand))]
        private string? publicKey;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateLicenceCommand))]
        private string clientId = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateLicenceCommand))]
        private string product = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateLicenceCommand))]
        private string majorProductVersion = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateLicenceCommand))]
        private string? licensedTo;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateLicenceCommand))]
        private string? licensedToEmail;

        [ObservableProperty]
        private bool trialLicence = true;

        [ObservableProperty]
        private int trialLicenceDaysUntilExpiration = 5;

        private readonly IDialogService dialogService;

        [ObservableProperty]
        private License? newLicense;

        public MainWindowViewModel()
        {
            this.dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        }

        public bool CanExecuteCreateKeys
        {
            get => !string.IsNullOrWhiteSpace(PassPhrase);
        }

        public bool CanExecuteCreateLicence
        {
            get => !string.IsNullOrWhiteSpace(PrivateKey)
                && !string.IsNullOrWhiteSpace(PublicKey)
                && !string.IsNullOrWhiteSpace(ClientId)
                && !string.IsNullOrWhiteSpace(LicensedTo)
                && !string.IsNullOrWhiteSpace(LicensedToEmail);
        }

        [RelayCommand(CanExecute = nameof(CanExecuteCreateLicence))]
        internal async Task CreateLicence()
        {
            var featureList = new FeatureList().ToDictionary();

            ILicenseBuilder licenseBuilder = License.New()
                .WithUniqueIdentifier(Guid.NewGuid())
                .As(TrialLicence ? LicenseType.Trial : LicenseType.Standard);

            if (TrialLicence)
            {
                licenseBuilder.ExpiresAt(DateTime.Now.AddDays(TrialLicenceDaysUntilExpiration));
            }

            NewLicense = licenseBuilder
                .WithAdditionalAttributes(new Dictionary<string, string>
                    {
                        {"ClientId", ClientId },
                        {"Product", Product },
                        {"MajorProductVersion", MajorProductVersion },
                        {"DatePublished", DateTime.UtcNow.ToString("o") },
                    })
                .WithProductFeatures(featureList)
                .LicensedTo(LicensedTo, LicensedToEmail)
                .CreateAndSignWithPrivateKey(PrivateKey, PassPhrase);

            // save licence file
            var settings = new SaveFileDialogSettings
            {
                Title = "SaveFile",
                Filters =
                [
                    new("License", "lic")
                ],
                DefaultExtension = "*.lic"
            };

            var path = await dialogService.ShowSaveFileDialogAsync(this, settings);

            if (!string.IsNullOrWhiteSpace(path?.Path?.LocalPath))
            {
                File.WriteAllText($"{path?.Path?.LocalPath}", NewLicense.ToString(), Encoding.UTF8);
            }
        }

        [RelayCommand(CanExecute = nameof(CanExecuteCreateKeys))]
        internal void CreateKeys()
        {
            var keyGenerator = Standard.Licensing.Security.Cryptography.KeyGenerator.Create();
            var keyPair = keyGenerator.GenerateKeyPair();
            PrivateKey = keyPair.ToEncryptedPrivateKeyString(PassPhrase);
            PublicKey = keyPair.ToPublicKeyString();
        }
    }
}
