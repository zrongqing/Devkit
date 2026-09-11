using Devkit.Server.Domain.Identity;
using Devkit.Server.Infrastructure.Configuration;
using Devkit.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Devkit.Server.Infrastructure.Identity;

internal sealed class IdentityBootstrapHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<BootstrapAccountOptions> bootstrapOptions,
    ILogger<IdentityBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevkitDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var administratorRole = await EnsureRoleAsync(dbContext, "Administrator", cancellationToken);
        await EnsureRoleAsync(dbContext, "User", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var options = bootstrapOptions.Value;
        if (!options.Enabled)
        {
            return;
        }

        var normalizedUserName = options.UserName.Trim().ToUpperInvariant();
        var normalizedEmail = options.Email.Trim().ToUpperInvariant();
        var existing = await dbContext.Users
            .Include(user => user.UserRoles)
            .SingleOrDefaultAsync(
                user => user.NormalizedUserName == normalizedUserName || user.NormalizedEmail == normalizedEmail,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.UserRoles.All(userRole => userRole.RoleId != administratorRole.Id))
            {
                existing.UserRoles.Add(new UserRole
                {
                    UserId = existing.Id,
                    RoleId = administratorRole.Id,
                    CreatedBy = existing.Id,
                    UpdatedBy = existing.Id
                });
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var user = new User
        {
            UserName = options.UserName.Trim(),
            NormalizedUserName = normalizedUserName,
            Email = options.Email.Trim(),
            NormalizedEmail = normalizedEmail
        };
        user.CreatedBy = user.Id;
        user.UpdatedBy = user.Id;
        user.PasswordHash = passwordHasher.HashPassword(user, options.Password);
        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = administratorRole.Id,
            CreatedBy = user.Id,
            UpdatedBy = user.Id
        });
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Bootstrap administrator {UserName} was created", user.UserName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<Role> EnsureRoleAsync(DevkitDbContext dbContext, string name, CancellationToken cancellationToken)
    {
        var normalizedName = name.ToUpperInvariant();
        var existing = await dbContext.Roles.SingleOrDefaultAsync(role => role.NormalizedName == normalizedName, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var role = new Role { Name = name, NormalizedName = normalizedName };
        dbContext.Roles.Add(role);
        return role;
    }
}
