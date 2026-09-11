namespace Devkit.Server.Api.Contracts;

public sealed record ApiResponse<T>(T Data, string TraceId);
