using System.Windows.Media;
using Devkit.Services.Interfaces.Notifications;
using Syncfusion.UI.Xaml.SfToastNotification;
using ClientNotificationLevel = Devkit.Services.Interfaces.Notifications.NotificationLevel;
using ClientNotificationPlacement = Devkit.Services.Interfaces.Notifications.NotificationPlacement;

namespace Devkit.Services.Notifications;

internal static class ToastOptionsFactory
{
    public static ToastOptions Create(NotificationRequest request, string notificationId)
    {
        Validate(request);

        var (title, severity, accentBrush, defaultDuration) = request.Level switch
        {
            ClientNotificationLevel.Debug => ("调试", ToastSeverity.Info, Brush(0x59, 0x63, 0x6E), TimeSpan.FromSeconds(3)),
            ClientNotificationLevel.Info => ("信息", ToastSeverity.Info, Brush(0x09, 0x69, 0xDA), TimeSpan.FromSeconds(5)),
            ClientNotificationLevel.Warning => ("警告", ToastSeverity.Warning, Brush(0x9A, 0x67, 0x00), TimeSpan.FromSeconds(8)),
            ClientNotificationLevel.Error => ("错误", ToastSeverity.Error, Brush(0xD1, 0x24, 0x2F), Timeout.InfiniteTimeSpan),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Level), request.Level, "Unsupported notification level.")
        };

        var preventAutoClose = request.KeepOpen ||
                               (request.AutoCloseAfter is null && defaultDuration == Timeout.InfiniteTimeSpan);
        var duration = request.AutoCloseAfter ??
                       (defaultDuration == Timeout.InfiniteTimeSpan ? TimeSpan.Zero : defaultDuration);

        return new ToastOptions
        {
            Id = notificationId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? title : request.Title.Trim(),
            Message = request.Message.Trim(),
            Mode = request.Delivery == NotificationDelivery.Windows ? ToastMode.Default : ToastMode.Window,
            Placement = request.Placement == ClientNotificationPlacement.TopRight
                ? ToastPlacement.TopRight
                : ToastPlacement.BottomRight,
            Severity = severity,
            Variant = ToastVariant.Outlined,
            AccentBrush = accentBrush,
            ShowCloseButton = true,
            ShowActionButtons = false,
            ShowAnimationType = ToastAnimation.FadeIn,
            CloseAnimationType = ToastAnimation.FadeOut,
            Duration = duration,
            PreventAutoClose = preventAutoClose
        };
    }

    private static void Validate(NotificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Notification message cannot be empty.", nameof(request));
        }

        if (!Enum.IsDefined(request.Level))
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Level, "Unsupported notification level.");
        }

        if (!Enum.IsDefined(request.Placement))
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Placement, "Unsupported notification placement.");
        }

        if (!Enum.IsDefined(request.Delivery))
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Delivery, "Unsupported notification delivery target.");
        }

        if (request.AutoCloseAfter is { } duration && duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(request), duration, "Auto-close duration must be positive.");
        }

        if (request.KeepOpen && request.AutoCloseAfter is not null)
        {
            throw new ArgumentException("KeepOpen and AutoCloseAfter cannot be used together.", nameof(request));
        }
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
