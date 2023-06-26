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
        readonly IDrawHelperService? dhs;

        public MainView()
        {
            InitializeComponent();

            DataContext = Ioc.Default.GetService<SettingsViewModel>();

            CheckForUpdate(DataContext as SettingsViewModel);

            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                DataContext = m.Value;
                Topmost = m.Value.AlwaysOnTop;
                InvalidateVisual();
            });

            dhs = Ioc.Default.GetService<IDrawHelperService>();
        }

        static void CheckForUpdate(SettingsViewModel? vm)
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

                if (vm is not null)
                {
                    int dockedWidth = vm.DockInMainWindow ? vm.SettingsMinWidth / 2 : 0;
                    var items = vm?.Items;

                    if (items is not null)
                    {
                        foreach (ICollimationHelper item in items)
                        {
                            double width2 = Width / 2 - dockedWidth;
                            double height2 = Height / 2;
                            double scaleOrDefault = vm?.Scale ?? 1.0;
                            double rotAngleOrDefault = vm?.RotationAngle ?? 0;

                            var brush = new SolidColorBrush(item.ItemColor);

                            Matrix scaleMat = Matrix.CreateScale(scaleOrDefault, scaleOrDefault);
                            Matrix rotationMat = Matrix.CreateRotation(rotAngleOrDefault * Math.PI / 180);
                            Matrix translateMat = Matrix.CreateTranslation(width2, height2);

                            using (context.PushTransform(translateMat.Invert() * scaleMat * rotationMat * translateMat))
                            {
                                switch (item)
                                {
                                    case CircleViewModel civm:
                                        dhs?.DrawMask(context, vm, civm, width2, height2, brush, translateMat);
                                        break;
                                    case ScrewViewModel scvm:
                                        dhs?.DrawMask(context, vm, scvm, width2, height2, brush, translateMat);
                                        break;
                                    case PrimaryClipViewModel pcvm:
                                        dhs?.DrawMask(context, vm, pcvm, width2, height2, brush, translateMat);
                                        break;
                                    case SpiderViewModel spvm:
                                        dhs?.DrawMask(context, vm, spvm, width2, height2, brush, translateMat);
                                        break;
                                    case BahtinovMaskViewModel bmvm:
                                        dhs?.DrawMask(context, vm, bmvm, width2, height2, brush, translateMat);
                                        break;
                                };
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