using System.Windows;

namespace Devkit.Services.Notifications;

internal sealed class WpfClientUiContext : IClientUiContext
{
    public T Invoke<T>(Func<object?, T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var application = Application.Current;
        if (application is null)
        {
            return operation(null);
        }

        return application.Dispatcher.CheckAccess()
            ? operation(application.MainWindow)
            : application.Dispatcher.Invoke(() => operation(application.MainWindow));
    }
}
