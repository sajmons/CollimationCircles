using CollimationCircles.Helper;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollimationCircles.ViewModels
{
    public partial class SettingsViewModel
    {
        [ObservableProperty]
        private int thicknessMin = Constraints.ThicknessMin;

        [ObservableProperty]
        private int thicknessMax = Constraints.ThicknessMax;

        [ObservableProperty]
        private int radiusMin = Constraints.RadiusMin;

        [ObservableProperty]
        private int radiusMax = Constraints.RadiusMax;

        [ObservableProperty]
        private int rotationAngleMin = Constraints.RotationAngleMin;

        [ObservableProperty]
        private int rotationAngleMax = Constraints.RotationAngleMax;

        [ObservableProperty]
        private int inclinationAngleMin = Constraints.InclinationAngleMin;

        [ObservableProperty]
        private int inclinationAngleMax = Constraints.InclinationAngleMax;

        [ObservableProperty]
        private int spacingMin = Constraints.SpacingMin;

        [ObservableProperty]
        private int spacingMax = Constraints.SpacingMax;

        [ObservableProperty]
        private int countMin = Constraints.CountMin;

        [ObservableProperty]
        private int countMax = Constraints.CountMax;

        [ObservableProperty]
        private double opacityMin = Constraints.OpacityMin;

        [ObservableProperty]
        private double opacityMax = Constraints.OpacityMax;

        [ObservableProperty]
        private double scaleMin = Constraints.ScaleMin;

        [ObservableProperty]
        private double scaleMax = Constraints.ScaleMax;

        [ObservableProperty]
        private int offsetMin = Constraints.OffsetMin;

        [ObservableProperty]
        private int offsetMax = Constraints.OffsetMax;

        [ObservableProperty]
        private int labelSizeMin = Constraints.LabelSizeMin;

        [ObservableProperty]
        private int labelSizeMax = Constraints.LabelSizeMax;
    }
}
