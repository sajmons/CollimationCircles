using CollimationCircles.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace CollimationCircles.Models
{
    public partial class Profile : ObservableValidator
    {
        [ObservableProperty]
        public string name;

        [ObservableProperty]
        public ObservableCollection<CollimationHelper> shapes = new ObservableCollection<CollimationHelper>();

        public Profile(string name, ObservableCollection<CollimationHelper> scopeShapes)
        { 
            Name = name;
            Shapes.AddRange(scopeShapes);
        }
    }
}
