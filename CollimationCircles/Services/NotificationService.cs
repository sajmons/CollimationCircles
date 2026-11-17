using System;
using Avalonia.Controls.Notifications;

namespace CollimationCircles.Services
{
    public interface INotificationService
    {
        void Initialize(WindowNotificationManager manager);
        void ShowInfo(string title, string message);
        void ShowSuccess(string title, string message);
        void ShowWarning(string title, string message);
        void ShowError(string title, string message);
    }

    public class NotificationService : INotificationService
    {
        private WindowNotificationManager? _manager;

        public void Initialize(WindowNotificationManager manager)
        {
            ArgumentNullException.ThrowIfNull(manager);
            _manager = manager;
        }

        private WindowNotificationManager Manager => _manager
            ?? throw new InvalidOperationException("NotificationService has not been initialized with a window notification manager.");

        public void ShowInfo(string title, string message) =>
            Manager.Show(new Notification(title, message, NotificationType.Information));

        public void ShowSuccess(string title, string message) =>
            Manager.Show(new Notification(title, message, NotificationType.Success));

        public void ShowWarning(string title, string message) =>
            Manager.Show(new Notification(title, message, NotificationType.Warning));

        public void ShowError(string title, string message) =>
            Manager.Show(new Notification(title, message, NotificationType.Error));
    }
}
