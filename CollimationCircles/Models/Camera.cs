using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace CollimationCircles.Models
{
    public partial class Camera : ObservableObject, ICamera
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SensorWidth { get; set; }
        public int SensorHeight { get; set; }
        public APIType APIType { get; set; }
        public string ServerAddress { get; set; } = string.Empty;
        public int ServerPort { get; set; }
        public int DeviceNumber { get; set; }
        public string AlpacaUniqueId { get; set; } = string.Empty;

        [ObservableProperty]
        public List<ICameraControl> controls = [];

        [ObservableProperty]
        private bool isPlaying = false;        

        public void SetDefaultControls()
        {
            Controls.ForEach(c => c.SetDefault());
        }
    }
}
