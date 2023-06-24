using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
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
        readonly IDrawHelperService? drawHelperService;

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

            drawHelperService = Ioc.Default.GetService<IDrawHelperService>();
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
                    var it = vm?.Items;                    
                    int dockedWidth = vm.DockInMainWindow ? vm.SettingsMinWidth / 2 : 0;

                    if (it is not null)
                    {
                        foreach (ICollimationHelper item in it)
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
                                bool showlabels = vm?.ShowLabels ?? false;
                                double labelSize = vm?.LabelSize ?? 15;                                

                                if (item is CircleViewModel cModel && item.IsVisible)
                                {
                                    var selected = vm?.SelectedItem == cModel;
                                    drawHelperService?.DrawCircle(context, showlabels, selected, cModel, width2, height2, brush, labelSize);
                                }

                                if (item is ScrewViewModel sModel && item.IsVisible)
                                {
                                    var selected = vm?.SelectedItem == sModel;
                                    drawHelperService?.DrawScrew(context, showlabels, selected, sModel, width2, height2, brush, translateMat, labelSize);
                                }

                                if (item is PrimaryClipViewModel pcModel && item.IsVisible)
                                {
                                    var selected = vm?.SelectedItem == pcModel;
                                    drawHelperService?.DrawPrimaryClip(context, showlabels, selected, pcModel, width2, height2, brush, translateMat, labelSize);
                                }

                                if (item is SpiderViewModel spModel && item.IsVisible)
                                {
                                    var selected = vm?.SelectedItem == spModel;
                                    drawHelperService?.DrawSpider(context, showlabels, selected, spModel, width2, height2, brush, translateMat, labelSize);
                                }

                                if (item is BahtinovMaskViewModel foModel && item.IsVisible)
                                {
                                    var selected = vm?.SelectedItem == foModel;
                                    drawHelperService?.DrawBahtinovMask(context, showlabels, selected, foModel, width2, height2, brush, translateMat, labelSize);
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