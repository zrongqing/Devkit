namespace Devkit.Services.Dialogs;

internal interface IConfirmationDialogPresenter
{
    bool Show(object? owner, ConfirmationDialogOptions options);
}
