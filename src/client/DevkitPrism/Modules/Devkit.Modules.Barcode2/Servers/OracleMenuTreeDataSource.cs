using Mapster;
using Microsoft.EntityFrameworkCore;
using Barcode2.Configuration;
using Barcode2.DB.Context;
using Barcode2.Models;

namespace Barcode2.Servers;

public sealed class OracleMenuTreeDataSource : IMenuTreeDataSource
{
    private readonly IBarcode2ConfigurationService _configuration;

    public OracleMenuTreeDataSource(IBarcode2ConfigurationService configuration)
    {
        _configuration = configuration;
    }

    #region IMenuTreeDataSource Members
    public async Task<IReadOnlyList<MenuTreeItem>> GetMenuTreeAsync(
        string environmentKey,
        CancellationToken cancellationToken)
    {
        var connectionString = await _configuration.GetDatabaseConnectionStringAsync(
            environmentKey,
            cancellationToken);
        await using var context = new MyDbContext(connectionString);

        var rows = await CreateQuery(context).ToListAsync(cancellationToken);
        var records = rows.Adapt<List<MenuTreeRecord>>(MenuTreeRecordMapping.Configuration);

        return MenuTreeBuilder.Build(records);
    }
    #endregion

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
