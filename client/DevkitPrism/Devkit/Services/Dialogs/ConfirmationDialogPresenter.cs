using System.Windows;
using Devkit.Views;

namespace Devkit.Services.Dialogs;

internal sealed class ConfirmationDialogPresenter : IConfirmationDialogPresenter
{
    public bool Show(object? owner, ConfirmationDialogOptions options)
    {
        var dialog = new ConfirmationDialog(options)
        {
            Owner = owner as Window
        };

        return dialog.ShowDialog() == true;
    }
}
