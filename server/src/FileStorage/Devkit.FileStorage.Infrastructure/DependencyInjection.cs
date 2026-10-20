using Devkit.FileStorage.Application.Abstractions;
using Devkit.FileStorage.Application.Files;
using Devkit.FileStorage.Domain.Abstractions;
using Devkit.FileStorage.Infrastructure.Configuration;
using Devkit.FileStorage.Infrastructure.Files;
using Devkit.FileStorage.Infrastructure.Health;
using Devkit.FileStorage.Infrastructure.Identity;
using Devkit.FileStorage.Infrastructure.Persistence;
using Devkit.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Devkit.FileStorage.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddFileStorageInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDevkitSqlServer<FileStorageDbContext>(configuration, "FileStorage");
        services.AddScoped<IFileStorageStore, FileStorageStore>();
        services.AddScoped<LocalFileService>();
        services.AddScoped<IFileService>(provider => provider.GetRequiredService<LocalFileService>());
        services.AddScoped<IFileReferenceService>(provider => provider.GetRequiredService<LocalFileService>());
        services.AddScoped<StorageJobHandler>();
        services.AddHostedService<FileStorageWorker>();
        services.AddSingleton<JwtValidationKey>();
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection("Jwt"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience), "Jwt issuer and audience are required")
            .ValidateOnStart();
        services.AddOptions<FileStorageOptions>().Bind(configuration.GetSection("FileStorage"))
            .Validate(o => Path.IsPathFullyQualified(o.DataRoot), "FileStorage:DataRoot must be absolute")
            .Validate(o => o.MaximumUploadBytes is > 0 and <= 1024L * 1024 * 1024 && o.MinimumFreeBytes >= 0, "Invalid file size or free space limits")
            .Validate(o => o.Clients.All(c => System.Text.Encoding.UTF8.GetByteCount(c.Value.ApiKey) >= 32 && o.Purposes.ContainsKey(c.Value.Module)), "File clients require a key of at least 32 UTF-8 bytes and a registered module")
            .Validate(o => o.Purposes.All(p => p.Value.UploadPermissions.Length > 0 && p.Value.DownloadPermissions.Length > 0), "Purpose permissions are required")
            .ValidateOnStart();
        services.AddOptions<IdentityServiceOptions>().Bind(configuration.GetSection("Identity"))
            .Validate(IdentityServiceOptions.IsValid, "Identity requires a root HTTPS URL unless RequireHttps is false")
            .Validate(o => o.TimeoutSeconds is > 0 and <= 30, "Invalid identity timeout").ValidateOnStart();
        services.AddHttpClient("identity", (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<IdentityServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl); client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IIdentitySession, RemoteIdentitySession>();
        services.AddScoped<IFileAccess, RemoteFileAccess>();
        services.AddHealthChecks().AddCheck<IdentityServiceHealthCheck>("identity", tags: ["ready"]);
        return services;
    }
}
