using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Models
{
    public partial class CameraControl(ControlType controlName, Camera camera) : ObservableObject, ICameraControl
    {
        private readonly ICameraControlService cameraControlService = Ioc.Default.GetRequiredService<ICameraControlService>();
        public ControlType Name { get; set; } = controlName;
        public int Min { get; set; }
        public int Max { get; set; }
        public double Step { get; set; } = 0.1;
        public int Default { get; set; }
        public ControlValueType ValueType { get; set; }
        public bool AutoSupported { get; set; }

        [ObservableProperty]
        private int value;

        [ObservableProperty]
        private bool isAuto;

        public string Flags { get; set; } = string.Empty;

        private readonly Camera _camera = camera;

        partial void OnValueChanged(int oldValue, int newValue)
        {
            if (_camera is not null)
            {
                cameraControlService.Set(Name, newValue, _camera);
            }
        }

        partial void OnIsAutoChanged(bool oldValue, bool newValue)
        {
            if (_camera is not null && AutoSupported)
            {
                cameraControlService.SetAuto(Name, newValue, _camera);
            }
        }

        public void SetDefault()
        {
            Value = Default;
        }
    }
}
