using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;

namespace CollimationCircles.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DataContext = Ioc.Default.GetService<MainViewModel>();

            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                DataContext = m.Value;
                InvalidateVisual();
            });
        }

        public override void Render(DrawingContext context)
        {
            var vm = DataContext as MainViewModel;

            if (vm == null)
                return;

            foreach (ICollimationHelper item in vm.Items)
            {
                var width2 = Width / 2;
                var height2 = Height / 2;

                var brush = new SolidColorBrush(Color.Parse(item.Color));

                Matrix scale = Matrix.CreateScale(vm.Scale, vm.Scale);
                Matrix rotation = Matrix.CreateRotation(vm.Rotation * Math.PI / 180);
                Matrix translate = Matrix.CreateTranslation(width2, height2);

                using (context.PushPreTransform(translate.Invert() * scale * rotation * translate))
                {
                    if (item is CrossViewModel && item.IsVisible)
                    {
                        DrawCross(context, vm.ShowLabels, (CrossViewModel)item, width2, height2, brush, translate);
                    }

                    if (item is CircleViewModel && item.IsVisible)
                    {
                        DrawCircle(context, vm.ShowLabels, (CircleViewModel)item, width2, height2, brush);
                    }

                    if (item is ScrewViewModel && item.IsVisible)
                    {
                        DrawScrew(context, vm.ShowLabels, (ScrewViewModel)item, width2, height2, brush, translate, 4);
                    }
                }
            }
        }

        private void DrawCircle(DrawingContext context, bool showLabels, CircleViewModel item, double width2, double height2, SolidColorBrush brush)
        {
            context.DrawEllipse(Brushes.Transparent, new Pen(brush, item.Thickness), new Point(width2, height2), item.Radius, item.Radius);

            if (showLabels)
            {
                var formattedText = new FormattedText(
                    item.Label,
                    Typeface.Default,
                    15,
                    TextAlignment.Center,
                    TextWrapping.NoWrap,
                    new Size(Width, Bounds.Height));

                context.DrawText(brush, new Point(0, height2 - item.Radius), formattedText);
            }
        }

        private void DrawCross(DrawingContext context, bool showLabels, CrossViewModel item, double width2, double height2, SolidColorBrush brush, Matrix translate)
        {
            var pen = new Pen(brush, item.Thickness);

            double w = item.Radius;
            var halfCrossSpacing = item.Size / 2;
            double centerX = width2 - halfCrossSpacing;
            double centerY = height2 - halfCrossSpacing;

            Matrix rotate = Matrix.CreateRotation(item.Rotation * Math.PI / 180);

            using (context.PushPreTransform(translate.Invert() * rotate * translate))
            {
                context.DrawRectangle(pen, new Rect(centerX - w, centerY, 2 * w + item.Size, item.Size));
                context.DrawRectangle(pen, new Rect(centerX, centerY - w, item.Size, 2 * w + item.Size));

                if (showLabels)
                {
                    var formattedText = new FormattedText(
                        item.Label,
                        Typeface.Default,
                        15,
                        TextAlignment.Center,
                        TextWrapping.NoWrap,
                        new Size(Width, Bounds.Height));

                    context.DrawText(brush, new Point(0, height2 - item.Radius), formattedText);
                }
            }
        }

        private void DrawScrew(DrawingContext context, bool showLabels, ScrewViewModel item, double width2, double height2, SolidColorBrush brush, Matrix translate, int numCirc)
        {
            double angle = 360 / numCirc;

            Matrix rotate2 = Matrix.CreateRotation(item.Rotation * Math.PI / 180);
            using (context.PushPreTransform(translate.Invert() * rotate2 * translate))
            {
                for (int i = 0; i < numCirc; i++)
                {
                    Matrix rotate = Matrix.CreateRotation(angle * i * Math.PI / 180);
                    using (context.PushPreTransform(rotate * translate))
                    {
                        context.DrawEllipse(brush, new Pen(brush, item.Thickness), new Point(0, item.Radius), item.Size, item.Size);

                        if (showLabels)
                        {
                            var formattedText = new FormattedText(
                                $"{item.Label} {i}",
                                Typeface.Default,
                                15,
                                TextAlignment.Center,
                                TextWrapping.NoWrap,
                                new Size(10, 10 + height2 - item.Radius));

                            context.DrawText(brush, new Point(0, item.Radius), formattedText);
                        }
                    }
                }
            }
        }
    }
}