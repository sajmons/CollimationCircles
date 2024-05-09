using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace CollimationCircles.Models
{
    public partial class Camera : ObservableObject, ICamera
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public APIType APIType { get; set; }
        public string Path { get; set; } = string.Empty;

        [ObservableProperty]
        public List<ICameraControl> controls = [];

        public void SetDefaultControls()
        {
            Controls.ForEach(c => c.SetDefault());
        }
    }
}
