using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Devkit.Server.Api.Endpoints;
using Devkit.Server.Application.Abstractions;
using Devkit.Server.Infrastructure;
using Devkit.Server.Infrastructure.Workspace;
using Devkit.Server.Infrastructure.Configuration;
using Devkit.Server.Infrastructure.SystemInfo;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
// Keep machine settings and model credentials outside the source tree.
var externalConfig = Environment.GetEnvironmentVariable("DEVKIT_CONFIG_FILE");
if (!string.IsNullOrWhiteSpace(externalConfig))
{
    builder.Configuration.AddJsonFile(externalConfig, optional: false, reloadOnChange: false)
        .AddEnvironmentVariables().AddCommandLine(args);
}
var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
builder.Services.AddDevkitInfrastructure(
    builder.Configuration,
    new ServerRuntimeOptions("Devkit Server", version, builder.Environment.EnvironmentName));
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
    .Configure<IOptions<JwtOptions>>((options, configuredJwtOptions) =>
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
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
                var versionClaim = context.Principal?.FindFirstValue("auth_version");
                if (!Guid.TryParse(subject, out var userId) || !int.TryParse(versionClaim, out var authenticationVersion))
                {
                    context.Fail("Token claims are invalid.");
                    return;
                }

                var validator = context.HttpContext.RequestServices.GetRequiredService<IAccessTokenValidator>();
                if (!await validator.IsCurrentAsync(userId, authenticationVersion, context.HttpContext.RequestAborted))
                {
                    context.Fail("Token has been invalidated.");
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
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many authentication requests.",
            Extensions =
            {
                ["code"] = "rate_limited",
                ["traceId"] = context.HttpContext.TraceIdentifier
            }
        }, cancellationToken);
    };
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseRateLimiter();
app.UseCors("Web");
app.UseAuthentication();
app.UseAuthorization();
app.MapDevkitOpenApi();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.SwaggerEndpoint("/openapi/v1.json", "Devkit Server API v1");
    options.DocumentTitle = "Devkit Server API";
});
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapEndpointModules();
app.MapControllers();
app.Run();

public partial class Program;
