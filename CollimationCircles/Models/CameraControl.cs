using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Models
{
    public partial class CameraControl(ControlType controlName, Camera camera) : ObservableObject, ICameraControl
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly ICameraControlService cameraControlService = Ioc.Default.GetRequiredService<ICameraControlService>();
        private bool suppressCameraDispatch;
        public ControlType Name { get; set; } = controlName;
        public double Min { get; set; }
        public double Max { get; set; }
        public double Step { get; set; } = 0.1;
        public double Default { get; set; }
        public ControlValueType ValueType { get; set; }
        public bool AutoSupported { get; set; }
        public bool IsModeOnly { get; set; }

        [ObservableProperty]
        private double value;

        [ObservableProperty]
        private bool isAuto;

        public string Flags { get; set; } = string.Empty;

        private readonly Camera _camera = camera;

        internal void ApplyDiscoveredState(double min, double max, double step, double @default, double currentValue, bool autoSupported, bool currentAuto, string flags, ControlValueType valueType)
        {
            suppressCameraDispatch = true;
            try
            {
                Min = min;
                Max = max;
                Step = step;
                Default = @default;
                AutoSupported = autoSupported;
                Flags = flags;
                ValueType = valueType;
                Value = currentValue;
                IsAuto = currentAuto;

                OnPropertyChanged(nameof(Min));
                OnPropertyChanged(nameof(Max));
                OnPropertyChanged(nameof(Step));
                OnPropertyChanged(nameof(Default));
                OnPropertyChanged(nameof(AutoSupported));
                OnPropertyChanged(nameof(Flags));
                OnPropertyChanged(nameof(ValueType));
            }
            finally
            {
                suppressCameraDispatch = false;
            }
        }

        partial void OnValueChanged(double oldValue, double newValue)
        {
            if (suppressCameraDispatch)
            {
                return;
            }

            logger.Info($"UI control change: camera='{_camera?.Name}', control={Name}, oldValue={oldValue}, newValue={newValue}, isPlaying={_camera?.IsPlaying}");

            if (_camera is not null && _camera.IsPlaying)
            {
                cameraControlService.Set(Name, newValue, _camera);
            }
            else
            {
                logger.Warn($"Ignoring UI control change because camera is not playing: camera='{_camera?.Name}', control={Name}, attemptedValue={newValue}");
            }
        }        

        public void SetDefault()
        {
            Value = Default;
        }
    }
}
