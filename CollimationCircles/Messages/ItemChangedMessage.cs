using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CollimationCircles.Messages
{
    public class ItemChangedMessage : ValueChangedMessage<ItemViewModel>
    {
        public ItemChangedMessage(ItemViewModel settings) : base(settings)
        {
        }
    }
}
