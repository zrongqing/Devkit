using Devkit.Server.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Server.Infrastructure.Persistence;

public sealed class DevkitDbContext(DbContextOptions<DevkitDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DevkitDbContext).Assembly);
    }
}
