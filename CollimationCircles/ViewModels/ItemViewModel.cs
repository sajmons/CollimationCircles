using CollimationCircles.Messages;
using CollimationCircles.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace CollimationCircles.ViewModels
{
    public partial class ItemViewModel : BaseViewModel
    {
        [ObservableProperty]
        public Guid id = Guid.NewGuid();

        [ObservableProperty]
        public string color = ItemColor.Orange;

        [ObservableProperty]
        [Range(1, 10)]
        public int thickness = 1;

        [ObservableProperty]
        [Range(1, 2000)]
        public double radius = 10;

        [ObservableProperty]
        [Range(1, 30)]
        public double spacing = 10;

        [ObservableProperty]
        [Range(0, 90)]
        public double rotation = 45;

        [ObservableProperty]
        public string type = ItemType.Circle;

        [ObservableProperty]
        public bool isCross;

        [ObservableProperty]
        public string label = string.Empty;

        [ObservableProperty]
        public string toolTip = string.Empty;

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new ItemChangedMessage(this));
        }

        partial void OnTypeChanged(string value)
        {
            IsCross = string.Equals(value, ItemType.Cross);
        }
    }
}
