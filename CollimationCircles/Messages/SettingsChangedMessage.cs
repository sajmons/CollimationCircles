using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CollimationCircles.Messages
{
    public class SettingsChangedMessage : ValueChangedMessage<SettingsViewModel>
    {
        public SettingsChangedMessage(SettingsViewModel settings) : base(settings)
        {
        }
    }
}
