using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Ssamc.Models;

public sealed class MenuDetails
{
    [Category("菜单信息")]
    [Display(Order = 0)]
    [DisplayName("菜单 ID")]
    [ReadOnly(true)]
    public long MenuId { get; init; }

    [Category("菜单信息")]
    [Display(Order = 1)]
    [DisplayName("菜单名字")]
    [ReadOnly(true)]
    public string MenuName { get; init; } = string.Empty;

    [Category("菜单信息")]
    [Display(Order = 2)]
    [DisplayName("菜单编码")]
    [ReadOnly(true)]
    public string MenuCode { get; init; } = string.Empty;

    [Category("菜单信息")]
    [Display(Order = 3)]
    [DisplayName("菜单上级目录")]
    [ReadOnly(true)]
    public string ParentDirectory { get; init; } = string.Empty;

    [Category("菜单模块")]
    [Display(Order = 10)]
    [DisplayName("模块名字")]
    [ReadOnly(true)]
    public string ModuleName { get; init; } = string.Empty;

    [Category("菜单模块")]
    [Display(Order = 11)]
    [DisplayName("模块 ID")]
    [ReadOnly(true)]
    public string ModuleId { get; init; } = string.Empty;

    [Category("模块主页面")]
    [Display(Order = 20)]
    [DisplayName("主页面 ID")]
    [ReadOnly(true)]
    public string MainPageId { get; init; } = string.Empty;

    [Category("模块主页面")]
    [Display(Order = 21)]
    [DisplayName("主页面名字")]
    [ReadOnly(true)]
    public string MainPageName { get; init; } = string.Empty;
}
