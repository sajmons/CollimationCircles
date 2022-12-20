using Avalonia;
using Avalonia.Media;
using CollimationCircles.ViewModels;
using System.Globalization;
using System;

namespace CollimationCircles.Services
{
    internal class DrawHelperService : IDrawHelperService
    {
        public void DrawCircle(DrawingContext context, bool showLabels, CircleViewModel item, double width2, double height2, IBrush brush)
        {
            context.DrawEllipse(Brushes.Transparent, new Pen(brush, item.Thickness), new Point(width2, height2), item.Radius, item.Radius);

            if (showLabels)
            {
                var formattedText = new FormattedText(
                    item.Label,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    15,
                    brush);

                context.DrawText(formattedText, new Point(width2, height2 - item.Radius));
            }
        }

        public void DrawCross(DrawingContext context, bool showLabels, CrossViewModel item, double width2, double height2, IBrush brush, Matrix translate)
        {
            var pen = new Pen(brush, item.Thickness);

            double w = item.Radius;
            var halfCrossSpacing = item.Size / 2;
            double centerX = width2 - halfCrossSpacing;
            double centerY = height2 - halfCrossSpacing;

            Matrix rotate = Matrix.CreateRotation(item.RotationAngle * Math.PI / 180);

            using (context.PushPreTransform(translate.Invert() * rotate * translate))
            {
                context.DrawRectangle(pen, new Rect(centerX - w, centerY, 2 * w + item.Size, item.Size));
                context.DrawRectangle(pen, new Rect(centerX, centerY - w, item.Size, 2 * w + item.Size));

                if (showLabels)
                {
                    var formattedText = new FormattedText(
                        item.Label,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        15,
                        brush);

                    context.DrawText(formattedText, new Point(width2, height2 - item.Radius));
                }
            }
        }

        public void DrawScrew(DrawingContext context, bool showLabels, ScrewViewModel item, double width2, double height2, IBrush brush, Matrix translate)
        {
            double angle = 360 / item.Count;

            Matrix rotate2 = Matrix.CreateRotation(item.RotationAngle * Math.PI / 180);
            using (context.PushPreTransform(translate.Invert() * rotate2 * translate))
            {
                for (int i = 0; i < item.Count; i++)
                {
                    Matrix rotate = Matrix.CreateRotation(angle * i * Math.PI / 180);
                    using (context.PushPreTransform(rotate * translate))
                    {
                        context.DrawEllipse(brush, new Pen(brush, item.Thickness), new Point(0, item.Radius), item.Size, item.Size);

                        if (showLabels)
                        {
                            var formattedText = new FormattedText(
                                $"{item.Label} {i}",
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                Typeface.Default,
                                15,
                                brush);

                            context.DrawText(formattedText, new Point(item.Size, item.Radius));
                        }
                    }
                }
            }
        }
        public void DrawPrimaryClip(DrawingContext context, bool showLabels, PrimaryClipViewModel item, double width2, double height2, IBrush brush, Matrix translate)
        {
            double angle = 360 / item.Count;

            Matrix rotate2 = Matrix.CreateRotation(item.RotationAngle * Math.PI / 180);
            using (context.PushPreTransform(translate.Invert() * rotate2 * translate))
            {
                for (int i = 0; i < item.Count; i++)
                {
                    Matrix rotate = Matrix.CreateRotation(angle * i * Math.PI / 180);
                    using (context.PushPreTransform(rotate * translate))
                    {
                        context.DrawRectangle(new Pen(brush, item.Thickness), new Rect(-item.Size / 2, item.Radius - item.Size / 2, item.Size, item.Size / 3));

                        if (showLabels)
                        {
                            var formattedText = new FormattedText(
                                $"{item.Label} {i}",
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                Typeface.Default,
                                15,
                                brush);

                            context.DrawText(formattedText, new Point(item.Size / 3, item.Radius));
                        }
                    }
                }
            }
        }

        public void DrawSpider(DrawingContext context, bool showLabels, SpiderViewModel item, double width2, double height2, IBrush brush, Matrix translate)
        {
            double angle = 360 / item.Count;

            Matrix rotate2 = Matrix.CreateRotation(item.RotationAngle * Math.PI / 180);
            using (context.PushPreTransform(translate.Invert() * rotate2 * translate))
            {
                for (int i = 0; i < item.Count; i++)
                {
                    Matrix rotate = Matrix.CreateRotation(angle * i * Math.PI / 180);
                    using (context.PushPreTransform(rotate * translate))
                    {
                        context.DrawRectangle(new Pen(brush, item.Thickness), new Rect(item.Size / 2, -item.Size / 4, item.Radius, item.Size / 2));

                        if (showLabels)
                        {
                            var formattedText = new FormattedText(
                                $"{item.Label} {i}",
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                Typeface.Default,
                                15,
                                brush);

                            context.DrawText(formattedText, new Point(item.Radius, item.Size / 2));
                        }
                    }
                }
            }
        }
    }
}