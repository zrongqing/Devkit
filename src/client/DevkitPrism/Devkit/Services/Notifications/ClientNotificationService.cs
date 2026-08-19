using Devkit.Services.Interfaces.Logging;
using Devkit.Services.Interfaces.Notifications;

namespace Devkit.Services.Notifications;

internal sealed class ClientNotificationService(
    IClientUiContext uiContext,
    IToastNotificationPresenter presenter,
    IWindowsToastRegistration windowsToastRegistration,
    IClientLogger logger) : IClientNotificationService
{
    public string Show(NotificationRequest request)
    {
        var notificationId = Guid.NewGuid().ToString("N");
        _ = ToastOptionsFactory.Create(request, notificationId);

        try
        {
            uiContext.Invoke(host =>
            {
                if (host is null)
                {
                    logger.Warning(null, "A notification could not be displayed because the application window is unavailable.");
                    return false;
                }

                ShowCore(host, request, notificationId);
                return true;
            });
        }
        catch (Exception exception)
        {
            logger.Warning(exception, "A client notification could not be displayed.");
        }

        return notificationId;
    }

    public bool Close(string notificationId)
    {
        if (string.IsNullOrWhiteSpace(notificationId))
        {
            throw new ArgumentException("Notification ID cannot be empty.", nameof(notificationId));
        }

        try
        {
            return uiContext.Invoke(_ => presenter.Close(notificationId));
        }
        catch (Exception exception)
        {
            logger.Warning(exception, "Client notification {NotificationId} could not be closed.", notificationId);
            return false;
        }
    }

    public void CloseAll()
    {
        try
        {
            uiContext.Invoke(_ =>
            {
                presenter.CloseAll();
                return true;
            });
        }
        catch (Exception exception)
        {
            logger.Warning(exception, "Client notifications could not be closed.");
        }
    }

    private void ShowCore(object host, NotificationRequest request, string notificationId)
    {
        if (request.Delivery == NotificationDelivery.Windows && !windowsToastRegistration.EnsureRegistered())
        {
            ShowInApp(host, request, notificationId);
            return;
        }

        try
        {
            presenter.Show(host, ToastOptionsFactory.Create(request, notificationId));
        }
        catch (Exception exception) when (request.Delivery == NotificationDelivery.Windows)
        {
            logger.Warning(exception, "A Windows notification failed; the notification will be shown in the client.");
            ShowInApp(host, request, notificationId);
        }
    }

    private void ShowInApp(object host, NotificationRequest request, string notificationId) =>
        presenter.Show(
            host,
            ToastOptionsFactory.Create(request with { Delivery = NotificationDelivery.InApp }, notificationId));
}
