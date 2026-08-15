using Syncfusion.UI.Xaml.SfToastNotification;

namespace Devkit.Services.Notifications;

internal interface IToastNotificationPresenter
{
    void Show(object host, ToastOptions options);

    bool Close(string notificationId);

    void CloseAll();
}
