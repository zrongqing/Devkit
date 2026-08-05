using System.Windows;
using Devkit.Services.Interfaces.Logging;

namespace Devkit.Services.Diagnostics;

/// <summary>
/// Records otherwise unhandled exceptions before the client exits.
/// </summary>
public sealed class ClientCrashHandler(IClientLogger logger)
{
    private int _isHandling;

    public void HandleDispatcherException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        if (Interlocked.Exchange(ref _isHandling, 1) != 0)
        {
            return;
        }

        try
        {
            logger.Critical(eventArgs.Exception, "An unhandled exception occurred on the UI thread.");
            MessageBox.Show(
                "客户端发生未处理错误，即将退出。详细信息已保存到日志文件。",
                "Devkit",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            eventArgs.Handled = true;
            Application.Current.Shutdown(-1);
        }
        finally
        {
            Interlocked.Exchange(ref _isHandling, 0);
        }
    }

    public void HandleAppDomainException(object? sender, UnhandledExceptionEventArgs eventArgs)
    {
        logger.Critical(
            eventArgs.ExceptionObject as Exception ?? new InvalidOperationException("Unknown unhandled exception."),
            "An unhandled exception caused process termination. IsTerminating: {IsTerminating}",
            eventArgs.IsTerminating);
    }

    public void HandleUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        logger.Error(eventArgs.Exception, "An unobserved task exception occurred.");
        eventArgs.SetObserved();
    }
}
