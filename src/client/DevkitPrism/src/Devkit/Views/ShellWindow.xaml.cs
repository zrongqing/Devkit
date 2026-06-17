using System.Collections.ObjectModel;
using System.Windows.Controls;
using Devkit.Core.UI.Views;
using Devkit.Services.Interfaces;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;

namespace Devkit.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class ShellWindow : ChromelessWindow, IShellWindow
{
    public static Border _border = null;
    public string themeName = App.Current.Properties["Theme"]?.ToString() != null ? App.Current.Properties["Theme"]?.ToString() : "Windows11Light";

    public ShellWindow(IFileService fileService)
    {
        InitializeComponent();
        SfSkinManager.SetTheme(this, new Theme(themeName));
        var a = fileService;
    }

    #region IShellWindow Members
    public void ShowWindow()
    {
        Show();
    }

    public void CloseWindow()
    {
        Close();
    }
    #endregion
}

public class MyObservableCollection : ObservableCollection<object>
{
}
