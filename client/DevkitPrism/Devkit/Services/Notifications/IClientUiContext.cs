namespace Devkit.Services.Notifications;

internal interface IClientUiContext
{
    T Invoke<T>(Func<object?, T> operation);
}
