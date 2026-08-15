namespace Devkit.Services.Notifications;

internal interface IWindowsToastRegistration
{
    bool IsAvailable { get; }

    bool EnsureRegistered();
}
