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
            var vm = Ioc.Default.GetService<MainViewModel>();

            if (vm == null)
                return;

            foreach (ItemViewModel item in vm.Items)
            {
                var halfCrossSpacing = item.Spacing / 2;
                var w = item.Radius;
                var width2 = Width / 2;
                var height2 = Height / 2;
                var centerX = width2 - halfCrossSpacing;
                var centerY = height2 - halfCrossSpacing;

                var brush = new SolidColorBrush(Color.Parse(item.Color ?? CColor.Orange));

                var pen = new Pen(brush, item.Thickness);

                Matrix scale = Matrix.CreateScale(vm.Scale, vm.Scale);
                Matrix translate = Matrix.CreateTranslation(width2, height2);

                using (context.PushPreTransform(translate.Invert() * scale * translate))
                {
                    if (item.IsCross)
                    {
                        Matrix rotate = Matrix.CreateRotation(item.Rotation * Math.PI / 180);

                        using (context.PushPreTransform(translate.Invert() * rotate * translate))
                        {
                            context.DrawRectangle(pen, new Rect(centerX - w, centerY, 2 * w + item.Spacing, item.Spacing));
                            context.DrawRectangle(pen, new Rect(centerX, centerY - w, item.Spacing, 2 * w + item.Spacing));

                            if (vm.ShowLabels)
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
                    else
                    {
                        context.DrawEllipse(Brushes.Transparent, new Pen(brush, item.Thickness), new Point(width2, height2), item.Radius, item.Radius);

                        if (vm.ShowLabels)
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
            }
        }
    }
}