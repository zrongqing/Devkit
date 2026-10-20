using Devkit.Infrastructure.Persistence;
using Devkit.Business.Application.Workspace;
using Devkit.Business.Infrastructure.Identity;
using Devkit.Business.Application.Abstractions;
using Devkit.Business.Application.Contracts.Modules;
using Devkit.Business.Application.Modules;
using Devkit.Business.Domain.Abstractions;
using Devkit.Business.Infrastructure.Configuration;
using Devkit.Business.Infrastructure.Health;
using Devkit.Business.Infrastructure.Modules;
using Devkit.Business.Infrastructure.Persistence;
using Devkit.Business.Infrastructure.SystemInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Devkit.Business.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        ServerRuntimeOptions runtimeOptions)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(runtimeOptions);
        services.AddSingleton<JwtValidationKey>();
        services.AddSingleton<ISystemInfoService, SystemInfoService>();
        services.AddHttpContextAccessor();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required")
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
        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ModuleControlOptions>>().Value;
            return new ModuleRuntimePolicy(
                TimeSpan.FromSeconds(options.OfflineAfterSeconds),
                TimeSpan.FromSeconds(options.DefaultLeaseSeconds),
                TimeSpan.FromSeconds(options.MaximumLeaseSeconds),
                options.MaximumPayloadBytes);
        });

        services.AddDevkitSqlServer<BusinessDbContext>(configuration, "Business");

        services.AddScoped<IModuleRuntimeStore, SqlModuleRuntimeStore>();
        services.AddScoped<IModuleRuntimeService, ModuleRuntimeService>();
        services.AddOptions<IdentityServiceOptions>().Bind(configuration.GetSection("Identity"))
            .Validate(IdentityServiceOptions.IsValid, "Identity:BaseUrl must be a root HTTP(S) URL; HTTPS is required unless Identity:RequireHttps is false")
            .Validate(options => options.TimeoutSeconds is > 0 and <= 30, "Identity:TimeoutSeconds must be between 1 and 30")
            .ValidateOnStart();
        services.AddHttpClient("identity", (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<IdentityServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IIdentitySession, RemoteIdentitySession>();
        services.AddScoped<IWorkspaceAccess, RemoteWorkspaceAccess>();

        services.AddHealthChecks()
            .AddCheck<IdentityServiceHealthCheck>("identity", tags: ["ready"]);
        return services;
    }
}
