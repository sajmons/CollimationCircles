using CollimationCircles.Helper;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using System;

namespace CollimationCircles.Models
{
    public partial class CameraControl : ObservableObject, ICameraControl
    {
        private readonly ICameraControlService cameraControlService;
        private readonly ILibVLCService libVLCService;
        public ControlType Name { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public double Step { get; set; } = 0.1;
        public int Default { get; set; }        
        public ControlValueType ValueType { get; set; }

        [ObservableProperty]
        private int value;
        public string Flags { get; set; } = string.Empty;        

        private readonly bool initialization = false;

        public CameraControl(ControlType controlName)
        {
            cameraControlService = Ioc.Default.GetRequiredService<ICameraControlService>();
            libVLCService = Ioc.Default.GetRequiredService<ILibVLCService>();
            Name = controlName;

            initialization = true;

            try
            {
                Initialize(controlName);
            }
            finally
            {
                initialization = false;
            }
        }

        private void Initialize(ControlType controlName)
        {
            Value = Default;
        }

        partial void OnValueChanged(int oldValue, int newValue)
        {
            if (!initialization)
            {
                cameraControlService.Set(Name, newValue, libVLCService.Camera);
            }
        }

        public void SetDefault()
        {
            Initialize(Name);
        }
    }
}
