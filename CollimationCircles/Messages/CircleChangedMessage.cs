using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CollimationCircles.Messages
{
    public class CircleChangedMessage : ValueChangedMessage<MarkViewModel>
    {
        public CircleChangedMessage(MarkViewModel settings) : base(settings)
        {
        }
    }
}
