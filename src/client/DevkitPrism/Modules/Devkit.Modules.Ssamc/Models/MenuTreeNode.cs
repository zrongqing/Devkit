using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ssamc.Models;

public partial class MenuTreeNode : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;
    public long MenuId { get; init; }

    public long? ParentMenuId { get; init; }

    public required string Name { get; init; }

    public string? Code { get; init; }

    public string? ParentName { get; init; }

    public long? ModuleId { get; init; }

    public string? ModuleName { get; init; }

    public long? MainPageId { get; init; }

    public string? MainPageName { get; init; }

    public bool CanOpen => MainPageId > 0;

    public MenuDetails Details => new()
    {
        MenuId = MenuId,
        MenuName = Name,
        MenuCode = Code ?? string.Empty,
        ParentDirectory = ParentName ?? string.Empty,
        ModuleName = ModuleName ?? string.Empty,
        ModuleId = ModuleId?.ToString() ?? string.Empty,
        MainPageId = MainPageId?.ToString() ?? string.Empty,
        MainPageName = MainPageName ?? string.Empty
    };

    public ObservableCollection<MenuTreeNode> Children { get; init; } = [];
}
