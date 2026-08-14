using Mapster;

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

    public long? MainPageId { get; init; }

    public string? MainPageName { get; init; }

    public short? SortOrder { get; init; }
}

internal sealed class MenuTreeQueryRow
{
    public long MenuId { get; init; }

    public long? ParentMenuId { get; init; }

    public string? Name { get; init; }

    public string? EnglishName { get; init; }

    public string? Code { get; init; }

    public long? ModuleId { get; init; }

    public string? ModuleName { get; init; }

    public long? MainPageId { get; init; }

    public string? MainPageName { get; init; }

    public short? SortOrder { get; init; }
}

public sealed class MenuTreeRecordMapping : IRegister
{
    internal static TypeAdapterConfig Configuration { get; } = CreateConfiguration();

    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<MenuTreeQueryRow, MenuTreeRecord>();
    }

    private static TypeAdapterConfig CreateConfiguration()
    {
        var config = new TypeAdapterConfig();
        new MenuTreeRecordMapping().Register(config);
        config.Compile();
        return config;
    }
}
