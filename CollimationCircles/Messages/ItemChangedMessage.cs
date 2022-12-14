using CollimationCircles.Models;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CollimationCircles.Messages
{
    public class ItemChangedMessage : ValueChangedMessage<CollimationHelper>
    {
        public ItemChangedMessage(CollimationHelper settings) : base(settings)
        {
        }
    }
}
