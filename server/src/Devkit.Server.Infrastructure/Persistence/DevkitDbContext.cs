using Devkit.Server.Domain.Identity;
using Devkit.Server.Domain.Modules;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Server.Infrastructure.Persistence;

public sealed class DevkitDbContext(DbContextOptions<DevkitDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserClaim> UserClaims => Set<UserClaim>();
    public DbSet<RoleClaim> RoleClaims => Set<RoleClaim>();
    public DbSet<UserLogin> UserLogins => Set<UserLogin>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ModuleInstance> ModuleInstances => Set<ModuleInstance>();
    public DbSet<BufferedModuleCommand> BufferedModuleCommands => Set<BufferedModuleCommand>();
    public DbSet<ModuleCommandSequence> ModuleCommandSequences => Set<ModuleCommandSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DevkitDbContext).Assembly);
    }
}
