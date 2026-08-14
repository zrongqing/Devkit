using Ssamc.Configuration;
using Ssamc.Models;
using Microsoft.EntityFrameworkCore;
using Ssamc.DB;

namespace Ssamc.Servers;

public sealed class OracleMenuTreeDataSource : IMenuTreeDataSource
{
    public async Task<IReadOnlyList<MenuTreeItem>> GetMenuTreeAsync(
        CancellationToken cancellationToken)
    {
        var connectionString = SsamcEnvironment.GetMenuDatabaseConnection();
        await using var context = new MyDbContext(connectionString);

        var records = await CreateQuery(context).ToListAsync(cancellationToken);

        return MenuTreeBuilder.Build(records);
    }

    internal static IQueryable<MenuTreeRecord> CreateQuery(MyDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return
            from menu in context.SYS_MENU.AsNoTracking()
            where menu.IS_DELETE == 0
            join module in context.SYS_MODULE.AsNoTracking()
                on menu.ID_MODULE equals (long?)module.ID into matchingModules
            from module in matchingModules.DefaultIfEmpty()
            select new MenuTreeRecord
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
