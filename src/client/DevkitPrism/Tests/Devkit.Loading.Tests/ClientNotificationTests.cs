using Devkit.Services.Dialogs;
using Devkit.Services.Interfaces.Dialogs;
using Devkit.Services.Interfaces;
using Devkit.Services.Interfaces.Logging;
using Devkit.Services.Interfaces.Notifications;
using Devkit.Services.Notifications;
using Moq;
using Syncfusion.UI.Xaml.SfToastNotification;
using Ssamc.ViewModels;
using Xunit;

namespace Devkit.Loading.Tests;

public sealed class ClientNotificationTests
{
    [Theory]
    [InlineData(NotificationLevel.Debug, "调试", ToastSeverity.Info, "#FF59636E", 3, false)]
    [InlineData(NotificationLevel.Info, "信息", ToastSeverity.Info, "#FF0969DA", 5, false)]
    [InlineData(NotificationLevel.Warning, "警告", ToastSeverity.Warning, "#FF9A6700", 8, false)]
    [InlineData(NotificationLevel.Error, "错误", ToastSeverity.Error, "#FFD1242F", 0, true)]
    public void Factory_maps_level_visuals_and_default_lifetime(
        NotificationLevel level,
        string title,
        ToastSeverity severity,
        string accentColor,
        int durationSeconds,
        bool preventAutoClose)
    {
        var options = ToastOptionsFactory.Create(
            new NotificationRequest { Message = "message", Level = level },
            "notification-id");

        Assert.Equal("notification-id", options.Id);
        Assert.Equal(title, options.Title);
        Assert.Equal(severity, options.Severity);
        Assert.Equal(accentColor, options.AccentBrush.Color.ToString());
        Assert.Equal(TimeSpan.FromSeconds(durationSeconds), options.Duration);
        Assert.Equal(preventAutoClose, options.PreventAutoClose);
        Assert.Equal(ToastVariant.Outlined, options.Variant);
        Assert.True(options.ShowCloseButton);
    }

    [Theory]
    [InlineData(NotificationPlacement.TopRight, ToastPlacement.TopRight)]
    [InlineData(NotificationPlacement.BottomRight, ToastPlacement.BottomRight)]
    public void Factory_maps_supported_window_placements(
        NotificationPlacement placement,
        ToastPlacement expected)
    {
        var options = ToastOptionsFactory.Create(
            new NotificationRequest { Message = "message", Placement = placement },
            "id");

        Assert.Equal(expected, options.Placement);
        Assert.Equal(ToastMode.Window, options.Mode);
    }

    [Fact]
    public void Factory_maps_windows_delivery_to_native_mode()
    {
        var options = ToastOptionsFactory.Create(
            new NotificationRequest
            {
                Message = "message",
                Delivery = NotificationDelivery.Windows
            },
            "id");

        Assert.Equal(ToastMode.Default, options.Mode);
    }

    [Fact]
    public void Factory_honors_custom_duration_and_keep_open()
    {
        var timed = ToastOptionsFactory.Create(
            new NotificationRequest
            {
                Message = "timed",
                Level = NotificationLevel.Error,
                AutoCloseAfter = TimeSpan.FromSeconds(12)
            },
            "timed");
        var persistent = ToastOptionsFactory.Create(
            new NotificationRequest
            {
                Message = "persistent",
                Level = NotificationLevel.Info,
                KeepOpen = true
            },
            "persistent");

        Assert.Equal(TimeSpan.FromSeconds(12), timed.Duration);
        Assert.False(timed.PreventAutoClose);
        Assert.True(persistent.PreventAutoClose);
    }

    [Fact]
    public void Factory_rejects_invalid_requests()
    {
        Assert.Throws<ArgumentException>(() => ToastOptionsFactory.Create(
            new NotificationRequest { Message = " " },
            "id"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ToastOptionsFactory.Create(
            new NotificationRequest { Message = "message", AutoCloseAfter = TimeSpan.Zero },
            "id"));
        Assert.Throws<ArgumentException>(() => ToastOptionsFactory.Create(
            new NotificationRequest
            {
                Message = "message",
                KeepOpen = true,
                AutoCloseAfter = TimeSpan.FromSeconds(1)
            },
            "id"));
    }

    [Fact]
    public void Windows_delivery_failure_is_logged_and_falls_back_in_app()
    {
        var presenter = new RecordingToastPresenter { ThrowForNativeToast = true };
        var registration = new FakeWindowsToastRegistration { IsAvailable = true };
        var logger = new Mock<IClientLogger>();
        var service = new ClientNotificationService(
            new ImmediateUiContext(new object()),
            presenter,
            registration,
            logger.Object);

        service.Show(new NotificationRequest
        {
            Message = "message",
            Delivery = NotificationDelivery.Windows
        });

        Assert.Equal(ToastMode.Window, Assert.Single(presenter.Shown).Mode);
        logger.Verify(value => value.Warning(
            It.IsAny<Exception>(),
            It.Is<string>(message => message.Contains("Windows notification")),
            It.IsAny<object?[]>()), Times.Once);
    }

    [Fact]
    public void Close_and_close_all_delegate_through_the_ui_context()
    {
        var presenter = new RecordingToastPresenter { CloseResult = true };
        var uiContext = new ImmediateUiContext(new object());
        var service = new ClientNotificationService(
            uiContext,
            presenter,
            new FakeWindowsToastRegistration(),
            Mock.Of<IClientLogger>());

        Assert.True(service.Close("notification-id"));
        service.CloseAll();

        Assert.Equal("notification-id", presenter.ClosedId);
        Assert.True(presenter.CloseAllCalled);
        Assert.Equal(2, uiContext.InvocationCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Confirmation_service_returns_presenter_result(bool expected)
    {
        var presenter = new RecordingConfirmationPresenter { Result = expected };
        IConfirmationDialogService service = new ConfirmationDialogService(
            new ImmediateUiContext(new object()),
            presenter);

        var result = await service.ConfirmAsync(
            " message ",
            " title ",
            " confirm ",
            " cancel ");

        Assert.Equal(expected, result);
        Assert.Equal(
            new ConfirmationDialogOptions("message", "title", "confirm", "cancel"),
            presenter.Options);
    }

    [Fact]
    public void Webapp_missing_source_configuration_reports_warning()
    {
        var notifications = new Mock<IClientNotificationService>();
        notifications.Setup(service => service.Show(It.IsAny<NotificationRequest>()))
            .Returns("notification-id");
        var viewModel = new WebappUpdateViewModel(
            Mock.Of<IFileService>(),
            Mock.Of<IModuleStorage>(),
            notifications.Object)
        {
            WebappPath = string.Empty
        };

        viewModel.UpdateFilesCommand.Execute("test");

        notifications.Verify(service => service.Show(It.Is<NotificationRequest>(request =>
            request.Level == NotificationLevel.Warning &&
            request.Message.Contains("webapp"))), Times.Once);
    }

    private sealed class ImmediateUiContext(object? host) : IClientUiContext
    {
        public int InvocationCount { get; private set; }

        public T Invoke<T>(Func<object?, T> operation)
        {
            InvocationCount++;
            return operation(host);
        }
    }

    private sealed class RecordingToastPresenter : IToastNotificationPresenter
    {
        public List<ToastOptions> Shown { get; } = [];

        public bool ThrowForNativeToast { get; init; }

        public bool CloseResult { get; init; }

        public string? ClosedId { get; private set; }

        public bool CloseAllCalled { get; private set; }

        public void Show(object host, ToastOptions options)
        {
            if (ThrowForNativeToast && options.Mode == ToastMode.Default)
            {
                throw new InvalidOperationException("Native toast unavailable.");
            }

            Shown.Add(options);
        }

        public bool Close(string notificationId)
        {
            ClosedId = notificationId;
            return CloseResult;
        }

        public void CloseAll() => CloseAllCalled = true;
    }

    private sealed class FakeWindowsToastRegistration : IWindowsToastRegistration
    {
        public bool IsAvailable { get; init; }

        public bool EnsureRegistered() => IsAvailable;
    }

    private sealed class RecordingConfirmationPresenter : IConfirmationDialogPresenter
    {
        public bool Result { get; init; }

        public ConfirmationDialogOptions? Options { get; private set; }

        public bool Show(object? owner, ConfirmationDialogOptions options)
        {
            Options = options;
            return Result;
        }
    }
}
