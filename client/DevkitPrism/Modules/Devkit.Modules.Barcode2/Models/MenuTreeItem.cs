namespace Barcode2.Models;

/// <summary>
/// A transport-neutral menu item supplied by <c> IMenuTreeDataSource </c>.
/// </summary>
public sealed class MenuTreeItem
{
    public long MenuId { get; init; }

    public long? ParentMenuId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Code { get; init; }

    public string? ParentName { get; init; }

    public long? ModuleId { get; init; }

    public string? ModuleName { get; init; }

    public long? MainPageId { get; init; }

    public string? MainPageName { get; init; }

    public short? SortOrder { get; init; }

    public IReadOnlyList<MenuTreeItem> Children { get; init; } = [];
}
