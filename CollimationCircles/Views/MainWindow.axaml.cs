using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CollimationCircles.Messages;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;

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
            var vm = Ioc.Default.GetService<MainViewModel>();

            if (vm == null)
                return;

            foreach (MarkViewModel mark in vm.Marks)
            {
                var halfCrossSpacing = mark.Spacing / 2;
                var w = mark.Radius;
                var width2 = Width / 2;
                var height2 = Height / 2;
                var centerX = width2 - halfCrossSpacing;
                var centerY = height2 - halfCrossSpacing;

                var brush = new SolidColorBrush(Color.Parse(mark.Color ?? Colors.Red.ToString()));

                var pen = new Pen(brush, mark.Thickness);

                Matrix scale = Matrix.CreateScale(vm.Scale, vm.Scale);
                Matrix translate = Matrix.CreateTranslation(width2, height2);

                using (context.PushPreTransform(translate.Invert() * scale * translate))
                {
                    if (mark.IsCross)
                    {
                        Matrix rotate = Matrix.CreateRotation(mark.Rotation * Math.PI / 180);

                        using (context.PushPreTransform(translate.Invert() * rotate * translate))
                        {
                            context.DrawRectangle(pen, new Rect(centerX - w, centerY, 2 * w + mark.Spacing, mark.Spacing));
                            context.DrawRectangle(pen, new Rect(centerX, centerY - w, mark.Spacing, 2 * w + mark.Spacing));

                            if (vm.ShowLabels)
                            {
                                var formattedText = new FormattedText(
                                    mark.Label,
                                    Typeface.Default,
                                    15,
                                    TextAlignment.Center,
                                    TextWrapping.NoWrap,
                                    new Size(Width, Bounds.Height));

                                context.DrawText(brush, new Point(0, height2 - mark.Radius), formattedText);
                            }
                        }
                    }
                    else
                    {
                        context.DrawEllipse(Brushes.Transparent, new Pen(brush, mark.Thickness), new Point(width2, height2), mark.Radius, mark.Radius);

                        if (vm.ShowLabels)
                        {

                            var formattedText = new FormattedText(
                                mark.Label,
                                Typeface.Default,
                                15,
                                TextAlignment.Center,
                                TextWrapping.NoWrap,
                                new Size(Width, Bounds.Height));

                            context.DrawText(brush, new Point(0, height2 - mark.Radius), formattedText);

                        }
                    }                    
                }
            }
        }
    }
}