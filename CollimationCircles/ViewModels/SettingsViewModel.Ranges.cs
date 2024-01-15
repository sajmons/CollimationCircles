using CollimationCircles.Helper;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollimationCircles.ViewModels
{
    public partial class SettingsViewModel
    {
        [ObservableProperty]
        private int thicknessMin = Ranges.ThicknessMin;

        [ObservableProperty]
        private int thicknessMax = Ranges.ThicknessMax;

        [ObservableProperty]
        private int radiusMin = Ranges.RadiusMin;

        [ObservableProperty]
        private int radiusMax = Ranges.RadiusMax;

        [ObservableProperty]
        private int rotationAngleMin = Ranges.RotationAngleMin;

        [ObservableProperty]
        private int rotationAngleMax = Ranges.RotationAngleMax;

        [ObservableProperty]
        private int inclinationAngleMin = Ranges.InclinationAngleMin;

        [ObservableProperty]
        private int inclinationAngleMax = Ranges.InclinationAngleMax;

        [ObservableProperty]
        private int spacingMin = Ranges.SpacingMin;

        [ObservableProperty]
        private int spacingMax = Ranges.SpacingMax;

        [ObservableProperty]
        private int countMin = Ranges.CountMin;

        [ObservableProperty]
        private int countMax = Ranges.CountMax;

        [ObservableProperty]
        private double opacityMin = Ranges.OpacityMin;

        [ObservableProperty]
        private double opacityMax = Ranges.OpacityMax;

        [ObservableProperty]
        private double scaleMin = Ranges.ScaleMin;

        [ObservableProperty]
        private double scaleMax = Ranges.ScaleMax;

        [ObservableProperty]
        private int offsetMin = Ranges.OffsetMin;

        [ObservableProperty]
        private int offsetMax = Ranges.OffsetMax;

        [ObservableProperty]
        private int labelSizeMin = Ranges.LabelSizeMin;

        [ObservableProperty]
        private int labelSizeMax = Ranges.LabelSizeMax;        
    }
}
