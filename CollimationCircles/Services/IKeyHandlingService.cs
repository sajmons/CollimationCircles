using Avalonia.Controls;
using Avalonia.Input;
using CollimationCircles.ViewModels;

namespace CollimationCircles.Services;

public interface IKeyHandlingService
{    
    void HandleMovement(Window window, SettingsViewModel? vm, KeyEventArgs e);
    void HandleGlobalRotation(SettingsViewModel? vm, KeyEventArgs e);
    void HandleGlobalScale(SettingsViewModel? vm, KeyEventArgs e);    
    void HandleHelperRadius(SettingsViewModel? vm, KeyEventArgs e);    
    void HandleHelperRotation(SettingsViewModel? vm, KeyEventArgs e);
    void HandleHelperCount(SettingsViewModel? vm, KeyEventArgs e);
    void HandleHelperSpacing(SettingsViewModel? vm, KeyEventArgs e);
    void HandleHelperThickness(SettingsViewModel? vm, KeyEventArgs e);
}
