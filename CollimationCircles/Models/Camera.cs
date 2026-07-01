using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace CollimationCircles.Models
{
    public partial class Camera : ObservableObject, ICamera
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public APIType APIType { get; set; }
        public string Path { get; set; } = string.Empty;
        public int VendorId { get; set; }
        public int ProductId { get; set; }

        [ObservableProperty]
        public List<ICameraControl> controls = [];

        [ObservableProperty]
        private bool isPlaying = false;

        public Camera()
        {
            if (OperatingSystem.IsWindows())
            {
                APIType = APIType.Dshow;
            }
            else if (OperatingSystem.IsLinux())
            {
                APIType = APIType.V4l2;
            }
            else if (OperatingSystem.IsMacOS())
            {
                APIType = APIType.QTCapture;
            }
        }

        public void SetDefaultControls()
        {
            Controls.ForEach(c => c.SetDefault());
        }
    }
}
