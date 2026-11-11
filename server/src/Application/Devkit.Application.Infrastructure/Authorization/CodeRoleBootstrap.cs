using System.Security.Cryptography;
using System.Text;
using Devkit.Application.Application.Authorization;
using Devkit.Application.Domain.Identity;
using Devkit.Application.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Devkit.Application.Infrastructure.Authorization;

public sealed record CodeRoleSeed(string Key, Guid Id, string Name, string[] Permissions);
public sealed class CodeRoleBootstrap(ApplicationDbContext db, IConfiguration configuration, DeploymentPermissions catalog)
{
    public async Task EnsureAsync(string key, string suppliedKey, CancellationToken ct)
    {
        var secret = configuration["CodeSystem:BootstrapKey"];
        if (secret is not { Length: >= 32 } || !CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(secret)), SHA256.HashData(Encoding.UTF8.GetBytes(suppliedKey))))
            throw new IdentityException(401, "service_unauthorized", "服务凭据无效。");
        var roles = configuration.GetSection("CodeSystem:Roles").Get<CodeRoleSeed[]>() ?? [];
        var seed = roles.SingleOrDefault(r => r.Key == key) ?? throw new IdentityException(404, "role_not_registered", "未登记该预置角色。");
        if (seed.Id == Guid.Empty || string.IsNullOrWhiteSpace(seed.Name) || seed.Name.Length > 100 ||
            seed.Name.Trim().Equals("Administrator", StringComparison.OrdinalIgnoreCase) || seed.Permissions == null ||
            seed.Permissions.Any(p => !p.StartsWith("code.scope.", StringComparison.Ordinal) || !catalog.All.Contains(p)))
            throw new IdentityException(400, "invalid_role", "编码角色清单无效。");
        var normalized = seed.Name.Trim().ToUpperInvariant();
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            if (!await db.Roles.IgnoreQueryFilters().AnyAsync(r => r.Id == seed.Id || r.NormalizedName == normalized, ct))
            {
                var role = new Role { Id = seed.Id, Name = seed.Name.Trim(), NormalizedName = normalized };
                foreach (var grant in seed.Permissions.Distinct())
                    role.RoleClaims.Add(new RoleClaim { RoleId = role.Id, ClaimType = "permission", ClaimValue = grant });
                db.Roles.Add(role);
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
        });
    }
}
