using Devkit.Infrastructure.Persistence;
using Devkit.Application.Application.Authorization;
using Devkit.Application.Application.Administration;
using Devkit.Application.Application.Navigation;
using Devkit.Application.Domain.Navigation;
using Devkit.Application.Infrastructure.Authorization;
using Devkit.Application.Application.Abstractions;
using Devkit.Application.Domain.Identity;
using Devkit.Application.Infrastructure.Caching;
using Devkit.Application.Infrastructure.Configuration;
using Devkit.Application.Infrastructure.Health;
using Devkit.Application.Infrastructure.Identity;
using Devkit.Application.Infrastructure.Mapping;
using Devkit.Application.Infrastructure.Navigation;
using Devkit.Application.Infrastructure.Persistence;
using Devkit.Application.Infrastructure.Workers;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Microsoft.Extensions.DependencyInjection;

namespace Devkit.Application.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<JwtKeyMaterial>();
        services.AddHttpContextAccessor();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required")
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
                && (options.IsBuiltInDefault || PasswordPolicy.Validate(options.Password) is null)),
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
        services.AddOptions<NavigationOptions>()
            .Bind(configuration.GetSection(NavigationOptions.SectionName))
            .Validate(NavigationMenuComposer.TryValidateAll,
                "Navigation configuration must define valid Web and Client menu trees")
            .ValidateOnStart();
        services.AddSingleton<INavigationMenuService, NavigationMenuService>();
        services.AddDevkitSqlServer<ApplicationDbContext>(configuration, "Application", options =>
        {
            var bootstrap = configuration.GetSection(BootstrapAccountOptions.SectionName).Get<BootstrapAccountOptions>()
                ?? new BootstrapAccountOptions();
            options.UseIdentityDataSeeding(bootstrap);
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
        services.AddScoped<IAccessTokenValidator, AccessTokenValidator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddHostedService<IdentityBootstrapHostedService>();
        services.AddHostedService<RefreshTokenCleanupWorker>();
        services.AddScoped<Devkit.Application.Domain.Organization.IOrganizationStore, OrganizationStore>();
        services.AddScoped<Devkit.Application.Domain.Organization.IAdministrationLog, OrganizationStore>();
        services.AddScoped<OrganizationAdministration>();
        services.AddScoped<AdministrationPageService>();
        services.AddScoped<MenuPageService>();
        services.AddScoped<ICurrentIdentity, CurrentIdentity>();
        services.AddScoped<IAccountAdministration, AccountAdministration>();
        services.AddScoped<IWebMenuStore, WebMenuStore>();
        services.AddScoped<IWebMenuDefaults, WebMenuDefaults>();
        services.AddScoped<WebMenuService>();
        services.AddHostedService<WebMenuBootstrapHostedService>();

        services.AddHealthChecks()
            .AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);
        return services;
    }
}
