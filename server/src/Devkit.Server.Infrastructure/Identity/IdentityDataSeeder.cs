using Devkit.Server.Domain.Identity;
using Devkit.Server.Infrastructure.Configuration;
using Devkit.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Server.Infrastructure.Identity;

internal static class IdentityDataSeeder
{
    public static bool Seed(DevkitDbContext dbContext, BootstrapAccountOptions options)
    {
        var now = DateTime.UtcNow;
        var administratorRole = EnsureRole(dbContext, "Administrator", now);
        EnsureRole(dbContext, "User", now);

        if (!options.Enabled)
        {
            dbContext.SaveChanges();
            return false;
        }

        Validate(options);
        var normalizedUserName = options.UserName.Trim().ToUpperInvariant();
        var normalizedEmail = options.Email.Trim().ToUpperInvariant();
        var user = dbContext.Users
            .IgnoreQueryFilters()
            .SingleOrDefault(candidate =>
                candidate.NormalizedUserName == normalizedUserName
                || candidate.NormalizedEmail == normalizedEmail);
        var administratorCreated = user is null;

        if (user is null)
        {
            user = CreateAdministrator(options, normalizedUserName, normalizedEmail, now);
            dbContext.Users.Add(user);
        }
        else if (user.IsDeleted)
        {
            user.IsDeleted = false;
            user.UpdatedAtUtc = now;
        }

        EnsureUserRole(dbContext, user, administratorRole, now);
        dbContext.SaveChanges();
        return administratorCreated;
    }

    public static async Task<bool> SeedAsync(
        DevkitDbContext dbContext,
        BootstrapAccountOptions options,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var administratorRole = await EnsureRoleAsync(dbContext, "Administrator", now, cancellationToken);
        await EnsureRoleAsync(dbContext, "User", now, cancellationToken);

        if (!options.Enabled)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        Validate(options);
        var normalizedUserName = options.UserName.Trim().ToUpperInvariant();
        var normalizedEmail = options.Email.Trim().ToUpperInvariant();
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                candidate => candidate.NormalizedUserName == normalizedUserName
                    || candidate.NormalizedEmail == normalizedEmail,
                cancellationToken);
        var administratorCreated = user is null;

        if (user is null)
        {
            user = CreateAdministrator(options, normalizedUserName, normalizedEmail, now);
            dbContext.Users.Add(user);
        }
        else if (user.IsDeleted)
        {
            user.IsDeleted = false;
            user.UpdatedAtUtc = now;
        }

        await EnsureUserRoleAsync(dbContext, user, administratorRole, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return administratorCreated;
    }

    private static Role EnsureRole(DevkitDbContext dbContext, string name, DateTime now)
    {
        var normalizedName = name.ToUpperInvariant();
        var role = dbContext.Roles
            .IgnoreQueryFilters()
            .SingleOrDefault(candidate => candidate.NormalizedName == normalizedName);
        if (role is null)
        {
            role = CreateRole(name, normalizedName, now);
            dbContext.Roles.Add(role);
        }
        else if (role.IsDeleted)
        {
            role.IsDeleted = false;
            role.UpdatedAtUtc = now;
        }

        return role;
    }

    private static async Task<Role> EnsureRoleAsync(
        DevkitDbContext dbContext,
        string name,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var normalizedName = name.ToUpperInvariant();
        var role = await dbContext.Roles
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(candidate => candidate.NormalizedName == normalizedName, cancellationToken);
        if (role is null)
        {
            role = CreateRole(name, normalizedName, now);
            dbContext.Roles.Add(role);
        }
        else if (role.IsDeleted)
        {
            role.IsDeleted = false;
            role.UpdatedAtUtc = now;
        }

        return role;
    }

    private static void EnsureUserRole(
        DevkitDbContext dbContext,
        User user,
        Role role,
        DateTime now)
    {
        var userRole = dbContext.UserRoles
            .IgnoreQueryFilters()
            .SingleOrDefault(candidate => candidate.UserId == user.Id && candidate.RoleId == role.Id);
        if (userRole is null)
        {
            dbContext.UserRoles.Add(CreateUserRole(user.Id, role.Id, now));
        }
        else if (userRole.IsDeleted)
        {
            userRole.IsDeleted = false;
            userRole.UpdatedAtUtc = now;
        }
    }

    private static async Task EnsureUserRoleAsync(
        DevkitDbContext dbContext,
        User user,
        Role role,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var userRole = await dbContext.UserRoles
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                candidate => candidate.UserId == user.Id && candidate.RoleId == role.Id,
                cancellationToken);
        if (userRole is null)
        {
            dbContext.UserRoles.Add(CreateUserRole(user.Id, role.Id, now));
        }
        else if (userRole.IsDeleted)
        {
            userRole.IsDeleted = false;
            userRole.UpdatedAtUtc = now;
        }
    }

    private static Role CreateRole(string name, string normalizedName, DateTime now) => new()
    {
        Name = name,
        NormalizedName = normalizedName,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };

    private static User CreateAdministrator(
        BootstrapAccountOptions options,
        string normalizedUserName,
        string normalizedEmail,
        DateTime now)
    {
        var user = new User
        {
            UserName = options.UserName.Trim(),
            NormalizedUserName = normalizedUserName,
            Email = options.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        user.CreatedBy = user.Id;
        user.UpdatedBy = user.Id;
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, options.Password);
        return user;
    }

    private static UserRole CreateUserRole(Guid userId, Guid roleId, DateTime now) => new()
    {
        UserId = userId,
        RoleId = roleId,
        CreatedBy = userId,
        UpdatedBy = userId,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };

    private static void Validate(BootstrapAccountOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.UserName)
            || string.IsNullOrWhiteSpace(options.Email)
            || PasswordPolicy.Validate(options.Password) is not null)
        {
            throw new InvalidOperationException(
                "Enabled bootstrap account requires user name, email, and a password matching the password policy.");
        }
    }
}
