namespace Devkit.Core.UI.Models;

/// <summary>
/// UI菜单
/// </summary>
public class MenuItemModel
{
    public MenuItemModel()
    {
    }
    public MenuItemModel(string code, string title)
    {
        Code = code;
        ViewCode = code;
        Title = title;
    }
    public MenuItemModel(string code, string title, UIMenuType uiMenuType) : this(code, title)
    {
        UIMenuType = uiMenuType;
    }
    public MenuItemModel(string code, string title, string parentCode, UIMenuType uiMenuType) : this(code, title, uiMenuType)
    {
        ParentCode = parentCode;
    }

    public MenuItemModel(string title, Type type, bool isClosable = true)
    {
    }
    /// <summary>
    /// 菜单标识
    /// </summary>
    /// <remarks> 菜单唯一标识，可以用来区分不同的菜单项，或者作为打开Tab时的参数传递给ViewModel </remarks>
    public string Code { get; set; }
    /// <summary>
    /// 父菜单标识
    /// </summary>
    public string ParentCode { get; set; }
    /// <summary>
    /// 菜单视图唯一标识符
    /// </summary>
    /// <remarks> 该菜单跟那个视图匹配 </remarks>
    public string ViewCode { get; set; }
    /// <summary>
    /// 菜单标题
    /// </summary>
    public string Title { get; set; }
    /// <summary>
    /// 菜单图标加载路径
    /// </summary>
    public string IconPath { get; set; }
    public byte[] Icon { get; set; }
    /// <summary>
    /// 记录该菜单要打开的 ViewModel 的类型
    /// </summary>
    public Type TargetViewModelType { get; set; }
    /// <summary>
    /// 标记该页面是否可关闭（如首页常驻不可关）
    /// </summary>
    public bool IsClosable { get; set; } = true;
    public UIMenuType UIMenuType { get; set; } = UIMenuType.Module;
}
