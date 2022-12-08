using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CollimationCircles.Messages
{
    public class SettingsChangedMessage : ValueChangedMessage<MainViewModel>
    {
        public SettingsChangedMessage(MainViewModel settings) : base(settings)
        {
        }
    }
}
