using Avalonia;
using Avalonia.Media;
using CollimationCircles.Models;
using CollimationCircles.ViewModels;
using System;
using System.Globalization;

namespace CollimationCircles.Services
{
    internal class DrawHelperService : IDrawHelperService
    {
        readonly FormattedText selectedMark = new(
                        "▲",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        18,
                        Brushes.Yellow);

        public void DrawMask<T>(DrawingContext context, SettingsViewModel? vm, T item, IBrush brush, Matrix translate)
        {
            if (item is not ICollimationHelper helper || !helper.IsVisible) return;

            if (vm is not null)
            {
                switch (item)
                {
                    case CircleViewModel civm:
                        DrawCircle(context, vm, civm, brush, translate);
                        break;
                    case ScrewViewModel scvm:
                        DrawScrew(context, vm, scvm, brush, translate);
                        break;
                    case PrimaryClipViewModel pcvm:
                        DrawPrimaryClip(context, vm, pcvm, brush, translate);
                        break;
                    case SpiderViewModel spvm:
                        DrawSpider(context, vm, spvm, brush, translate);
                        break;
                    case BahtinovMaskViewModel bmvm:
                        DrawBahtinovMask(context, vm, bmvm, brush, translate);
                        break;
                }
            }
        }

        private void DrawCircle(DrawingContext context, SettingsViewModel vm, CircleViewModel item, IBrush brush, Matrix translate)
        {
            using (context.PushTransform(translate))
            {
                context.DrawEllipse(Brushes.Transparent, new Pen(brush, item.Thickness), new Point(0, 0), item.Radius, item.Radius);

                if (vm.ShowLabels)
                {
                    var formattedText = new FormattedText(
                        item?.Label ?? "Undefined",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        vm.LabelSize,
                        brush);

                    if (item is not null)
                    {
                        context.DrawText(formattedText, new Point(- item.Size * formattedText.Width / vm.LabelSize, - item.Radius - item.Size * 2));
                    }
                }

                if (vm.SelectedItem is CircleViewModel && vm.ShowMarkAtSelectedItem && item is not null && vm.SelectedItem == item)
                {
                    context.DrawText(selectedMark, new Point(- item.Size, - item.Radius));
                }
            }
        }

        private void DrawScrew(DrawingContext context, SettingsViewModel vm, ScrewViewModel item, IBrush brush, Matrix translate)
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

                        if (vm.ShowLabels)
                        {
                            var formattedText = new FormattedText(
                                $"{item.Label} {i}",
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                Typeface.Default,
                                vm.LabelSize,
                                brush);

                            context.DrawText(formattedText, new Point(-item.Size - (formattedText.Width / vm.LabelSize), item.Radius + item.Size));
                        }

                        if (vm.SelectedItem is ScrewViewModel && i == 0 && vm.ShowMarkAtSelectedItem && vm.SelectedItem == item)
                        {
                            context.DrawText(selectedMark, new Point(-item.Size, item.Radius + item.Size));
                        }
                    }
                }
            }
        }

        private void DrawPrimaryClip(DrawingContext context, SettingsViewModel vm, PrimaryClipViewModel item, IBrush brush, Matrix translate)
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

                        if (vm.ShowLabels)
                        {
                            var formattedText = new FormattedText(
                                $"{item.Label} {i}",
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                Typeface.Default,
                                vm.LabelSize,
                                brush);

                            context.DrawText(formattedText, new Point((-item.Size / 2 - (formattedText.Width / vm.LabelSize)) / 2, item.Radius));
                        }

                        if (vm.SelectedItem is PrimaryClipViewModel && i == 0 && vm.ShowMarkAtSelectedItem && vm.SelectedItem == item)
                        {
                            context.DrawText(selectedMark, new Point((-item.Size / 2) / 2, item.Radius));
                        }
                    }
                }
            }
        }

        private void DrawSpider(DrawingContext context, SettingsViewModel vm, SpiderViewModel item, IBrush brush, Matrix translate)
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

                using (context.PushTransform(translate))
                {
                    if (vm.ShowLabels)
                    {
                        var formattedText = new FormattedText(
                            $"{item.Label}",
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            Typeface.Default,
                            vm.LabelSize,
                            brush);

                        context.DrawText(formattedText, new Point(- item.Radius, - item.Size / 2));
                    }

                    if (vm.SelectedItem is SpiderViewModel && vm.ShowMarkAtSelectedItem && vm.SelectedItem == item)
                    {
                        context.DrawText(selectedMark, new Point(- item.Radius, - item.Size / 2));
                    }
                }
            }
        }

        private void DrawBahtinovMask(DrawingContext context, SettingsViewModel vm, BahtinovMaskViewModel item, IBrush brush, Matrix translate)
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
                        using (context.PushTransform(translate))
                        {
                            context.DrawRectangle(new Pen(brush, item.Thickness), new Rect(-item.Radius, -item.Size / 4, item.Radius * 2, item.Size / 2));
                        }
                    }
                }

                using (context.PushTransform(translate))
                {
                    if (vm.ShowLabels)
                    {
                        var formattedText = new FormattedText(
                            $"{item.Label}",
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            Typeface.Default,
                            vm.LabelSize,
                            brush);

                        context.DrawText(formattedText, new Point(-item.Radius, -item.Size + vm.LabelSize));
                    }

                    if (vm.SelectedItem is BahtinovMaskViewModel && vm.ShowMarkAtSelectedItem && vm.SelectedItem == item)
                    {
                        context.DrawText(selectedMark, new Point(-item.Radius, item.Size));
                    }
                }
            }
        }
    }
}