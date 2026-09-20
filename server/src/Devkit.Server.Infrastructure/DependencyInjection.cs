using Devkit.Server.Application.Abstractions;
using Devkit.Server.Application.Contracts.Modules;
using Devkit.Server.Application.Modules;
using Devkit.Server.Domain.Abstractions;
using Devkit.Server.Domain.Identity;
using Devkit.Server.Infrastructure.Caching;
using Devkit.Server.Infrastructure.Configuration;
using Devkit.Server.Infrastructure.Health;
using Devkit.Server.Infrastructure.Identity;
using Devkit.Server.Infrastructure.Mapping;
using Devkit.Server.Infrastructure.Modules;
using Devkit.Server.Infrastructure.Navigation;
using Devkit.Server.Infrastructure.Persistence;
using Devkit.Server.Infrastructure.SystemInfo;
using Devkit.Server.Infrastructure.Workers;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Microsoft.Extensions.DependencyInjection;

namespace Devkit.Server.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDevkitInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        ServerRuntimeOptions runtimeOptions)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(runtimeOptions);
        services.AddSingleton<ISystemInfoService, SystemInfoService>();
        services.AddHttpContextAccessor();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => System.Text.Encoding.UTF8.GetByteCount(options.SigningKey) >= 32, "Jwt:SigningKey must contain at least 32 bytes")
            .Validate(options => options.AccessTokenMinutes is > 0 and <= 1440, "Jwt:AccessTokenMinutes must be between 1 and 1440")
            .Validate(options => options.RefreshTokenDays is > 0 and <= 365, "Jwt:RefreshTokenDays must be between 1 and 365")
            .ValidateOnStart();
        services.AddOptions<RegistrationOptions>()
            .Bind(configuration.GetSection(RegistrationOptions.SectionName));
        services.AddOptions<BootstrapAccountOptions>()
            .Bind(configuration.GetSection(BootstrapAccountOptions.SectionName))
            .Validate(options => !options.Enabled || (
                !string.IsNullOrWhiteSpace(options.UserName)
                && !string.IsNullOrWhiteSpace(options.Email)
                && PasswordPolicy.Validate(options.Password) is null),
                "Enabled bootstrap account requires user name, email, and a password matching the password policy")
            .ValidateOnStart();
        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Redis:ConnectionString is required")
            .Validate(options => options.ProfileTtlMinutes > 0, "Redis:ProfileTtlMinutes must be positive")
            .ValidateOnStart();
        services.AddOptions<RefreshTokenCleanupOptions>()
            .Bind(configuration.GetSection(RefreshTokenCleanupOptions.SectionName))
            .Validate(options => options.IntervalMinutes > 0, "BackgroundJobs:RefreshTokenCleanup:IntervalMinutes must be positive")
            .ValidateOnStart();
        services.AddOptions<ModuleControlOptions>()
            .Bind(configuration.GetSection(ModuleControlOptions.SectionName))
            .Validate(options => !options.Enabled || System.Text.Encoding.UTF8.GetByteCount(options.ApiKey) >= 32,
                "Enabled module control requires ModuleControl:ApiKey with at least 32 UTF-8 bytes")
            .Validate(options => options.OfflineAfterSeconds > 0, "ModuleControl:OfflineAfterSeconds must be positive")
            .Validate(options => options.DefaultLeaseSeconds > 0, "ModuleControl:DefaultLeaseSeconds must be positive")
            .Validate(options => options.MaximumLeaseSeconds >= options.DefaultLeaseSeconds,
                "ModuleControl:MaximumLeaseSeconds must be greater than or equal to DefaultLeaseSeconds")
            .Validate(options => options.MaximumPayloadBytes is >= 1024 and <= 1048576,
                "ModuleControl:MaximumPayloadBytes must be between 1024 and 1048576")
            .ValidateOnStart();
        services.AddOptions<NavigationOptions>()
            .Bind(configuration.GetSection(NavigationOptions.SectionName))
            .Validate(NavigationMenuComposer.TryValidateAll,
                "Navigation configuration must define valid Web and Client menu trees")
            .ValidateOnStart();
        services.AddSingleton<INavigationMenuService, NavigationMenuService>();
        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ModuleControlOptions>>().Value;
            return new ModuleRuntimePolicy(
                TimeSpan.FromSeconds(options.OfflineAfterSeconds),
                TimeSpan.FromSeconds(options.DefaultLeaseSeconds),
                TimeSpan.FromSeconds(options.MaximumLeaseSeconds),
                options.MaximumPayloadBytes);
        });

        services.AddScoped<AuditingSaveChangesInterceptor>();
        services.AddDbContext<DevkitDbContext>((serviceProvider, options) =>
        {
            var connectionString = configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default is required");
            options.UseSqlServer(connectionString, sqlServer => sqlServer.EnableRetryOnFailure());
            var bootstrapAccount = configuration
                .GetSection(BootstrapAccountOptions.SectionName)
                .Get<BootstrapAccountOptions>() ?? new BootstrapAccountOptions();
            options.UseIdentityDataSeeding(bootstrapAccount);
            options.AddInterceptors(serviceProvider.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        services.AddStackExchangeRedisCache(options =>
        {
            var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
            options.Configuration = redisOptions.ConnectionString;
            options.InstanceName = redisOptions.InstanceName;
        });
        services.TryAddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
            var parsed = ConfigurationOptions.Parse(redisOptions.ConnectionString);
            parsed.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(parsed);
        });

        TypeAdapterConfig mappingConfig = AuthMappingConfiguration.Create();
        services.AddSingleton(mappingConfig);
        services.AddScoped<IMapper, ServiceMapper>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<UserProfileCache>();
        services.AddScoped<IModuleRuntimeStore, SqlModuleRuntimeStore>();
        services.AddScoped<IModuleRuntimeService, ModuleRuntimeService>();
        services.AddScoped<AuthService>();
        services.AddScoped<IAuthService>(serviceProvider => serviceProvider.GetRequiredService<AuthService>());
        services.AddScoped<IAccessTokenValidator>(serviceProvider => serviceProvider.GetRequiredService<AuthService>());
        services.AddHostedService<IdentityBootstrapHostedService>();
        services.AddHostedService<RefreshTokenCleanupWorker>();

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sql-server", tags: ["ready"])
            .AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);
        return services;
    }
}
