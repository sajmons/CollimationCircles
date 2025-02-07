﻿using Avalonia.Media;
using CollimationCircles.Helper;
using CollimationCircles.Messages;
using CollimationCircles.Services;
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
        private string id = Guid.NewGuid().ToString();

        [JsonProperty]
        [ObservableProperty]
        private Color itemColor = Colors.Red;

        [JsonProperty]
        [ObservableProperty]
        private string? label;

        [JsonProperty]
        [ObservableProperty]
        [Range(Constraints.ThicknessMin, Constraints.ThicknessMax)]
        [NotifyDataErrorInfo]
        private int thickness = 1;

        [JsonProperty]
        [ObservableProperty]
        [Range(Constraints.RadiusMin, Constraints.RadiusMax)]
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
        [Range(Constraints.RotationAngleMin, Constraints.RotationAngleMax)]
        [NotifyDataErrorInfo]
        private double rotationAngle = 0;

        [JsonProperty]
        [ObservableProperty]
        [Range(Constraints.InclinationAngleMin, Constraints.InclinationAngleMax)]
        [NotifyDataErrorInfo]
        private double inclinationAngle = 0;

        [JsonProperty]
        [ObservableProperty]
        [Range(Constraints.SizeMin, Constraints.SizeMax)]
        [NotifyDataErrorInfo]
        private double size = 10;

        [JsonProperty]
        [ObservableProperty]
        [Range(Constraints.CountMin, Constraints.CountMax)]
        [NotifyDataErrorInfo]
        private int count = 4;

        [JsonProperty]
        [ObservableProperty]
        [Range(Constraints.CountMin, Constraints.CountMax)]
        [NotifyDataErrorInfo]
        private int maxCount = 10;

        [JsonProperty]
        [ObservableProperty]
        private bool isLabelVisible = true;

        [ObservableProperty]
        private string? invalidateGraphics;

        [JsonProperty]
        [ObservableProperty]
        [Range(Constraints.OpacityMin, Constraints.OpacityMax)]
        [NotifyDataErrorInfo]
        private double opacity = 1;

        internal readonly IResourceService ResSvc;

        public CollimationHelper()
        {
            ResSvc = Ioc.Default.GetRequiredService<IResourceService>();
        }

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

                return ResSvc.TryGet($"IconData.{dynRes}") ?? throw new Exception($"Resurce not found '{dynRes}'.");
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
                case nameof(IsLabelVisible):
                    if (!HasErrors)
                    {
                        base.OnPropertyChanged(e);

                        SettingsViewModel vm = Ioc.Default.GetRequiredService<SettingsViewModel>();

                        if (vm.SelectedItem?.Label is not null)
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
