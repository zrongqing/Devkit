using CommunityToolkit.Mvvm.ComponentModel;

namespace Devkit.Core.UI.Models;

/// <summary>
/// Tab选项卡
/// </summary>
public partial class UITabItemModel : ObservableObject
{
    /// <summary>
    /// 显示
    /// </summary>
    [ObservableProperty]
    private string _header;
    /// <summary>
    /// 唯一ID
    /// </summary>
    /// <remarks>
    /// menu_code
    /// </remarks>
    [ObservableProperty]
    private string _code;
    /// <summary>
    /// 是否选中
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;
    /// <summary>
    /// 是否能关闭
    /// </summary>
    [ObservableProperty]
    private bool _canClose = true;
    [ObservableProperty]
    private bool _allowPin = true;
    [ObservableProperty]
    private bool _showPin = true;
    [ObservableProperty]
    private System.Windows.Visibility _closeButtonState = System.Windows.Visibility.Visible;

    public object Content { get; set; }

    public UITabItemModel(string code, string header, object content)
    {
        this.Header = header;
        this.Code = code;
        this.Content = content;
    }
}