using System.Reflection;
using Devkit.AspNetCore.Endpoints;
using Devkit.AspNetCore.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Devkit.AspNetCore.Hosting;

public static class ApiHostingExtensions
{
    public static WebApplicationBuilder AddDevkitExternalConfiguration(this WebApplicationBuilder builder,
        string environmentVariable, string[] args)
    {
        var path = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(path))
            builder.Configuration.AddJsonFile(path, optional: false, reloadOnChange: false)
                .AddEnvironmentVariables().AddCommandLine(args);
        return builder;
    }

    public static IServiceCollection AddDevkitApi(this IServiceCollection services, IConfiguration configuration,
        params Assembly[] endpointAssemblies)
    {
        services.AddEndpointModules(endpointAssemblies);
        services.AddExceptionHandler<ServiceExceptionHandler>();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
            context.ProblemDetails.Extensions.TryAdd("code", "server_error");
        });
        services.AddOpenApi("v1", options => options.ShouldInclude = description =>
            description.RelativePath?.StartsWith("api/v1", StringComparison.OrdinalIgnoreCase) == true
            || description.RelativePath?.StartsWith("health", StringComparison.OrdinalIgnoreCase) == true);
        services.AddCors(options => options.AddPolicy("Web", policy =>
        {
            var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            if (allowedOrigins.Length > 0)
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition");
        }));
        return services;
    }

    public static WebApplication UseDevkitApi(this WebApplication app, Action<WebApplication>? beforeAuthentication = null)
    {
        app.UseExceptionHandler();
        beforeAuthentication?.Invoke(app);
        app.UseCors("Web");
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    public static WebApplication MapDevkitApi(this WebApplication app, string title)
    {
        app.MapOpenApi("/openapi/{documentName}.json");
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.SwaggerEndpoint("/openapi/v1.json", $"{title} v1");
            options.DocumentTitle = title;
        });
        app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
        app.MapEndpointModules();
        return app;
    }
}
