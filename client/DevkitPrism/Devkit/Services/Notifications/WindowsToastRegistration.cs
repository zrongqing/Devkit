using Devkit.Services.Interfaces.Logging;
using Syncfusion.UI.Xaml.SfToastNotification;

namespace Devkit.Services.Notifications;

internal sealed class WindowsToastRegistration(IClientLogger logger) : IWindowsToastRegistration
{
    private readonly object _syncRoot = new();
    private bool _registrationAttempted;

    public bool IsAvailable { get; private set; }

    public bool EnsureRegistered()
    {
        lock (_syncRoot)
        {
            if (_registrationAttempted)
            {
                return IsAvailable;
            }

            _registrationAttempted = true;

            try
            {
                WindowsToastBootstrapper.RemoveShortcutOnUnload = true;
                WindowsToastBootstrapper.Initialize("Devkit.App", "Devkit");
                IsAvailable = true;
            }
            catch (Exception exception)
            {
                logger.Warning(exception, "Windows notification registration is unavailable; in-app notifications will be used.");
            }

            return IsAvailable;
        }
    }
}
