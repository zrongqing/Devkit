using System.Collections.ObjectModel;
using Devkit.Core.UI.Mvvm;

namespace Devkit.Core.UI.Models;

/// <summary>
/// UI菜单
/// </summary>
public class MenuItemModel : ViewModelBase
{
    /// <summary>
    /// 菜单标识
    /// </summary>
    /// <remarks> 菜单唯一标识，可以用来区分不同的菜单项，或者作为打开Tab时的参数传递给ViewModel </remarks>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// 父菜单标识
    /// </summary>
    public string? ParentId { get; set; }
    /// <summary>
    /// 菜单标题
    /// </summary>
    public string Title { get; set; } = "未命名";
    /// <summary>
    /// 排序
    /// </summary>
    public int Order { get; set; } = 0;
    /// <summary>
    /// 显示
    /// </summary>
    public bool IsVisible { get; set; } = true;
    /// <summary>
    /// View-first 导航时使用的视图名（注册到 Region 的名字）
    /// </summary>
    public string ViewName { get; set; }
    /// <summary>
    /// 记录该菜单要打开的 ViewModel 的类型
    /// </summary>
    public Type ViewModelType { get; set; }
    /// <summary>
    /// 菜单图标加载路径
    /// </summary>
    public string? IconPath { get; set; }
    public byte[] Icon { get; set; }

    /// <summary>
    /// 标记该页面是否可关闭（如首页常驻不可关）
    /// </summary>
    public bool IsClosable { get; set; } = true;
    
    public bool HasChildren => Children?.Any() == true;
    public ObservableCollection<MenuItemModel> Children { get; set; } = new();
}
