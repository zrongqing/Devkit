using Devkit.Server.Api.Endpoints;
using Devkit.Server.Infrastructure;
using Devkit.Server.Infrastructure.SystemInfo;

var builder = WebApplication.CreateBuilder(args);
var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
builder.Services.AddDevkitInfrastructure(new ServerRuntimeOptions("Devkit Server", version, builder.Environment.EnvironmentName));
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseExceptionHandler();
app.MapDevkitOpenApi();
app.MapHealthChecks("/health");
app.MapSystemEndpoints();
app.Run();

public partial class Program;
