using CollimationCircles.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace CollimationCircles.ViewModels
{
    public partial class StreamViewModel
    {
        private readonly Services.Alpaca.AlpacaFocuserClient alpacaFocuserClient = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanControlFocuser))]
        [NotifyPropertyChangedFor(nameof(CanMoveFocuser))]
        private ObservableCollection<Focuser> focusers = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanControlFocuser))]
        [NotifyPropertyChangedFor(nameof(CanMoveFocuser))]
        private Focuser? selectedFocuser;

        [ObservableProperty]
        private ObservableCollection<int> focuserSteps = [1, 10, 100, 1000];

        [ObservableProperty]
        private int selectedFocuserStep = 10;

        [ObservableProperty]
        private bool invertFocuserDirection;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FocuserPositionText))]
        private int? focuserPosition;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanControlFocuser))]
        [NotifyPropertyChangedFor(nameof(CanMoveFocuser))]
        private bool isFocuserBusy;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanMoveFocuser))]
        private bool isFocuserMoving;

        public string FocuserPositionText => FocuserPosition?.ToString() ?? "-";

        public bool CanControlFocuser =>
            IsServerConnected
            && !IsServerBusy
            && !IsFocuserBusy
            && SelectedFocuser is not null
            && Focusers.Count > 0;

        public bool CanMoveFocuser => CanControlFocuser && !IsFocuserMoving;

        partial void OnSelectedFocuserChanged(Focuser? value)
        {
            FocuserPosition = null;

            if (value is null)
            {
                return;
            }

            _ = SelectFocuserAsync(value);
        }

        private async Task SelectFocuserAsync(Focuser focuser)
        {
            IsFocuserBusy = true;
            try
            {
                FocuserPosition = await alpacaFocuserClient.SelectFocuserAsync(focuser);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to select focuser");
            }
            finally
            {
                IsFocuserBusy = false;
            }
        }

        [RelayCommand]
        private Task MoveFocuserIn() => MoveFocuserAsync(-1);

        [RelayCommand]
        private Task MoveFocuserOut() => MoveFocuserAsync(1);

        private async Task MoveFocuserAsync(int direction)
        {
            if (!CanMoveFocuser)
            {
                return;
            }

            var steps = direction * SelectedFocuserStep * (InvertFocuserDirection ? -1 : 1);
            IsFocuserMoving = true;
            try
            {
                FocuserPosition = await alpacaFocuserClient.MoveByAsync(steps);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to move focuser by {steps} steps.");
            }
            finally
            {
                IsFocuserMoving = false;
            }
        }

        [RelayCommand]
        private async Task HaltFocuser()
        {
            try
            {
                await alpacaFocuserClient.HaltAsync();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to halt focuser.");
            }
        }

        private async Task RefreshFocusersAsync(string address, int port)
        {
            try
            {
                var found = await Task.Run(() => alpacaFocuserClient.DiscoverFocusersAsync(address, port)).WaitAsync(TimeSpan.FromSeconds(10));
                Focusers = new ObservableCollection<Focuser>(found);
                SelectedFocuser = Focusers.FirstOrDefault();
            }
            catch (TimeoutException ex)
            {
                logger.Warn(ex, $"Focuser discovery on Alpaca server {address}:{port} timed out");
                ResetFocuserState();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to discover focusers");
                ResetFocuserState();
            }
        }

        private void ResetFocuserState()
        {
            Focusers = [];
            SelectedFocuser = null;
            FocuserPosition = null;
            IsFocuserMoving = false;
            alpacaFocuserClient.ClearSelectedFocuser();
        }
    }
}
