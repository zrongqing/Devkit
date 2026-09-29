using System.Data;
using Devkit.Server.Application.Workspace;
using Devkit.Server.Domain.Identity;
using Devkit.Server.Domain.Navigation;
using Devkit.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Devkit.Server.Infrastructure.Navigation;

internal sealed class WebMenuConfiguration : IEntityTypeConfiguration<WebMenu>
{
    public void Configure(EntityTypeBuilder<WebMenu> b)
    {
        b.ToTable("WebMenus"); b.HasKey(x => x.Id);
        b.Property(x => x.MenuCode).HasMaxLength(120); b.HasAlternateKey(x => x.MenuCode);
        b.Property(x => x.ParentCode).HasMaxLength(120);
        b.HasOne<WebMenu>().WithMany().HasForeignKey(x => x.ParentCode).HasPrincipalKey(x => x.MenuCode).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Type).HasMaxLength(20); b.Property(x => x.Title).HasMaxLength(120);
        b.Property(x => x.RouteKey).HasMaxLength(120); b.Property(x => x.IconKey).HasMaxLength(80);
        b.Property(x => x.Source).HasMaxLength(20); b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
internal sealed class WebMenuStateConfiguration : IEntityTypeConfiguration<WebMenuState>
{
    public void Configure(EntityTypeBuilder<WebMenuState> b)
    {
        b.ToTable("WebMenuState"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Version).IsConcurrencyToken(); b.HasData(new WebMenuState());
    }
}

internal sealed class WebMenuStore(DevkitDbContext db) : IWebMenuStore
{
    public async Task<WebMenuSnapshot> ReadAsync(CancellationToken ct) => new(
        await db.Set<WebMenuState>().AsNoTracking().Where(x => x.Id == 1).Select(x => x.Version).SingleAsync(ct),
        await db.Set<WebMenu>().AsNoTracking().ToListAsync(ct));

    public async Task CommitAsync(int expectedVersion, IReadOnlyList<WebMenu> menus,
        IReadOnlyDictionary<string, string[]>? initialGrants, CancellationToken ct)
    {
        try { await CommitCoreAsync(expectedVersion, menus, initialGrants, ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException(409, "menu_conflict", "菜单已变更，请重新比较或刷新。");
        }
    }

    private async Task CommitCoreAsync(int expectedVersion, IReadOnlyList<WebMenu> menus,
        IReadOnlyDictionary<string, string[]>? initialGrants, CancellationToken ct)
    {
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var state = await db.Set<WebMenuState>().SingleAsync(x => x.Id == 1, ct);
            await db.Entry(state).ReloadAsync(ct);
            if (state.Version != expectedVersion) throw new BusinessException(409, "menu_conflict", "菜单已变更，请重新比较或刷新。");
            var current = await db.Set<WebMenu>().IgnoreQueryFilters().ToListAsync(ct);
            foreach (var menu in menus)
            {
                var old = current.SingleOrDefault(x => x.MenuCode == menu.MenuCode);
                if (old is null) db.Add(menu);
                else
                {
                    if (old.Type != menu.Type || old.RouteKey != menu.RouteKey) throw new BusinessException(400, "invalid_menu", "已使用的菜单编码不能更改类型或页面映射。");
                    menu.Id = old.Id; menu.CreatedAtUtc = old.CreatedAtUtc; menu.CreatedBy = old.CreatedBy;
                    db.Entry(old).CurrentValues.SetValues(menu);
                }
            }
            var codes = menus.Select(x => x.MenuCode).ToHashSet();
            foreach (var removed in current.Where(x => !x.IsDeleted && !codes.Contains(x.MenuCode))) removed.IsDeleted = true;
            if (initialGrants is not null && state.Version == 0)
            {
                var users = await db.UserClaims.Where(x => x.ClaimType == "permission").ToListAsync(ct);
                var roles = await db.RoleClaims.Where(x => x.ClaimType == "permission").ToListAsync(ct);
                foreach (var user in users.GroupBy(x => x.UserId))
                    foreach (var code in user.SelectMany(x => initialGrants.GetValueOrDefault(x.ClaimValue) ?? []).Distinct())
                        db.UserClaims.Add(new UserClaim { UserId = user.Key, ClaimType = "menu:web", ClaimValue = code });
                foreach (var role in roles.GroupBy(x => x.RoleId))
                    foreach (var code in role.SelectMany(x => initialGrants.GetValueOrDefault(x.ClaimValue) ?? []).Distinct())
                        db.RoleClaims.Add(new RoleClaim { RoleId = role.Key, ClaimType = "menu:web", ClaimValue = code });
            }
            state.Version++;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        });
    }
}
