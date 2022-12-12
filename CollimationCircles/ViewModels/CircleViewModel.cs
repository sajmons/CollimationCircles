using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Resources.Strings;
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
        public string color = ItemColor.Red;        
        [ObservableProperty]
        public string label = Text.Circle;
        [ObservableProperty]
        public int thickness = 1;
        [ObservableProperty]
        public double radius = 250;
        [ObservableProperty]
        public bool isVisible = true;
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
