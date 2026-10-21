using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Devkit.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddDevkitSqlServer<TContext>(this IServiceCollection services,
        IConfiguration configuration, string contextName, Action<DbContextOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contextName);
        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<AuditingSaveChangesInterceptor>();
        services.AddOptions<DatabaseConnectionOptions>(contextName)
            .Configure(options => options.ConnectionString = DatabaseConfiguration.GetConnectionString(configuration))
            .ValidateOnStart();
        services.AddDbContext<TContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IOptionsMonitor<DatabaseConnectionOptions>>()
                .Get(contextName).ConnectionString;
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
            configure?.Invoke(options);
            options.AddInterceptors(provider.GetRequiredService<AuditingSaveChangesInterceptor>());
        });
        services.AddHealthChecks().AddCheck<SqlServerHealthCheck<TContext>>("sql-server", tags: ["ready"]);
        return services;
    }

    private sealed class DatabaseConnectionOptions
    {
        public DatabaseConnectionOptions() { }
        public string ConnectionString { get; set; } = "";
    }
}
