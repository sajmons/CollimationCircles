using CollimationCircles.Messages;
using CollimationCircles.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.ComponentModel;

namespace CollimationCircles.ViewModels
{
    public partial class CircleViewModel : ObservableValidator, ICircle
    {
        [ObservableProperty]
        public Guid id = Guid.NewGuid();
        [ObservableProperty]
        public string color = ItemColor.Tomato;        
        [ObservableProperty]
        public string label = "Circle";
        [ObservableProperty]
        public int thickness = 1;
        [ObservableProperty]
        public double radius = 300;
        [ObservableProperty]
        public bool visibility = true;
        [ObservableProperty]
        public bool isRotatable = false;
        [ObservableProperty]
        public bool isSizeable = false;
        [ObservableProperty]
        public bool isEditable = true;

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new ItemChangedMessage(this));
        }
    }
}
