using CollimationCircles.Messages;
using CollimationCircles.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.ComponentModel;

namespace CollimationCircles.ViewModels
{
    public partial class ScrewViewModel : ObservableValidator, IScrew
    {
        [ObservableProperty]
        public double rotation;
        [ObservableProperty]
        public double size = 10;
        [ObservableProperty]
        public Guid id = Guid.NewGuid();
        [ObservableProperty]
        public string color = ItemColor.Lime;        
        [ObservableProperty]
        public string label = "Screw";
        [ObservableProperty]
        public int thickness = 1;
        [ObservableProperty]
        public double radius = 300;
        [ObservableProperty]
        public bool visibility = true;
        [ObservableProperty]
        public bool isRotatable = true;
        [ObservableProperty]
        public bool isSizeable = true;
        [ObservableProperty]
        public bool isEditable = false;

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new ItemChangedMessage(this));
        }
    }
}
