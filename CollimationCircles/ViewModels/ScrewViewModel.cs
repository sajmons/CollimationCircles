﻿using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Resources.Strings;
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
        public string label = Text.PrimaryScrew;
        [ObservableProperty]
        public int thickness = 1;
        [ObservableProperty]
        public double radius = 270;
        [ObservableProperty]
        public bool isVisible = true;
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
