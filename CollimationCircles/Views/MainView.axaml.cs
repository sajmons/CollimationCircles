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
        private readonly IDrawHelperService dhs;
        private readonly SettingsViewModel vm;
        private readonly IKeyHandlingService khs;

        public MainView()
        {
            InitializeComponent();

            vm = Ioc.Default.GetRequiredService<SettingsViewModel>();

            DataContext = vm;

            Position = vm.MainWindowPosition;

            CheckForUpdate(vm);

            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                Topmost = m.Value.AlwaysOnTop;
                InvalidateVisual();
            });

            dhs = Ioc.Default.GetRequiredService<IDrawHelperService>();
            khs = Ioc.Default.GetRequiredService<IKeyHandlingService>();

            PositionChanged += MainView_PositionChanged;
        }

        private void MainView_PositionChanged(object? sender, PixelPointEventArgs e)
        {
            vm.MainWindowPosition = Position;
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
                int dockedWidth = vm.DockInMainWindow ? vm.SettingsMinWidth / 2 : 0;
                var items = vm.Items;

                if (vm.ShowKeyboardShortcuts == true)
                {
                    string shortcutsString = string.Empty;

                    foreach (var sk in vm.GlobalShortcuts)
                    {
                        shortcutsString += $"{sk.Key}: {sk.Value}{Environment.NewLine}";
                    }

                    foreach (var sk in vm.ShapeShortcuts)
                    {
                        shortcutsString += $"{sk.Key}: {sk.Value}{Environment.NewLine}";
                    }

                    dhs.DrawText(context, shortcutsString, new Point(15, 10), Brushes.Yellow, vm.LabelSize);
                }

                if (!vm.LicenseService.IsValid)
                {
                    dhs.DrawText(context, vm.ResSvc.TryGetString("UnlicensedVersion"), new Point(15, vm.MainWindowHeight - 35), Brushes.Red, vm.LabelSize);
                }

                if (items is not null)
                {
                    double width2 = Width / 2 - dockedWidth;
                    double height2 = Height / 2;
                    double scaleOrDefault = vm.Scale;
                    double rotAngleOrDefault = vm.RotationAngle;
                    double offsetX = vm.GlobalOffsetX;
                    double offsetY = vm.GlobalOffsetY;

                    Matrix scaleMat = Matrix.CreateScale(scaleOrDefault, scaleOrDefault);
                    Matrix rotationMat = Matrix.CreateRotation(rotAngleOrDefault * Math.PI / 180);
                    Matrix translateMat = Matrix.CreateTranslation(width2 + offsetX, height2 + offsetY);

                    foreach (ICollimationHelper item in items)
                    {
                        using (context.PushTransform(translateMat.Invert() * scaleMat * rotationMat * translateMat))
                        {
                            switch (item)
                            {
                                case CircleViewModel civm:
                                    dhs.DrawMask(context, vm, civm, translateMat);
                                    break;
                                case ScrewViewModel scvm:
                                    dhs.DrawMask(context, vm, scvm, translateMat);
                                    break;
                                case PrimaryClipViewModel pcvm:
                                    dhs.DrawMask(context, vm, pcvm, translateMat);
                                    break;
                                case SpiderViewModel spvm:
                                    dhs.DrawMask(context, vm, spvm, translateMat);
                                    break;
                                case BahtinovMaskViewModel bmvm:
                                    dhs.DrawMask(context, vm, bmvm, translateMat);
                                    break;
                            };
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
            // Remember window position and Size
            vm.MainWindowPosition = Position;
            vm.MainWindowWidth = Width;
            vm.MainWindowHeight = Height;

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            vm.SaveState();

            base.OnClosed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            khs.HandleMovement(this, vm, e);
            khs.HandleGlobalScale(vm, e);
            khs.HandleHelperRadius(vm, e);
            khs.HandleGlobalRotation(vm, e);
            khs.HandleHelperRotation(vm, e);
            khs.HandleHelperCount(vm, e);
            khs.HandleHelperThickness(vm, e);
            khs.HandleHelperSpacing(vm, e);
            khs.HandleHelperInclination(vm, e);

            base.OnKeyDown(e);
        }
    }
}