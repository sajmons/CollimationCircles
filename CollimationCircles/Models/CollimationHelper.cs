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
using System.ComponentModel.DataAnnotations;
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
        private string? label;

        [JsonProperty]
        [ObservableProperty]
        [Range(1, 10)]
        [NotifyDataErrorInfo]
        private int thickness = 1;

        [JsonProperty]
        [ObservableProperty]
        [Range(1, 2000)]
        [NotifyDataErrorInfo]
        private double radius = 300;

        [JsonProperty]
        [ObservableProperty]
        private double rotationIncrement = 1;

        [JsonProperty]
        [ObservableProperty]
        private double inclinationIncrement = 0.1;

        [JsonProperty]
        [ObservableProperty]
        private bool isVisible = true;

        [JsonProperty]
        [ObservableProperty]
        private bool isRotatable = false;

        [JsonProperty]
        [ObservableProperty]
        private bool isInclinatable = false;

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
        [Range(-180, 180)]
        [NotifyDataErrorInfo]
        private double rotationAngle = 0;

        [JsonProperty]
        [ObservableProperty]
        [Range(-90, 90)]
        [NotifyDataErrorInfo]
        private double inclinationAngle = 0;

        [JsonProperty]
        [ObservableProperty]
        [Range(1, 100)]
        [NotifyDataErrorInfo]
        private double size = 10;

        [JsonProperty]
        [ObservableProperty]
        [Range(1, 10)]
        [NotifyDataErrorInfo]
        private int count = 4;

        public Bitmap? Image
        {
            get
            {
                string path = "Resources/Images/";

                if (this is CircleViewModel)
                    path += nameof(CircleViewModel).ToLower();
                else if (this is PrimaryClipViewModel)
                    path += nameof(PrimaryClipViewModel).ToLower();
                else if (this is ScrewViewModel)
                    path += nameof(ScrewViewModel).ToLower();
                else if (this is SpiderViewModel)
                    path += nameof(SpiderViewModel).ToLower();
                else if (this is BahtinovMaskViewModel)
                    path += nameof(BahtinovMaskViewModel).ToLower();
                else
                    path += string.Empty;

                string assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;

                var uri = new Uri($"avares://{assemblyName}/{path}.png");

                var asset = AssetLoader.Open(uri);

                if (asset is null)
                    return null;
                else
                    return new Bitmap(asset);
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (!HasErrors)
            {
                base.OnPropertyChanged(e);
                WeakReferenceMessenger.Default.Send(new ItemChangedMessage(this));
            }
        }
    }
}
