using Devkit.Server.Application.Workspace;
using Devkit.Server.Application.Navigation;
using Devkit.Server.Domain.Navigation;
using Devkit.Server.Infrastructure.Navigation;
using Devkit.Server.Domain.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Devkit.Server.Infrastructure.Workspace;

public static class WorkspaceRegistration
{
    public static IServiceCollection AddWorkspace(this IServiceCollection services,IConfiguration config)
    {
        services.AddScoped<IWebMenuStore, WebMenuStore>();
        services.AddScoped<IWebMenuDefaults, WebMenuDefaults>();
        services.AddScoped<WebMenuService>();
        services.AddHostedService<WebMenuBootstrapHostedService>();
        services.AddOptions<WorkspaceOptions>().Bind(config.GetSection("Workspace"));
        services.AddHttpClient();
        services.AddScoped<IWorkspaceStore,WorkspaceStore>();services.AddScoped<IWorkspaceAccess,WorkspaceAccess>();
        services.AddScoped<IAccountAdministration,AccountAdministration>();services.AddScoped<StudyService>();
        services.AddScoped<LocalFileService>();services.AddScoped<IFileService>(x=>x.GetRequiredService<LocalFileService>());
        services.AddScoped<IDocumentProcessor,DocumentProcessor>();services.AddScoped<IModelGateway,ModelGateway>();
        services.AddScoped<QdrantKnowledgeIndex>();services.AddScoped<IKnowledgeIndex>(x=>x.GetRequiredService<QdrantKnowledgeIndex>());
        services.AddScoped<WorkspaceJobHandler>();services.AddHostedService<WorkspaceWorker>();
        return services;
    }
}
