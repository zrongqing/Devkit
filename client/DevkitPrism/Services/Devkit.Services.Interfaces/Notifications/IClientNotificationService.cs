namespace Devkit.Services.Interfaces.Notifications;

public interface IClientNotificationService
{
    string Show(NotificationRequest request);

    bool Close(string notificationId);

    void CloseAll();
}
