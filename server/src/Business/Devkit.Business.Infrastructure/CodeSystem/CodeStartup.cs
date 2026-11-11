using System.Net.Http.Json;
using Devkit.Business.Application.CodeSystem;
using Devkit.Business.Domain.CodeSystem;
using Devkit.DomainCommons.Initialization;
using Devkit.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Devkit.Business.Infrastructure.CodeSystem;

public static class CodeRegistration
{
    public static IServiceCollection AddCodeSystem(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CodeSettings>().Bind(configuration.GetSection("CodeSystem"))
            .Validate(s => s.Valid(), "编码场景或初始化配置无效。").ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<CodeSettings>>().Value);
        services.AddScoped<CodeLedger>();
        services.AddScoped<ICodeIssueStore>(sp => sp.GetRequiredService<CodeLedger>());
        services.AddScoped<ICodeGenerator, CodeGenerator>();
        services.AddScoped<CodeManagement>();
        services.AddScoped<CodeAccess>();
        return services.AddStartupDataInitializer<CodeStartup>();
    }
}

public sealed class CodeStartup(CodeSettings settings, IHttpClientFactory clients, CodeLedger ledger, ILogger<CodeStartup> log) : IStartupDataInitializer
{
    // Stable audit subject for configured seed drafts; it is never a login or a grant.
    public static readonly Guid SeedActor = new("d7cba2d2-9269-4c62-91e2-ab54e3d0622e");
    public async Task InitializeAsync(CancellationToken ct)
    {
        foreach (var key in settings.RoleKeys)
        {
            log.LogInformation("Checking configured code role {RoleKey}", key);
            using var request = new HttpRequestMessage(HttpMethod.Put, "api/v1/internal/code-system/bootstrap/roles/" + Uri.EscapeDataString(key));
            request.Headers.Add("X-Code-Bootstrap-Key", settings.BootstrapKey);
            using var client = clients.CreateClient("identity");
            using var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }
        foreach (var seed in settings.Drafts)
        {
            log.LogInformation("Checking configured code draft {RuleKey}", seed.RuleKey);
            var scenario = settings.Scenarios.Single(x => x.RuleKey == seed.RuleKey);
            await ledger.CreateAsync(scenario, seed.Name, seed.Definition, SeedActor, true, ct);
        }
    }
}
