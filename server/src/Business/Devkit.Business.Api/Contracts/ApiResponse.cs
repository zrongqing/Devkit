namespace Devkit.Business.Api.Contracts;

public sealed record ApiResponse<T>(T Data, string TraceId);
