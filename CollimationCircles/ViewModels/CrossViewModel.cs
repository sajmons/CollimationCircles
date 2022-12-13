using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.ComponentModel;

namespace CollimationCircles.ViewModels
{
    public partial class CrossViewModel : ObservableValidator, ICross
    {
        [ObservableProperty]
        public double rotation = 45;
        [ObservableProperty]
        public double size = 4;
        [ObservableProperty]
        public Guid id = Guid.NewGuid();
        [ObservableProperty]
        public string color = ItemColor.Red;        
        [ObservableProperty]
        public string label = Text.Spider;
        [ObservableProperty]
        public int thickness = 1;
        [ObservableProperty]
        public double radius = 280;
        [ObservableProperty]
        public bool isVisible = true;
        [ObservableProperty]
        public bool isRotatable = true;
        [ObservableProperty]
        public bool isSizeable = true;
        [ObservableProperty]
        public bool isEditable = true;
        [ObservableProperty]
        public bool isCountable = false;

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new ItemChangedMessage(this));
        }
    }
}
