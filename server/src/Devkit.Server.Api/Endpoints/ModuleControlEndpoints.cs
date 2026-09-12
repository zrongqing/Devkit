using System.Text.Json;
using Devkit.Server.Api.Contracts;
using Devkit.Server.Application.Abstractions;
using Devkit.Server.Application.Contracts.Modules;

namespace Devkit.Server.Api.Endpoints;

public static class ModuleControlEndpoints
{
    public static RouteGroupBuilder MapModuleControlEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/internal/modules")
            .WithTags("Module Control (Internal)")
            .AddEndpointFilter<ModuleApiKeyEndpointFilter>();

        group.MapGet("/instances", ListInstancesAsync)
            .WithName("ListModuleInstances")
            .WithSummary("Lists registered module instances and their effective runtime status")
            .Produces<ApiResponse<IReadOnlyList<ModuleInstanceDto>>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group.MapPut("/{moduleKey}/instances/{instanceId}", RegisterAsync)
            .WithName("RegisterModuleInstance")
            .WithSummary("Registers a newly started business-module instance")
            .Produces<ApiResponse<ModuleInstanceDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/{moduleKey}/instances/{instanceId}/heartbeat", HeartbeatAsync)
            .WithName("HeartbeatModuleInstance")
            .WithSummary("Renews the liveness timestamp of a registered module instance")
            .Produces<ApiResponse<ModuleInstanceDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPut("/{moduleKey}/instances/{instanceId}/state", SetStateAsync)
            .WithName("SetModuleInstanceState")
            .WithSummary("Moves a module instance between running, draining, and stopped states")
            .Produces<ApiResponse<ModuleInstanceDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/commands", EnqueueAsync)
            .WithName("EnqueueModuleCommand")
            .WithSummary("Durably buffers an ordered command for a target module")
            .Produces<ApiResponse<BufferedModuleCommandResponse>>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapGet("/commands/{commandId:guid}", FindCommandAsync)
            .WithName("GetModuleCommand")
            .WithSummary("Returns the current delivery state of a buffered module command")
            .Produces<ApiResponse<BufferedModuleCommandResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/{moduleKey}/instances/{instanceId}/commands/lease", LeaseNextAsync)
            .WithName("LeaseNextModuleCommand")
            .WithSummary("Leases the next ordered command to an online module instance")
            .Produces<ApiResponse<LeasedModuleCommandResponse>>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        group.MapPost("/{moduleKey}/instances/{instanceId}/commands/{commandId:guid}/complete", CompleteAsync)
            .WithName("CompleteModuleCommand")
            .WithSummary("Acknowledges successful processing of a leased module command")
            .Produces<ApiResponse<BufferedModuleCommandResponse>>()
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPost("/{moduleKey}/instances/{instanceId}/commands/{commandId:guid}/fail", FailAsync)
            .WithName("FailModuleCommand")
            .WithSummary("Retries or dead-letters a failed leased module command")
            .Produces<ApiResponse<BufferedModuleCommandResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
        return group;
    }

    private static async Task<IResult> ListInstancesAsync(
        HttpContext context,
        IModuleRuntimeService service,
        CancellationToken cancellationToken)
    {
        var instances = await service.ListInstancesAsync(cancellationToken);
        return Results.Ok(new ApiResponse<IReadOnlyList<ModuleInstanceDto>>(instances, context.TraceIdentifier));
    }

    private static async Task<IResult> RegisterAsync(
        string moduleKey,
        string instanceId,
        RegisterModuleInstanceRequest request,
        HttpContext context,
        IModuleRuntimeService service,
        CancellationToken cancellationToken)
    {
        var result = await service.RegisterAsync(
            new RegisterModuleInstance(moduleKey, instanceId, request.Version, request.BaseAddress),
            cancellationToken);
        return ToResult(result, context);
    }

    private static async Task<IResult> HeartbeatAsync(
        string moduleKey,
        string instanceId,
        HttpContext context,
        IModuleRuntimeService service,
        CancellationToken cancellationToken)
    {
        var result = await service.HeartbeatAsync(moduleKey, instanceId, cancellationToken);
        return ToResult(result, context);
    }

    private static async Task<IResult> SetStateAsync(
        string moduleKey,
        string instanceId,
        SetModuleInstanceStateRequest request,
        HttpContext context,
        IModuleRuntimeService service,
        CancellationToken cancellationToken)
    {
        var result = await service.SetStateAsync(moduleKey, instanceId, request.State, cancellationToken);
        return ToResult(result, context);
    }

    private static async Task<IResult> EnqueueAsync(
        EnqueueModuleCommandRequest request,
        HttpContext context,
        IModuleRuntimeService service,
        CancellationToken cancellationToken)
    {
        var result = await service.EnqueueAsync(
            new EnqueueModuleCommand(
                request.SourceModule,
                request.TargetModule,
                request.CommandType,
                request.SchemaVersion,
                request.Payload?.GetRawText() ?? string.Empty,
                request.IdempotencyKey,
                request.CorrelationId,
                request.AvailableAtUtc),
            cancellationToken);
        if (!result.Succeeded)
        {
            return ToProblem(result.ErrorCode!, result.ErrorMessage!, context);
        }

        return Results.Accepted(
            $"/api/v1/internal/modules/commands/{result.Value!.Id}",
            new ApiResponse<BufferedModuleCommandResponse>(ToResponse(result.Value), context.TraceIdentifier));
    }

    private static async Task<IResult> FindCommandAsync(
        Guid commandId,
        HttpContext context,
        IModuleRuntimeService service,
        CancellationToken cancellationToken)
    {
        var result = await service.FindCommandAsync(commandId, cancellationToken);
        return result.Succeeded
            ? Results.Ok(new ApiResponse<BufferedModuleCommandResponse>(ToResponse(result.Value!), context.TraceIdentifier))
            : ToProblem(result.ErrorCode!, result.ErrorMessage!, context);
    }

    private static async Task<IResult> LeaseNextAsync(
        string moduleKey,
        string instanceId,
        LeaseModuleCommandRequest? request,
        HttpContext context,
        IModuleRuntimeService service,
        CancellationToken cancellationToken)
    {
        var result = await service.LeaseNextAsync(moduleKey, instanceId, request?.LeaseSeconds, cancellationToken);
        if (!result.Succeeded)
        {
            return ToProblem(result.ErrorCode!, result.ErrorMessage!, context);
        }

        return result.Value is null
            ? Results.NoContent()
            : Results.Ok(new ApiResponse<LeasedModuleCommandResponse>(ToResponse(result.Value), context.TraceIdentifier));
    }

    private static async Task<IResult> CompleteAsync(
        string moduleKey,
        string instanceId,
        Guid commandId,
        CompleteModuleCommandRequest request,
        HttpContext context,
        IModuleRuntimeService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CompleteAsync(moduleKey, instanceId, commandId, request.LeaseId, cancellationToken);
        return result.Succeeded
            ? Results.Ok(new ApiResponse<BufferedModuleCommandResponse>(ToResponse(result.Value!), context.TraceIdentifier))
            : ToProblem(result.ErrorCode!, result.ErrorMessage!, context);
    }

    private static async Task<IResult> FailAsync(
        string moduleKey,
        string instanceId,
        Guid commandId,
        FailModuleCommandRequest request,
        HttpContext context,
        IModuleRuntimeService service,
        CancellationToken cancellationToken)
    {
        var result = await service.FailAsync(
            moduleKey,
            instanceId,
            commandId,
            request.LeaseId,
            request.Retry,
            request.RetryDelaySeconds,
            request.Error,
            cancellationToken);
        return result.Succeeded
            ? Results.Ok(new ApiResponse<BufferedModuleCommandResponse>(ToResponse(result.Value!), context.TraceIdentifier))
            : ToProblem(result.ErrorCode!, result.ErrorMessage!, context);
    }

    private static BufferedModuleCommandResponse ToResponse(BufferedModuleCommandDto command) => new(
        command.Id,
        command.SourceModule,
        command.TargetModule,
        command.Sequence,
        command.CommandType,
        command.SchemaVersion,
        ParsePayload(command.Payload),
        command.IdempotencyKey,
        command.CorrelationId,
        command.Status,
        command.DeliveryAttempts,
        command.AvailableAtUtc,
        command.CompletedAtUtc,
        command.LastError,
        command.IsDuplicate);

    private static LeasedModuleCommandResponse ToResponse(LeasedModuleCommandDto command) => new(
        command.Id,
        command.LeaseId,
        command.SourceModule,
        command.TargetModule,
        command.Sequence,
        command.CommandType,
        command.SchemaVersion,
        ParsePayload(command.Payload),
        command.IdempotencyKey,
        command.CorrelationId,
        command.DeliveryAttempt,
        command.LeaseExpiresAtUtc);

    private static JsonElement ParsePayload(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.Clone();
    }

    private static IResult ToResult<T>(ModuleOperationResult<T> result, HttpContext context)
    {
        return result.Succeeded
            ? Results.Ok(new ApiResponse<T>(result.Value!, context.TraceIdentifier))
            : ToProblem(result.ErrorCode!, result.ErrorMessage!, context);
    }

    private static IResult ToProblem(string code, string message, HttpContext context)
    {
        var status = code switch
        {
            "module_instance_not_found" or "module_command_not_found" => StatusCodes.Status404NotFound,
            "idempotency_conflict" or "module_command_lease_conflict" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return Results.Problem(
            statusCode: status,
            title: message,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = context.TraceIdentifier
            });
    }
}
