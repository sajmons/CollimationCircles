using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CollimationCircles.Messages;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Reflection;

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
        private Color itemColor = Colors.Red;

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
        private double rotationAngle = 0;

        [JsonProperty]
        [ObservableProperty]
        private double size = 10;

        [JsonProperty]
        [ObservableProperty]
        private int count = 4;
        public Bitmap? Image
        {
            get
            {
                string path = "Resources/Images/";

                if (this is CircleViewModel)
                    path += "circle";
                else if (this is CrossViewModel)
                    path += "cross";
                else if (this is PrimaryClipViewModel)
                    path += "clip";
                else if (this is ScrewViewModel)
                    path += "screw";
                else if (this is SpiderViewModel)
                    path += "spider";
                else
                    path += string.Empty;

                string assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;

                var uri = new Uri($"avares://{assemblyName}/{path}.png");

                var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                var asset = assets?.Open(uri);

                if (asset is null)
                    return null;
                else
                    return new Bitmap(asset);
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new ItemChangedMessage(this));
        }
    }
}
