namespace Devkit.Services.Interfaces.Notifications;

public sealed record NotificationRequest
{
    public required string Message { get; init; }

    public string? Title { get; init; }

    public NotificationLevel Level { get; init; } = NotificationLevel.Info;

    public NotificationPlacement Placement { get; init; } = NotificationPlacement.TopRight;

    public NotificationDelivery Delivery { get; init; } = NotificationDelivery.InApp;

    /// <summary>
    /// Overrides the level-specific default duration. Leave unset to use the default.
    /// </summary>
    public TimeSpan? AutoCloseAfter { get; init; }

    /// <summary>
    /// Keeps an in-app notification visible until it is explicitly closed.
    /// </summary>
    public bool KeepOpen { get; init; }
}
