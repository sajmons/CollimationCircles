using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CollimationCircles.ViewModels;

namespace CollimationCircles.Services
{
    public class KeyHandlingService : IKeyHandlingService
    {
        public void HandleMovement(Window window, SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled)
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

                    vm.Position = window.Position;
                }
            }
        }

        public void HandleGlobalRotation(SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                if (vm != null)
                {
                    double rotation = vm.RotationAngle;

                    switch (e.Key)
                    {
                        case Key.R:
                            rotation += 1;
                            e.Handled = true;
                            break;
                        case Key.F:
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
            if (!e.Handled)
            {
                if (vm != null)
                {
                    double increment = vm.Scale;

                    switch (e.Key)
                    {
                        case Key.Add:
                        case Key.OemPlus:
                            increment += 0.01;
                            e.Handled = true;
                            break;

                        case Key.Subtract:
                        case Key.OemMinus:
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
            if (!e.Handled)
            {
                if (vm != null)
                {
                    switch (e.Key)
                    {                        
                        case Key.W:
                            vm.SelectedItem.Radius += 1;
                            e.Handled = true;
                            break;
                        
                        case Key.S:
                            vm.SelectedItem.Radius -= 1;
                            e.Handled = true;
                            break;
                    }
                }
            }
        }

        public void HandleHelperRotation(SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                if (vm != null)
                {
                    switch (e.Key)
                    {
                        case Key.Q:
                            vm.SelectedItem.RotationAngle -= 1;
                            e.Handled = true;
                            break;

                        case Key.A:
                            vm.SelectedItem.RotationAngle += 1;
                            e.Handled = true;
                            break;
                    }                    
                }
            }
        }

        public void HandleHelperCount(SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                if (vm != null)
                {
                    switch (e.Key)
                    {
                        case Key.T:
                            vm.SelectedItem.Count -= 1;
                            e.Handled = true;
                            break;

                        case Key.G:
                            vm.SelectedItem.Count += 1;
                            e.Handled = true;
                            break;
                    }
                }
            }
        }

        public void HandleHelperSpacing(SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                if (vm != null)
                {
                    switch (e.Key)
                    {
                        case Key.Z:
                            vm.SelectedItem.Size += 1;
                            e.Handled = true;
                            break;

                        case Key.H:
                            vm.SelectedItem.Size -= 1;
                            e.Handled = true;
                            break;
                    }
                }
            }
        }

        public void HandleHelperThickness(SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                if (vm != null)
                {
                    switch (e.Key)
                    {
                        case Key.E:
                            vm.SelectedItem.Thickness += 1;
                            e.Handled = true;
                            break;

                        case Key.D:
                            vm.SelectedItem.Thickness -= 1;
                            e.Handled = true;
                            break;
                    }                    
                }
            }
        }
    }
}