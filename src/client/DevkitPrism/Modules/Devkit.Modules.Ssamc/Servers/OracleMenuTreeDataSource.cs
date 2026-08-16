using Devkit.Services.Interfaces;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Ssamc.Configuration;
using Ssamc.DB.Context;
using Ssamc.Models;

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

    #region IMenuTreeDataSource Members
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
    #endregion

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
                on menu.ID_MODULE equals module.ID into matchingModules
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
                MainPageId = module == null
                                 ? null
                                 : context.SYS_PAGE
                                     .Where(page => page.ID_MODULE == module.ID && page.IS_MAIN == true)
                                     .OrderBy(page => page.INT_SORT)
                                     .ThenBy(page => page.ID)
                                     .Select(page => (long?)page.ID)
                                     .FirstOrDefault(),
                MainPageName = module == null
                                   ? null
                                   : context.SYS_PAGE
                                       .Where(page => page.ID_MODULE == module.ID && page.IS_MAIN == true)
                                       .OrderBy(page => page.INT_SORT)
                                       .ThenBy(page => page.ID)
                                       .Select(page => page.STR_NAME)
                                       .FirstOrDefault(),
                SortOrder = menu.INT_SORT
            };
    }
}
