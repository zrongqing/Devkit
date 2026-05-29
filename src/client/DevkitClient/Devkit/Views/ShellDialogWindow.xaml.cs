using System.Windows.Controls;

using Devkit.Contracts.Views;
using Devkit.ViewModels;

using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;

namespace Devkit.Views;

public partial class ShellDialogWindow : ChromelessWindow, IShellDialogWindow
{
    public ShellDialogWindow(ShellDialogViewModel viewModel)
    {
        InitializeComponent();
        viewModel.SetResult = OnSetResult;
        DataContext = viewModel;
    }

    public Frame GetDialogFrame()
        => dialogFrame;

    private void OnSetResult(bool? result)
    {
        DialogResult = result;
        Close();
    }
}
