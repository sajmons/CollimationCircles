using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Services;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;

namespace CollimationCircles.Views
{
    public partial class MainWindow : Window
    {
        IDrawHelperService? drawHelperService;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = Ioc.Default.GetService<MainViewModel>();

            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                DataContext = m.Value;
                InvalidateVisual();
            });

            drawHelperService = Ioc.Default.GetService<IDrawHelperService>();
        }

        public override void Render(DrawingContext context)
        {
            try
            {
                MainViewModel? vm = Ioc.Default.GetService<MainViewModel>();

                if (vm is not null)
                {
                    var it = vm?.Items;

                    if (it is not null)
                    {
                        foreach (ICollimationHelper item in it)
                        {
                            double width2 = Width / 2;
                            double height2 = Height / 2;

                            var brush = new SolidColorBrush(item.ItemColor);

                            Matrix scale = Matrix.CreateScale(vm.Scale, vm.Scale);
                            Matrix rotation = Matrix.CreateRotation(vm.RotationAngle * Math.PI / 180);
                            Matrix translate = Matrix.CreateTranslation(width2, height2);

                            using (context.PushPreTransform(translate.Invert() * scale * rotation * translate))
                            {
                                if (item is CircleViewModel && item.IsVisible)
                                {
                                    drawHelperService?.DrawCircle(context, vm.ShowLabels, (CircleViewModel)item, width2, height2, brush);
                                }

                                if (item is ScrewViewModel && item.IsVisible)
                                {
                                    drawHelperService?.DrawScrew(context, vm.ShowLabels, (ScrewViewModel)item, width2, height2, brush, translate);
                                }

                                if (item is PrimaryClipViewModel && item.IsVisible)
                                {
                                    drawHelperService?.DrawPrimaryClip(context, vm.ShowLabels, (PrimaryClipViewModel)item, width2, height2, brush, translate);
                                }

                                if (item is SpiderViewModel && item.IsVisible)
                                {
                                    drawHelperService?.DrawSpider(context, vm.ShowLabels, (SpiderViewModel)item, width2, height2, brush, translate);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                throw;                
            }
        }        
    }
}