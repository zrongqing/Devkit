namespace Devkit.Services.Dialogs;

internal sealed record ConfirmationDialogOptions(
    string Message,
    string Title,
    string ConfirmText,
    string CancelText);
