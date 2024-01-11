using Avalonia.Media;
using CollimationCircles.Helper;
using CollimationCircles.Messages;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace CollimationCircles.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class CollimationHelper : ObservableValidator, ICollimationHelper
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

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

        [JsonProperty]
        [ObservableProperty]
        [Range(1, 10)]
        [NotifyDataErrorInfo]
        private int maxCount = 10;

        [ObservableProperty]
        private string? invalidateGraphics;

        [JsonProperty]
        [ObservableProperty]
        [Range(0.1, 1)]
        [NotifyDataErrorInfo]
        private double opacity = 1;

        public string ResourceString
        {
            get
            {
                string dynRes = string.Empty;

                if (this is CircleViewModel)
                    dynRes = "Circle";
                else if (this is PrimaryClipViewModel)
                    dynRes = "PrimaryClip";
                else if (this is ScrewViewModel)
                    dynRes = "Screw";
                else if (this is SpiderViewModel)
                    dynRes = "Spider";
                else if (this is BahtinovMaskViewModel)
                    dynRes = "BahtinovMask";
                else
                    dynRes += string.Empty;

                return DynRes.TryGet($"IconData.{dynRes}");
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ItemColor):
                case nameof(Label):
                case nameof(Radius):
                case nameof(RotationAngle):
                case nameof(InclinationAngle):
                case nameof(Size):
                case nameof(Count):
                case nameof(Thickness):
                case nameof(IsVisible):
                case nameof(Opacity):
                    if (!HasErrors)
                    {
                        base.OnPropertyChanged(e);

                        SettingsViewModel? vm = Ioc.Default.GetService<SettingsViewModel>();

                        if (vm is not null && vm?.SelectedItem?.Label is not null)
                        {
                            WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(vm));

                            var pVal = Property.GetPropValue(this, e.PropertyName);

                            logger.Debug($"Shape '{vm.SelectedItem.Label}' property '{e.PropertyName}' changed to '{pVal}'");
                        }                        
                    }
                    break;
            }
        }        
    }
}
