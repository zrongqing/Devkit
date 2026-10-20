namespace Devkit.AspNetCore.Contracts;

public sealed record ApiResponse<T>(T Data, string TraceId);
