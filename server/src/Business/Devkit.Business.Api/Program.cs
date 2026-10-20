using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Devkit.Business.Api.Endpoints;
using Devkit.Business.Application.Abstractions;
using Devkit.Business.Infrastructure;
using Devkit.Business.Infrastructure.Workspace;
using Devkit.Business.Infrastructure.Configuration;
using Devkit.Business.Infrastructure.Identity;
using Devkit.Business.Infrastructure.SystemInfo;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Devkit.Business.Api;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        // Keep machine settings and model credentials outside the source tree.
        var externalConfig = Environment.GetEnvironmentVariable("DEVKIT_BUSINESS_CONFIG_FILE");
        if (!string.IsNullOrWhiteSpace(externalConfig))
        {
            builder.Configuration.AddJsonFile(externalConfig, optional: false, reloadOnChange: false)
                .AddEnvironmentVariables().AddCommandLine(args);
        }
        var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        builder.Services.AddBusinessInfrastructure(builder.Configuration,
            new ServerRuntimeOptions("Devkit Business", version, builder.Environment.EnvironmentName));
        builder.Services.AddEndpointModules(typeof(Program).Assembly);
        builder.Services.AddWorkspace(builder.Configuration);
        builder.Services.AddExceptionHandler<WorkspaceExceptionHandler>();
        builder.Services.AddControllers();
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
            context.ProblemDetails.Extensions.TryAdd("code", "server_error");
        });
        builder.Services.AddOpenApi("v1", options =>
        {
            options.ShouldInclude = description => description.RelativePath?.StartsWith("api/v1", StringComparison.OrdinalIgnoreCase) == true
                || description.RelativePath?.StartsWith("health", StringComparison.OrdinalIgnoreCase) == true;
        });
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        builder.Services.AddCors(options => options.AddPolicy("Web", policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition");
            }
        }));
        
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, JwtValidationKey>((options, configuredJwtOptions, keys) =>
            {
                var jwtOptions = configuredJwtOptions.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = keys.ValidationKey,
                    ValidAlgorithms = [keys.Algorithm],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var subject = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
                        if (!Guid.TryParse(subject, out var userId))
                        {
                            context.Fail("Token subject is invalid.");
                            return;
                        }
                        var bearer = context.Request.Headers.Authorization.ToString();
                        try
                        {
                            await context.HttpContext.RequestServices.GetRequiredService<IIdentitySession>()
                                .AuthenticateAsync(bearer["Bearer ".Length..], userId, context.HttpContext.RequestAborted);
                        }
                        catch (Devkit.Business.Application.Workspace.BusinessException exception) when (exception.Status == 401)
                        {
                            context.Fail("Identity session is no longer valid.");
                        }
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
                        {
                            Status = StatusCodes.Status401Unauthorized,
                            Title = "Authentication is required.",
                            Extensions =
                            {
                                ["code"] = "unauthorized",
                                ["traceId"] = context.HttpContext.TraceIdentifier
                            }
                        });
                    }
                };
            });
        builder.Services.AddAuthorization();
        var app = builder.Build();
        // Fail at startup for missing or unsuitable key files, before accepting any traffic.
        _ = app.Services.GetRequiredService<JwtValidationKey>();
        app.UseExceptionHandler();
        app.UseCors("Web");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapDevkitOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.SwaggerEndpoint("/openapi/v1.json", "Devkit Business API v1");
            options.DocumentTitle = "Devkit Business API";
        });
        app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
        app.MapEndpointModules();
        app.MapControllers();
        app.Run();
        
        
    }
}
