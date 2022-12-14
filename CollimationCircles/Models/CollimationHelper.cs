using CollimationCircles.Messages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace CollimationCircles.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class CollimationHelper : ObservableValidator, ICollimationHelper
    {
        [JsonProperty]
        [ObservableProperty]
        private Guid id = Guid.NewGuid();

        [JsonProperty]
        [ObservableProperty]
        private string color = ItemColor.Red;

        [JsonProperty]
        [ObservableProperty]
        private string label = "Base Helper";

        [JsonProperty]
        [ObservableProperty]
        private int thickness = 1;

        [JsonProperty]
        [ObservableProperty]
        private double radius = 300;

        [JsonProperty]
        [ObservableProperty]
        private bool isVisible = true;

        [JsonProperty]
        [ObservableProperty]
        private bool isRotatable = false;

        [JsonProperty]
        [ObservableProperty]
        private bool isSizeable = false;

        [JsonProperty]
        [ObservableProperty]
        private bool isEditable = true;

        [JsonProperty]
        [ObservableProperty]
        private bool isCountable = false;

        [JsonProperty]
        [ObservableProperty]
        private double rotation = 0;

        [JsonProperty]
        [ObservableProperty]
        private double size = 10;

        [JsonProperty]
        [ObservableProperty]
        private int count = 4;

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new ItemChangedMessage(this));
        }
    }
}
