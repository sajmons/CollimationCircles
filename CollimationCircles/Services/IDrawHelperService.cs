using Avalonia;
using Avalonia.Media;
using CollimationCircles.ViewModels;
using System.Collections.Generic;

namespace CollimationCircles.Services
{
    internal interface IDrawHelperService
    {
        public void DrawMask<T>(DrawingContext context, SettingsViewModel? vm, T item, Matrix translate);
        public void DrawText(DrawingContext context, string text, Point location, IBrush foregroundColor, double size);
    }
}