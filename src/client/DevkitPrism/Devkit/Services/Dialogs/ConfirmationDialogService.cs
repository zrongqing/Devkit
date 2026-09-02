using Devkit.Services.Interfaces.Dialogs;
using Devkit.Services.Notifications;

namespace Devkit.Services.Dialogs;

internal sealed class ConfirmationDialogService(
    IClientUiContext uiContext,
    IConfirmationDialogPresenter presenter) : IConfirmationDialogService
{
    public Task<bool> ConfirmAsync(
        string message,
        string title = "请确认",
        string confirmText = "确认",
        string cancelText = "取消")
    {
        Validate(message, title, confirmText, cancelText);
        var options = new ConfirmationDialogOptions(
            message.Trim(),
            title.Trim(),
            confirmText.Trim(),
            cancelText.Trim());

        var result = uiContext.Invoke(owner => presenter.Show(owner, options));
        return Task.FromResult(result);
    }

    private static void Validate(string message, string title, string confirmText, string cancelText)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Dialog message cannot be empty.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Dialog title cannot be empty.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(confirmText))
        {
            throw new ArgumentException("Confirm button text cannot be empty.", nameof(confirmText));
        }

        if (string.IsNullOrWhiteSpace(cancelText))
        {
            throw new ArgumentException("Cancel button text cannot be empty.", nameof(cancelText));
        }
    }
}
