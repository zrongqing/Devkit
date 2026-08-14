using Devkit.Services.Interfaces;
using Mapster;
using Ssamc.Configuration;
using Ssamc.Models;
using Microsoft.EntityFrameworkCore;
using Ssamc.DB;

namespace Ssamc.Servers;

public sealed class OracleMenuTreeDataSource : IMenuTreeDataSource
{
    private const string ModuleName = "ssamc";
    private const string SettingsFileName = "menu-tree.json";
    private readonly IFileService _fileService;
    private readonly IModuleStorage _moduleStorage;

    public OracleMenuTreeDataSource(IFileService fileService, IModuleStorage moduleStorage)
    {
        _fileService = fileService;
        _moduleStorage = moduleStorage;
    }

    public async Task<IReadOnlyList<MenuTreeItem>> GetMenuTreeAsync(
        string environmentKey,
        CancellationToken cancellationToken)
    {
        var connectionString = ResolveConnectionString(environmentKey);
        await using var context = new MyDbContext(connectionString);

        var rows = await CreateQuery(context).ToListAsync(cancellationToken);
        var records = rows.Adapt<List<MenuTreeRecord>>(MenuTreeRecordMapping.Configuration);

        return MenuTreeBuilder.Build(records);
    }

    internal string ResolveConnectionString(string environmentKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentKey);

        MenuTreeSettings? settings = null;
        try
        {
            var folderPath = _moduleStorage.GetModulePath(ModuleName);
            settings = _fileService.Read<MenuTreeSettings>(folderPath, SettingsFileName);
        }
        catch
        {
            // An unreadable local file is treated the same as a missing override.
        }

        return SsamcEnvironment.GetMenuDatabaseConnection(
            environmentKey,
            settings?.GetDatabaseConnection(environmentKey));
    }

    internal static IQueryable<MenuTreeQueryRow> CreateQuery(MyDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return
            from menu in context.SYS_MENU.AsNoTracking()
            where menu.IS_DELETE == 0
            join module in context.SYS_MODULE.AsNoTracking()
                on menu.ID_MODULE equals (long?)module.ID into matchingModules
            from module in matchingModules.DefaultIfEmpty()
            select new MenuTreeQueryRow
            {
                MenuId = menu.ID,
                ParentMenuId = menu.ID_TOP,
                Name = menu.STR_NAME,
                EnglishName = menu.STR_NAMEEN,
                Code = menu.STR_CODE,
                ModuleId = menu.ID_MODULE,
                ModuleName = module == null ? null : module.STR_NAME,
                SortOrder = menu.INT_SORT
            };
    }
}
