using Devkit.Infrastructure.Persistence;
using Devkit.Application.Domain.Organization;
using Microsoft.AspNetCore.Http;
using Devkit.Application.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Application.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor? accessor = null) : PagedDbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserClaim> UserClaims => Set<UserClaim>();
    public DbSet<RoleClaim> RoleClaims => Set<RoleClaim>();
    public DbSet<UserLogin> UserLogins => Set<UserLogin>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var context = accessor?.HttpContext;
        if (context is not null && Guid.TryParse(context.User.FindFirst("sub")?.Value, out var actor)
            && !HttpMethods.IsGet(context.Request.Method) && ChangeTracker.HasChanges()
            && (context.Request.Path.StartsWithSegments("/api/v1/identity") ||
                context.Request.Path.StartsWithSegments("/api/v1/web/menu-management") ||
                context.Request.Path.Equals(new PathString("/api/v1/auth/password"))))
        {
            // Persist the activity in the same transaction. Never capture request bodies or credentials.
            var operation = $"{context.Request.Method} {context.Request.Path.Value}";
            Set<AdministrationEvent>().Add(new AdministrationEvent
            {
                ActorId = actor, OccurredUtc = DateTime.UtcNow, Operation = operation[..Math.Min(operation.Length, 512)],
                TraceId = context.TraceIdentifier
            });
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        modelBuilder.UseSnakeCaseDatabaseNames();
    }
}
