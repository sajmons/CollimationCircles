using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CollimationCircles.ViewModels;

namespace CollimationCircles.Services
{
    public class MoveWindowService : IMoveWindowService
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

        public void HandleRotation(SettingsViewModel? vm, KeyEventArgs e)
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
                        case Key.L:
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

        public void HandleScale(SettingsViewModel? vm, KeyEventArgs e)
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
    }
}
