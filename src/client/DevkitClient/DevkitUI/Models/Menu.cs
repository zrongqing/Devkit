using System;
using System.Collections.ObjectModel;

namespace DevkitCore.Models;

public enum MenuType
{
    Module = 1, // 模块
    Menu = 2,  // 菜单
}

/// <summary>
/// 菜单类
/// </summary>
public class Menu
{
    /// <summary>
    /// 菜单唯一标识符，可以用来区分不同的菜单项，或者作为打开Tab时的参数传递给ViewModel
    /// </summary>
    public string Code { get; set; }
    /// <summary>
    /// 父菜单
    /// </summary>
    public string ParentCode { get; set; }

    /// <summary>
    /// 菜单视图唯一标识符
    /// </summary>
    public string ViewCode { get; set; }

    /// <summary>
    /// 菜单标题
    /// </summary>
    public string Header { get; set; }
    /// <summary>
    /// 菜单图标加载路径
    /// </summary>
    public string IconPath { get; set; }
    public MenuType MenuType { get; set; } = MenuType.Module;


    protected Menu()
    {

    }
    public Menu(string code, string header)
    {
        Code = code;
        ViewCode = code;
        Header = header;
    }
    public Menu(string code, string header, MenuType menuType):this(code, header)
    {
        MenuType = menuType;
    }
    public Menu(string code, string header, string parentCode, MenuType menuType) :this(code, header, menuType)
    {
        ParentCode = parentCode;
    }
}
