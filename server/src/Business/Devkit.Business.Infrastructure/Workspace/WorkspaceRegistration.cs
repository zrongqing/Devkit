using Devkit.Business.Application.Workspace;
using Devkit.Business.Domain.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Devkit.Business.Infrastructure.Workspace;

public static class WorkspaceRegistration
{
    public static IServiceCollection AddWorkspace(this IServiceCollection services,IConfiguration config)
    {
        services.AddOptions<WorkspaceOptions>().Bind(config.GetSection("Workspace"));
        services.AddHttpClient();
        services.AddScoped<IWorkspaceStore,WorkspaceStore>();
        services.AddScoped<StudyService>();
        services.AddScoped<LocalFileService>();services.AddScoped<IFileService>(x=>x.GetRequiredService<LocalFileService>());
        services.AddScoped<IDocumentProcessor,DocumentProcessor>();services.AddScoped<IModelGateway,ModelGateway>();
        services.AddScoped<QdrantKnowledgeIndex>();services.AddScoped<IKnowledgeIndex>(x=>x.GetRequiredService<QdrantKnowledgeIndex>());
        services.AddScoped<WorkspaceJobHandler>();services.AddHostedService<WorkspaceWorker>();
        return services;
    }
}
