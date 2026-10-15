using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System.Windows.Input;

namespace CollimationCircles.Controls
{
    public static class PointerHoldCommand
    {
        private static readonly AttachedProperty<bool> IsPointerDownProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("IsPointerDown", typeof(PointerHoldCommand));

        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(PointerHoldCommand));

        public static readonly AttachedProperty<ICommand?> PressedCommandProperty =
            AvaloniaProperty.RegisterAttached<Control, ICommand?>("PressedCommand", typeof(PointerHoldCommand));

        public static readonly AttachedProperty<object?> PressedCommandParameterProperty =
            AvaloniaProperty.RegisterAttached<Control, object?>("PressedCommandParameter", typeof(PointerHoldCommand));

        public static readonly AttachedProperty<ICommand?> ReleasedCommandProperty =
            AvaloniaProperty.RegisterAttached<Control, ICommand?>("ReleasedCommand", typeof(PointerHoldCommand));

        public static readonly AttachedProperty<object?> ReleasedCommandParameterProperty =
            AvaloniaProperty.RegisterAttached<Control, object?>("ReleasedCommandParameter", typeof(PointerHoldCommand));

        static PointerHoldCommand()
        {
            IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
        }

        public static bool GetIsEnabled(Control element) => element.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(Control element, bool value) => element.SetValue(IsEnabledProperty, value);

        public static ICommand? GetPressedCommand(Control element) => element.GetValue(PressedCommandProperty);

        public static void SetPressedCommand(Control element, ICommand? value) => element.SetValue(PressedCommandProperty, value);

        public static object? GetPressedCommandParameter(Control element) => element.GetValue(PressedCommandParameterProperty);

        public static void SetPressedCommandParameter(Control element, object? value) => element.SetValue(PressedCommandParameterProperty, value);

        public static ICommand? GetReleasedCommand(Control element) => element.GetValue(ReleasedCommandProperty);

        public static void SetReleasedCommand(Control element, ICommand? value) => element.SetValue(ReleasedCommandProperty, value);

        public static object? GetReleasedCommandParameter(Control element) => element.GetValue(ReleasedCommandParameterProperty);

        public static void SetReleasedCommandParameter(Control element, object? value) => element.SetValue(ReleasedCommandParameterProperty, value);

        private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is not bool isEnabled)
            {
                return;
            }

            if (isEnabled)
            {
                control.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, handledEventsToo: true);
                control.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, handledEventsToo: true);
                control.AddHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost, handledEventsToo: true);
            }
            else
            {
                control.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
                control.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
                control.RemoveHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost);
            }
        }

        private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control control)
            {
                return;
            }

            if (control.GetValue(IsPointerDownProperty))
            {
                e.Handled = true;
                return;
            }

            control.SetValue(IsPointerDownProperty, true);

            e.Pointer.Capture(control);

            var command = GetPressedCommand(control);
            var parameter = GetPressedCommandParameter(control);

            if (command?.CanExecute(parameter) == true)
            {
                command.Execute(parameter);
            }

            e.Handled = true;
        }

        private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (sender is not Control control)
            {
                return;
            }

            if (!TryEndHold(control))
            {
                e.Handled = true;
                return;
            }

            if (e.Pointer.Captured == control)
            {
                e.Pointer.Capture(null);
            }

            ExecuteRelease(control);
            e.Handled = true;
        }

        private static void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (sender is Control control && TryEndHold(control))
            {
                ExecuteRelease(control);
            }
        }

        private static bool TryEndHold(Control control)
        {
            if (!control.GetValue(IsPointerDownProperty))
            {
                return false;
            }

            control.SetValue(IsPointerDownProperty, false);
            return true;
        }

        private static void ExecuteRelease(Control control)
        {
            var command = GetReleasedCommand(control);
            var parameter = GetReleasedCommandParameter(control);

            if (command?.CanExecute(parameter) == true)
            {
                command.Execute(parameter);
            }
        }
    }
}
