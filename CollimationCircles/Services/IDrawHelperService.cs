using Avalonia;
using Avalonia.Media;
using CollimationCircles.ViewModels;

namespace CollimationCircles.Services
{
    internal interface IDrawHelperService
    {
        public void DrawMask<T>(DrawingContext context, SettingsViewModel? vm, T item, double width2, double height2, IBrush brush, Matrix translate);
    }
}