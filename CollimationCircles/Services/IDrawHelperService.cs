using Avalonia;
using Avalonia.Media;
using CollimationCircles.ViewModels;

namespace CollimationCircles.Services
{
    internal interface IDrawHelperService
    {
        void DrawCircle(DrawingContext context, bool showLabels, bool selected, CircleViewModel item, double width2, double height2, IBrush brush, double labelSize);
        void DrawScrew(DrawingContext context, bool showLabels, bool selected, ScrewViewModel item, double width2, double height2, IBrush brush, Matrix translate, double labelSize);
        void DrawPrimaryClip(DrawingContext context, bool showLabels, bool selected, PrimaryClipViewModel item, double width2, double height2, IBrush brush, Matrix translate, double labelSize);
        void DrawSpider(DrawingContext context, bool showLabels, bool selected, SpiderViewModel item, double width2, double height2, IBrush brush, Matrix translate, double labelSize);
        public void DrawBahtinovMask(DrawingContext context, bool showLabels, bool selected, BahtinovMaskViewModel item, double width2, double height2, IBrush brush, Matrix translate, double labelSize);
    }
}