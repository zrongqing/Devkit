using System.Security.Claims;
using Devkit.Server.Application.Workspace;
using Devkit.Server.Domain.Identity;
using Devkit.Server.Infrastructure.Persistence;
using Devkit.Server.Infrastructure.Identity;
using Devkit.Server.Infrastructure.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Server.Infrastructure.Workspace;

public sealed class WorkspaceAccess(DevkitDbContext db, IHttpContextAccessor context) : IWorkspaceAccess
{
    private Actor? cached;
    public async Task<Actor> CurrentAsync(CancellationToken ct = default)
    {
        if (cached is not null) return cached;
        if (context.HttpContext?.User.Identity?.IsAuthenticated != true || !Guid.TryParse(context.HttpContext.User.FindFirstValue("sub"), out var id))
            throw new BusinessException(401, "unauthorized", "请先登录。");
        var user = await db.Users.AsNoTracking().Include(x=>x.UserRoles).ThenInclude(x=>x.Role).ThenInclude(x=>x.RoleClaims).SingleOrDefaultAsync(x=>x.Id==id,ct)
            ?? throw new BusinessException(401,"unauthorized","账号已不可用。");
        var roles = user.UserRoles.Where(x=>!x.IsDeleted && !x.Role.IsDeleted).Select(x=>x.Role).ToArray();
        var admin = roles.Any(x=>x.Name=="Administrator");
        return cached = new Actor(id, admin, admin ? PermissionCatalog.All : roles.SelectMany(x=>x.RoleClaims).Where(x=>!x.IsDeleted && x.ClaimType=="permission").Select(x=>x.ClaimValue).Distinct().ToArray());
    }
}

internal sealed class AccountAdministration(DevkitDbContext db, UserProfileCache cache) : IAccountAdministration
{
    public async Task<IReadOnlyList<AccountView>> UsersAsync(Actor actor, CancellationToken ct)
    {
        actor.Require("system.identity.manage");
        return (await db.Users.Include(x=>x.UserRoles).ThenInclude(x=>x.Role).OrderBy(x=>x.UserName).ToListAsync(ct)).Select(x=>new AccountView(x.Id,x.UserName,x.Email,x.UserRoles.Where(r=>!r.IsDeleted && !r.Role.IsDeleted).Select(r=>r.Role.Name).ToArray())).ToArray();
    }
    public async Task<IReadOnlyList<RoleView>> RolesAsync(Actor actor, CancellationToken ct)
    {
        actor.Require("system.identity.manage");
        return (await db.Roles.Include(x=>x.RoleClaims).ToListAsync(ct)).Select(x=>new RoleView(x.Id,x.Name,x.Name=="Administrator" ? PermissionCatalog.All : x.RoleClaims.Where(c=>!c.IsDeleted && c.ClaimType=="permission").Select(c=>c.ClaimValue).ToArray())).ToArray();
    }
    public async Task<Guid> CreateAsync(Actor actor, AccountRequest r, CancellationToken ct)
    {
        actor.Require("system.identity.manage");
        if (string.IsNullOrWhiteSpace(r.UserName) || r.RoleIds is null || string.IsNullOrWhiteSpace(r.Password) || r.UserName.Trim().Length is <3 or >100 || !System.Net.Mail.MailAddress.TryCreate(r.Email,out _) || PasswordPolicy.Validate(r.Password) is not null)
            throw new BusinessException(400,"invalid_account","用户名至少 3 字；邮箱须有效；密码至少 12 位并包含大写、小写和数字。");
        var name=r.UserName.Trim().ToUpperInvariant(); var email=r.Email.Trim().ToUpperInvariant();
        if (await db.Users.IgnoreQueryFilters().AnyAsync(x=>x.NormalizedUserName==name || x.NormalizedEmail==email,ct)) throw new BusinessException(409,"account_exists","用户名或邮箱已存在。");
        var roles=await db.Roles.Where(x=>r.RoleIds.Contains(x.Id)).ToListAsync(ct);
        if(!actor.Administrator && roles.Any(x=>x.Name=="Administrator"))throw new BusinessException(403,"forbidden","只有管理员可以创建其他管理员。");
        if(roles.Count!=r.RoleIds.Distinct().Count()) throw new BusinessException(400,"invalid_role","角色不存在。");
        var user=new User {UserName=r.UserName.Trim(),NormalizedUserName=name,Email=r.Email.Trim(),NormalizedEmail=email};
        user.PasswordHash=new PasswordHasher<User>().HashPassword(user,r.Password);
        db.Users.Add(user);
        foreach(var role in roles) db.UserRoles.Add(new UserRole{UserId=user.Id,RoleId=role.Id});
        await db.SaveChangesAsync(ct); return user.Id;
    }
    public async Task<Guid> SaveRoleAsync(Actor actor, Guid? id, RoleRequest r, CancellationToken ct)
    {
        actor.Require("system.identity.manage");
        if(string.IsNullOrWhiteSpace(r.Name) || r.Permissions is null || r.Name.Length>100 || r.Permissions.Except(PermissionCatalog.All).Any()) throw new BusinessException(400,"invalid_role","角色名称或权限无效。");
        var role=id.HasValue ? await db.Roles.Include(x=>x.RoleClaims).SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw new BusinessException(404,"not_found","角色不存在。") : new Role();
        if(role.Name=="Administrator" || r.Name.Equals("Administrator",StringComparison.OrdinalIgnoreCase)) throw new BusinessException(409,"builtin_role","内置管理员角色拥有全部权限，不能编辑。");
        role.Name=r.Name.Trim(); role.NormalizedName=role.Name.ToUpperInvariant();
        if(await db.Roles.IgnoreQueryFilters().AnyAsync(x=>x.NormalizedName==role.NormalizedName && x.Id!=role.Id,ct)) throw new BusinessException(409,"role_exists","角色名已存在。");
        if(!id.HasValue) db.Roles.Add(role);
        foreach(var claim in role.RoleClaims) claim.IsDeleted=true;
        foreach(var p in r.Permissions.Distinct()) db.RoleClaims.Add(new RoleClaim{RoleId=role.Id,ClaimType="permission",ClaimValue=p});
        await db.SaveChangesAsync(ct); return role.Id;
    }
    public async Task AssignAsync(Actor actor, Guid userId, Guid[] roleIds, CancellationToken ct)
    {
        actor.Require("system.identity.manage");
        if(roleIds is null)throw new BusinessException(400,"invalid_role","角色列表不能为 null。");
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>
        {
            await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            var user=await db.Users.SingleOrDefaultAsync(x=>x.Id==userId,ct) ?? throw new BusinessException(404,"not_found","账号不存在。");
            var roles=await db.Roles.Where(x=>roleIds.Contains(x.Id)).ToListAsync(ct);
            if(!actor.Administrator&&(roles.Any(x=>x.Name=="Administrator")||await db.UserRoles.AnyAsync(x=>x.UserId==userId&&x.Role.Name=="Administrator",ct)))throw new BusinessException(403,"forbidden","只有管理员可以修改管理员账号的角色。");
            if(roles.Count!=roleIds.Distinct().Count()) throw new BusinessException(400,"invalid_role","角色不存在。");
            if(!roles.Any(x=>x.Name=="Administrator")) await EnsureAnotherAdminAsync(userId,ct);
            var current=await db.UserRoles.IgnoreQueryFilters().Where(x=>x.UserId==userId).ToListAsync(ct);
            foreach(var link in current) link.IsDeleted=!roleIds.Contains(link.RoleId);
            foreach(var id in roleIds.Distinct().Except(current.Select(x=>x.RoleId))) db.UserRoles.Add(new UserRole{UserId=userId,RoleId=id});
            user.AuthenticationVersion++;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        });
        await cache.RemoveAsync(userId,ct);
    }
    private async Task EnsureAnotherAdminAsync(Guid userId,CancellationToken ct)
    {
        var adminIds=await db.UserRoles.Where(x=>x.Role.Name=="Administrator" && !x.Role.IsDeleted && !x.User.IsDeleted).Select(x=>x.UserId).ToListAsync(ct);
        if(adminIds.Contains(userId) && !adminIds.Any(x=>x!=userId)) throw new BusinessException(409,"last_administrator","必须保留至少一个有效管理员。");
    }
    public async Task DisableAsync(Actor actor, Guid userId, CancellationToken ct)
    {
        actor.Require("system.identity.manage");
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>
        {
            await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            await EnsureAnotherAdminAsync(userId,ct);
            if(!actor.Administrator&&await db.UserRoles.AnyAsync(x=>x.UserId==userId&&x.Role.Name=="Administrator",ct))throw new BusinessException(403,"forbidden","只有管理员可以停用其他管理员。");
            var user=await db.Users.SingleOrDefaultAsync(x=>x.Id==userId,ct) ?? throw new BusinessException(404,"not_found","账号不存在。");
            user.IsDeleted=true; user.AuthenticationVersion++;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        });
        await cache.RemoveAsync(userId,ct);
    }
    public async Task ChangePasswordAsync(Actor actor, ChangePasswordRequest r, CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(r.CurrentPassword) || string.IsNullOrWhiteSpace(r.NewPassword))throw new BusinessException(400,"invalid_password","原密码和新密码不能为空。");
        var user=await db.Users.SingleAsync(x=>x.Id==actor.Id,ct); var hasher=new PasswordHasher<User>();
        if(hasher.VerifyHashedPassword(user,user.PasswordHash,r.CurrentPassword)==PasswordVerificationResult.Failed) throw new BusinessException(400,"invalid_password","原密码不正确。");
        var error=PasswordPolicy.Validate(r.NewPassword); if(error is not null) throw new BusinessException(400,"weak_password",error);
        user.PasswordHash=hasher.HashPassword(user,r.NewPassword); user.AuthenticationVersion++;
        var tokens=await db.RefreshTokens.Where(x=>x.UserId==user.Id && x.RevokedAtUtc==null).ToListAsync(ct);
        foreach(var token in tokens) token.RevokedAtUtc=DateTime.UtcNow;
        await db.SaveChangesAsync(ct); await cache.RemoveAsync(user.Id,ct);
    }
}
