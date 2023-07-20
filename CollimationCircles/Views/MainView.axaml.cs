using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
        private readonly IDrawHelperService? dhs;
        private readonly SettingsViewModel? vm;
        private readonly IKeyHandlingService? khs;

        public MainView()
        {
            InitializeComponent();

            vm = Ioc.Default.GetService<SettingsViewModel>();            

            DataContext = vm;

            Position = vm?.MainWindowPosition ?? new PixelPoint();

            CheckForUpdate(vm);

            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                Topmost = m.Value.AlwaysOnTop;
                InvalidateVisual();
            });

            dhs = Ioc.Default.GetService<IDrawHelperService>();
            khs = Ioc.Default.GetService<IKeyHandlingService>();
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
                            double offsetX = vm?.GlobalOffsetX ?? 0;
                            double offsetY = vm?.GlobalOffsetY ?? 0;

                            var brush = new SolidColorBrush(item.ItemColor);

                            Matrix scaleMat = Matrix.CreateScale(scaleOrDefault, scaleOrDefault);
                            Matrix rotationMat = Matrix.CreateRotation(rotAngleOrDefault * Math.PI / 180);
                            Matrix translateMat = Matrix.CreateTranslation(width2 + offsetX, height2 + offsetY);

                            using (context.PushTransform(translateMat.Invert() * scaleMat * rotationMat * translateMat))
                            {
                                switch (item)
                                {
                                    case CircleViewModel civm:
                                        dhs?.DrawMask(context, vm, civm, brush, translateMat);
                                        break;
                                    case ScrewViewModel scvm:
                                        dhs?.DrawMask(context, vm, scvm, brush, translateMat);
                                        break;
                                    case PrimaryClipViewModel pcvm:
                                        dhs?.DrawMask(context, vm, pcvm, brush, translateMat);
                                        break;
                                    case SpiderViewModel spvm:
                                        dhs?.DrawMask(context, vm, spvm, brush, translateMat);
                                        break;
                                    case BahtinovMaskViewModel bmvm:
                                        dhs?.DrawMask(context, vm, bmvm, brush, translateMat);
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

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (vm is not null)
            {
                // Remember window position and Size
                vm.MainWindowPosition = Position;
                vm.MainWindowWidth = Width;
                vm.MainWindowHeight = Height;
            }

            base.OnClosing(e);            
        }

        protected override void OnClosed(EventArgs e)
        {
            vm?.SaveState();

            base.OnClosed(e);            
        }        

        protected override void OnKeyDown(KeyEventArgs e)
        {
            khs?.HandleMovement(this, vm, e);
            khs?.HandleGlobalScale(vm, e);
            khs?.HandleHelperRadius(vm, e);
            khs?.HandleGlobalRotation(vm, e);
            khs?.HandleHelperRotation(vm, e);
            khs?.HandleHelperCount(vm, e);
            khs?.HandleHelperThickness(vm, e);
            khs?.HandleHelperSpacing(vm, e);

            base.OnKeyDown(e);
        }
    }
}