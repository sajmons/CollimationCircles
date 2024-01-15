using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CollimationCircles.Helper;
using CollimationCircles.ViewModels;

namespace CollimationCircles.Services
{
    public class KeyHandlingService : IKeyHandlingService
    {
        public void HandleMovement(Window window, SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled && e.KeyModifiers == KeyModifiers.Control)
            {
                int x = window.Position.X;
                int y = window.Position.Y;
                int increment = 1;

                if (vm != null)
                {
                    switch (e.Key)
                    {
                        case Key.Up:
                            y -= increment;
                            e.Handled = true;
                            break;

                        case Key.Down:
                            y += increment;
                            e.Handled = true;
                            break;

                        case Key.Left:
                            x -= increment;
                            e.Handled = true;
                            break;
                        case Key.Right:
                            x += increment;
                            e.Handled = true;
                            break;
                    }

                    window.Position = new PixelPoint(x, y);

                    vm.MainWindowPosition = window.Position;
                }
            }
        }

        public void HandleGlobalRotation(SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled && e.KeyModifiers == KeyModifiers.Control)
            {
                if (vm != null)
                {
                    double rotation = vm.RotationAngle;

                    switch (e.Key)
                    {
                        case Key.R:
                            if (vm.RotationAngle < Ranges.RotationAngleMax)
                                rotation += 1;
                            e.Handled = true;
                            break;
                        case Key.F:
                            if (vm.RotationAngle > Ranges.RotationAngleMin)
                                rotation -= 1;
                            e.Handled = true;
                            break;
                    }

                    if (e.Handled)
                    {
                        vm.RotationAngle = rotation;
                    }
                }
            }
        }

        public void HandleGlobalScale(SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled && e.KeyModifiers == KeyModifiers.Control)
            {
                if (vm != null)
                {
                    double increment = vm.Scale;

                    switch (e.Key)
                    {
                        case Key.Add:
                        case Key.OemPlus:
                            if (vm.Scale < Ranges.ScaleMax)
                                increment += 0.01;
                            e.Handled = true;
                            break;

                        case Key.Subtract:
                        case Key.OemMinus:
                            if (vm.Scale > Ranges.ScaleMin)
                                increment -= 0.01;
                            e.Handled = true;
                            break;
                    }

                    if (e.Handled)
                    {
                        vm.Scale = increment;
                    }
                }
            }
        }

        public void HandleHelperRadius(SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled && e.KeyModifiers == KeyModifiers.Control)
            {
                if (vm != null)
                {
                    switch (e.Key)
                    {
                        case Key.W:
                            if (vm.SelectedItem.Radius < Ranges.RadiusMax)
                                vm.SelectedItem.Radius += 1;
                            e.Handled = true;
                            break;

                        case Key.S:
                            if (vm.SelectedItem.Radius > Ranges.RadiusMin)
                                vm.SelectedItem.Radius -= 1;
                            e.Handled = true;
                            break;
                    }
                }
            }
        }

        public void HandleHelperRotation(SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled && e.KeyModifiers == KeyModifiers.Control)
            {
                if (vm != null)
                {
                    switch (e.Key)
                    {
                        case Key.A:
                            if (vm.SelectedItem.RotationAngle < Ranges.RotationAngleMax)
                                vm.SelectedItem.RotationAngle += 1;
                            e.Handled = true;
                            break;

                        case Key.Q:
                            if (vm.SelectedItem.RotationAngle > Ranges.RotationAngleMin)
                                vm.SelectedItem.RotationAngle -= 1;
                            e.Handled = true;
                            break;                        
                    }
                }
            }
        }

        public void HandleHelperCount(SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled && e.KeyModifiers == KeyModifiers.Control)
            {
                if (vm != null)
                {
                    switch (e.Key)
                    {
                        case Key.T:
                            if (vm.SelectedItem.Count < vm.SelectedItem.MaxCount)
                                vm.SelectedItem.Count += 1;
                            e.Handled = true;
                            break;

                        case Key.G:
                            if (vm.SelectedItem.Count > Ranges.CountMin)
                                vm.SelectedItem.Count -= 1;
                            e.Handled = true;
                            break;
                    }
                }
            }
        }

        public void HandleHelperSpacing(SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled && e.KeyModifiers == KeyModifiers.Control)
            {
                if (vm != null)
                {
                    switch (e.Key)
                    {
                        case Key.Z:
                            if (vm.SelectedItem.Size < Ranges.SpacingMax)
                                vm.SelectedItem.Size += 1;
                            e.Handled = true;
                            break;

                        case Key.H:
                            if (vm.SelectedItem.Size > Ranges.SpacingMin)
                                vm.SelectedItem.Size -= 1;
                            e.Handled = true;
                            break;
                    }
                }
            }
        }

        public void HandleHelperThickness(SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled && e.KeyModifiers == KeyModifiers.Control)
            {
                if (vm != null)
                {
                    switch (e.Key)
                    {
                        case Key.E:
                            if (vm.SelectedItem.Thickness < Ranges.ThicknessMax)
                                vm.SelectedItem.Thickness += 1;
                            e.Handled = true;
                            break;

                        case Key.D:
                            if (vm.SelectedItem.Thickness > Ranges.ThicknessMin)
                                vm.SelectedItem.Thickness -= 1;
                            e.Handled = true;
                            break;
                    }
                }
            }
        }

        public void HandleHelperInclination(SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled && e.KeyModifiers == KeyModifiers.Control)
            {
                if (vm != null)
                {
                    switch (e.Key)
                    {
                        case Key.U:
                            if (vm.SelectedItem.InclinationAngle < Ranges.InclinationAngleMax)
                                vm.SelectedItem.InclinationAngle += .1;
                            e.Handled = true;
                            break;

                        case Key.J:
                            if (vm.SelectedItem.InclinationAngle > Ranges.InclinationAngleMin)
                                vm.SelectedItem.InclinationAngle -= .1;
                            e.Handled = true;
                            break;
                    }
                }
            }
        }
    }
}