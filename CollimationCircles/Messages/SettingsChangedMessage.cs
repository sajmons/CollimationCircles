using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CollimationCircles.Messages
{
    public class SettingsChangedMessage(SettingsViewModel settings) : ValueChangedMessage<SettingsViewModel>(settings)
    {
    }
}
