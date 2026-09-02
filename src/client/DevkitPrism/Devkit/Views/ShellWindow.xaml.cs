using System.Collections.ObjectModel;
using System.Windows;
using Devkit.Core.UI.Contracts;
using Devkit.Services.Interfaces;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;

namespace Devkit.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class ShellWindow : ChromelessWindow, IShellWindow
{
    public string? ThemeName = Application.Current.Properties["Theme"]?.ToString() != null ? Application.Current.Properties["Theme"]?.ToString() : "Windows11Light";

    public ShellWindow(IFileService fileService)
    {
        SfSkinManager.ApplyThemeAsDefaultStyle = true;
        SfSkinManager.ApplicationTheme = new Theme("Windows11Light");
        InitializeComponent();
    }

    #region IShellWindow Members
    public void ShowWindow()
    {
        Show();
    }

    void IShellWindow.CloseWindow()
    {
        Close();
    }
    #endregion
}

public class MyObservableCollection : ObservableCollection<object>
{
}
