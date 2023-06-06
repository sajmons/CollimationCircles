using Avalonia;
using Avalonia.Media;
using CollimationCircles.ViewModels;
using System;
using System.Globalization;

namespace CollimationCircles.Services
{
    internal class DrawHelperService : IDrawHelperService
    {
        public void DrawCircle(DrawingContext context, bool showLabels, CircleViewModel item, double width2, double height2, IBrush brush, double labelSize)
        {
            context.DrawEllipse(Brushes.Transparent, new Pen(brush, item.Thickness), new Point(width2, height2), item.Radius, item.Radius);

            if (showLabels)
            {
                var formattedText = new FormattedText(
                    item?.Label ?? "Undefined",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    labelSize,
                    brush);

                context.DrawText(formattedText, new Point(width2 - item.Size * formattedText.Width / labelSize, height2 - item.Radius - item.Size * 2));
            }
        }

        public void DrawScrew(DrawingContext context, bool showLabels, ScrewViewModel item, double width2, double height2, IBrush brush, Matrix translate, double labelSize)
        {
            double angle = 360 / item.Count;

            Matrix rotate2 = Matrix.CreateRotation(item.RotationAngle * Math.PI / 180);
            using (context.PushTransform(translate.Invert() * rotate2 * translate))
            {
                for (int i = 0; i < item.Count; i++)
                {
                    Matrix rotate = Matrix.CreateRotation(angle * i * Math.PI / 180);
                    using (context.PushTransform(rotate * translate))
                    {
                        context.DrawEllipse(brush, new Pen(brush, item.Thickness), new Point(0, item.Radius), item.Size, item.Size);

                        if (showLabels)
                        {
                            var formattedText = new FormattedText(
                                $"{item.Label} {i}",
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                Typeface.Default,
                                labelSize,
                                brush);

                            context.DrawText(formattedText, new Point((-item.Size - (formattedText.Width / labelSize)), item.Radius + item.Size));
                        }
                    }
                }
            }
        }
        public void DrawPrimaryClip(DrawingContext context, bool showLabels, PrimaryClipViewModel item, double width2, double height2, IBrush brush, Matrix translate, double labelSize)
        {
            double angle = 360 / item.Count;

            Matrix rotate2 = Matrix.CreateRotation(item.RotationAngle * Math.PI / 180);
            using (context.PushTransform(translate.Invert() * rotate2 * translate))
            {
                for (int i = 0; i < item.Count; i++)
                {
                    Matrix rotate = Matrix.CreateRotation(angle * i * Math.PI / 180);
                    using (context.PushTransform(rotate * translate))
                    {
                        context.DrawRectangle(new Pen(brush, item.Thickness), new Rect(-item.Size / 2, item.Radius - item.Size / 2, item.Size, item.Size / 3));

                        if (showLabels)
                        {
                            var formattedText = new FormattedText(
                                $"{item.Label} {i}",
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                Typeface.Default,
                                labelSize,
                                brush);

                            context.DrawText(formattedText, new Point((-item.Size / 2 - (formattedText.Width / labelSize)) / 2, item.Radius));
                        }
                    }
                }
            }
        }

        public void DrawSpider(DrawingContext context, bool showLabels, SpiderViewModel item, double width2, double height2, IBrush brush, Matrix translate, double labelSize)
        {
            if (item.Count < 1) return;

            double angle = 360 / item.Count;

            Matrix rotate2 = Matrix.CreateRotation(item.RotationAngle * Math.PI / 180);
            using (context.PushTransform(translate.Invert() * rotate2 * translate))
            {
                for (int i = 0; i < item.Count; i++)
                {
                    Matrix rotate = Matrix.CreateRotation(angle * i * Math.PI / 180);
                    using (context.PushTransform(rotate * translate))
                    {
                        context.DrawRectangle(new Pen(brush, item.Thickness), new Rect(-item.Size / 4, -item.Size / 4, item.Radius + item.Size / 4, item.Size / 2));
                    }
                }

                if (showLabels)
                {
                    var formattedText = new FormattedText(
                        $"{item.Label}",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        labelSize,
                        brush);

                    context.DrawText(formattedText, new Point(width2 - item.Radius, height2 - item.Size / 2));
                }
            }
        }

        public void DrawBahtinovMask(DrawingContext context, bool showLabels, BahtinovMaskViewModel item, double width2, double height2, IBrush brush, Matrix translate, double labelSize)
        {
            double angle = item.InclinationAngle;

            Matrix rotate2 = Matrix.CreateRotation(item.RotationAngle * Math.PI / 180);
            using (context.PushTransform(translate.Invert() * rotate2 * translate))
            {
                for (int i = -1; i <= 1; i++)
                {
                    Matrix rotate = Matrix.CreateRotation(angle * i * Math.PI / 180);
                    using (context.PushTransform(translate.Invert() * rotate * translate))
                    {
                        context.DrawRectangle(new Pen(brush, item.Thickness), new Rect(width2 - item.Radius, height2 - item.Size / 4, item.Radius * 2 + item.Size / 4, item.Size / 2));
                    }
                }

                if (showLabels)
                {
                    var formattedText = new FormattedText(
                        $"{item.Label}",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        labelSize,
                        brush);

                    context.DrawText(formattedText, new Point(width2 - item.Radius, height2 - item.Size + labelSize));
                }
            }
        }
    }
}