using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Resources.Strings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.ComponentModel;

namespace CollimationCircles.ViewModels
{
    public partial class CrossViewModel : CollimationHelper
    {
        public CrossViewModel()
        {
            Rotation = 45;
            Size = 4;
            Color= ItemColor.Red;
            Label = Text.Spider;
            Radius= 200;
            IsRotatable= true;
            IsSizeable= true;
        }
    }
}
