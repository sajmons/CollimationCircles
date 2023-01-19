using Avalonia.Controls;
using Avalonia.Input;
using CollimationCircles.ViewModels;

namespace CollimationCircles.Services;

public interface IMoveWindowService
{
    void HandleMovement(Window window, SettingsViewModel? vm, KeyEventArgs e);
    void HandleScale(SettingsViewModel? vm, KeyEventArgs e);
    void HandleRotation(SettingsViewModel? vm, KeyEventArgs e);
}
