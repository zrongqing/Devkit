using System.ComponentModel;

namespace Ssamc.Models;

[Category("菜单详情")]
public sealed class MenuDetails
{
    [DisplayName("菜单 ID")]
    [ReadOnly(true)]
    public long MenuId { get; init; }

    [DisplayName("菜单名字")]
    [ReadOnly(true)]
    public string MenuName { get; init; } = string.Empty;

    [DisplayName("菜单编码")]
    [ReadOnly(true)]
    public string MenuCode { get; init; } = string.Empty;

    [DisplayName("菜单上级目录")]
    [ReadOnly(true)]
    public string ParentDirectory { get; init; } = string.Empty;

    [DisplayName("模块名字")]
    [ReadOnly(true)]
    public string ModuleName { get; init; } = string.Empty;

    [DisplayName("模块 ID")]
    [ReadOnly(true)]
    public string ModuleId { get; init; } = string.Empty;
}
