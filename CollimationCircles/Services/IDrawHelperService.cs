using Avalonia;
using Avalonia.Media;
using CollimationCircles.ViewModels;

namespace CollimationCircles.Services
{
    internal interface IDrawHelperService
    {
        void DrawCircle(DrawingContext context, bool showLabels, CircleViewModel item, double width2, double height2, IBrush brush, double labelSize);
        void DrawScrew(DrawingContext context, bool showLabels, ScrewViewModel item, double width2, double height2, IBrush brush, Matrix translate, double labelSize);
        void DrawPrimaryClip(DrawingContext context, bool showLabels, PrimaryClipViewModel item, double width2, double height2, IBrush brush, Matrix translate, double labelSize);
        void DrawSpider(DrawingContext context, bool showLabels, SpiderViewModel item, double width2, double height2, IBrush brush, Matrix translate, double labelSize);
        public void DrawBahtinovMask(DrawingContext context, bool showLabels, BahtinovMaskViewModel item, double width2, double height2, IBrush brush, Matrix translate, double labelSize);
    }
}