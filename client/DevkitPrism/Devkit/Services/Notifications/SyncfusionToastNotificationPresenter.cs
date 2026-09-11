using System.Windows;
using Syncfusion.UI.Xaml.SfToastNotification;

namespace Devkit.Services.Notifications;

internal sealed class SyncfusionToastNotificationPresenter : IToastNotificationPresenter
{
    public void Show(object host, ToastOptions options)
    {
        if (host is not DependencyObject dependencyObject)
        {
            throw new InvalidOperationException("The notification host is not a WPF dependency object.");
        }

        SfToastNotification.Show(dependencyObject, options);
    }

    public bool Close(string notificationId) => SfToastNotification.Close(notificationId);

    public void CloseAll() => SfToastNotification.CloseAll();
}
