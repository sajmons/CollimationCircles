using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.ComponentModel;

namespace CollimationCircles.ViewModels
{
    public partial class CircleViewModel : CollimationHelper
    {
        public CircleViewModel()
        { 
            Color = ItemColor.Red;
            Label = Text.Circle;
        }
    }
}
