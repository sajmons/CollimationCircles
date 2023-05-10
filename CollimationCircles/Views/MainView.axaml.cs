using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Services;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using System;

namespace CollimationCircles.Views
{
    public partial class MainView : Window
    {
        IDrawHelperService? drawHelperService;

        public MainView()
        {
            InitializeComponent();

            DataContext = Ioc.Default.GetService<SettingsViewModel>();

            CheckForUpdate(DataContext as SettingsViewModel);

            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                DataContext = m.Value;
                InvalidateVisual();
            });

            drawHelperService = Ioc.Default.GetService<IDrawHelperService>();
        }

        void CheckForUpdate(SettingsViewModel? vm)
        {
            if (vm?.CheckForNewVersionOnStartup is true)
            {
                vm.CheckForUpdateCommand.ExecuteAsync(null);
            }
        }

        public override void Render(DrawingContext context)
        {
            try
            {
                SettingsViewModel? vm = Ioc.Default.GetService<SettingsViewModel>();

                if (vm != null)
                {
                    var it = vm?.Items;

                    if (it is not null)
                    {
                        foreach (ICollimationHelper item in it)
                        {
                            double width2 = Width / 2;
                            double height2 = Height / 2;
                            double scaleOrDefault = vm?.Scale ?? 1.0;
                            double rotAngleOrDefault = vm?.RotationAngle ?? 0;

                            var brush = new SolidColorBrush(item.ItemColor);

                            Matrix scaleMat = Matrix.CreateScale(scaleOrDefault, scaleOrDefault);
                            Matrix rotationMat = Matrix.CreateRotation(rotAngleOrDefault * Math.PI / 180);
                            Matrix translateMat = Matrix.CreateTranslation(width2, height2);

                            using (context.PushTransform(translateMat.Invert() * scaleMat * rotationMat * translateMat))
                            {
                                bool showlabels = vm?.ShowLabels ?? false;

                                if (item is CircleViewModel cModel && item.IsVisible)
                                {
                                    drawHelperService?.DrawCircle(context, showlabels, cModel, width2, height2, brush);
                                }

                                if (item is ScrewViewModel sModel && item.IsVisible)
                                {
                                    drawHelperService?.DrawScrew(context, showlabels, sModel, width2, height2, brush, translateMat);
                                }

                                if (item is PrimaryClipViewModel pcModel && item.IsVisible)
                                {
                                    drawHelperService?.DrawPrimaryClip(context, showlabels, pcModel, width2, height2, brush, translateMat);
                                }

                                if (item is SpiderViewModel spModel && item.IsVisible)
                                {
                                    drawHelperService?.DrawSpider(context, showlabels, spModel, width2, height2, brush, translateMat);
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