namespace Ssamc.Models;

public sealed class MenuTreeRecord
{
    public long MenuId { get; init; }

    public long? ParentMenuId { get; init; }

    public string? Name { get; init; }

    public string? EnglishName { get; init; }

    public string? Code { get; init; }

    public long? ModuleId { get; init; }

    public string? ModuleName { get; init; }

    public short? SortOrder { get; init; }
}
