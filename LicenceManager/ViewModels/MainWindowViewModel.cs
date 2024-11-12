using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Standard.Licensing;
using System.Collections.Generic;
using System;

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
        private string? clientId;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateLicenceCommand))]
        private string? licensedTo;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateLicenceCommand))]
        private string? licensedToEmail;

        [ObservableProperty]
        private bool trialLicence = true;

        [ObservableProperty]
        private int trialLicenceDaysUntilExpiration = 30;

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
        internal void CreateLicence()
        {
            var license = License.New()
                .WithUniqueIdentifier(Guid.NewGuid())
                .As(TrialLicence ? LicenseType.Trial : LicenseType.Standard)
                .ExpiresAt(TrialLicence ? DateTime.Now.AddDays(TrialLicenceDaysUntilExpiration) : DateTime.Now.AddYears(100))
                .WithProductFeatures(new Dictionary<string, string>
                    {
                        {"Camera Video Stream", "yes"},
                        {"Shapes Manager", "yes"}
                    })
                .LicensedTo(LicensedTo, LicensedToEmail)
                .CreateAndSignWithPrivateKey(PrivateKey, PassPhrase);
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
