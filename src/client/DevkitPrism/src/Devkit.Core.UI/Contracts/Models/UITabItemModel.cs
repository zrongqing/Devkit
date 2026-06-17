using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Devkit.Core.UI.Models;

/// <summary>
/// Tab选项卡
/// </summary>
public partial class UITabItemModel : ObservableObject
{
    [ObservableProperty]
    private bool _allowPin = true;
    /// <summary>
    /// 是否能关闭
    /// </summary>
    [ObservableProperty]
    private bool _canClose = true;
    [ObservableProperty]
    private Visibility _closeButtonState = Visibility.Visible;
    /// <summary>
    /// 唯一ID
    /// </summary>
    /// <remarks>
    /// menu_code
    /// </remarks>
    [ObservableProperty]
    private string _code;
    /// <summary>
    /// 显示
    /// </summary>
    [ObservableProperty]
    private string _header;
    /// <summary>
    /// 是否选中
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;
    [ObservableProperty]
    private bool _showPin = true;

    public UITabItemModel(string code, string header, object content)
    {
        Header = header;
        Code = code;
        Content = content;
    }

    public object Content { get; set; }
}
