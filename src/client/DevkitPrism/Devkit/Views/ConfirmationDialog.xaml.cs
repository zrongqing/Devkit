using System.Windows;
using Devkit.Services.Dialogs;
using Syncfusion.Windows.Shared;

namespace Devkit.Views;

public partial class ConfirmationDialog : ChromelessWindow
{
    internal ConfirmationDialog(ConfirmationDialogOptions options)
    {
        InitializeComponent();

        Title = options.Title;
        TitleTextBlock.Text = options.Title;
        MessageTextBlock.Text = options.Message;
        ConfirmButton.Content = options.ConfirmText;
        CancelButton.Content = options.CancelText;
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void CancelButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
