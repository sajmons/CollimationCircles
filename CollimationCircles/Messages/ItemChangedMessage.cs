using CollimationCircles.Models;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CollimationCircles.Messages
{
    public class ItemChangedMessage : ValueChangedMessage<ICollimationHelper>
    {
        public ItemChangedMessage(ICollimationHelper settings) : base(settings)
        {
        }
    }
}
