using System.Security.Cryptography;
using System.Text;
using Devkit.FileStorage.Application.Files;
using Devkit.FileStorage.Infrastructure.Files;
using Microsoft.Extensions.Options;

namespace Devkit.FileStorage.Api.Endpoints;
public sealed class FileClientFilter(IOptions<FileStorageOptions> configured) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        var name = request.Headers["X-File-Service"].ToString();
        var key = request.Headers["X-File-Key"].ToString();
        if (!configured.Value.Clients.TryGetValue(name, out var client) || string.IsNullOrEmpty(key)
            || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(key)), SHA256.HashData(Encoding.UTF8.GetBytes(client.ApiKey))))
            throw new FileStorageException(401, "service_unauthorized", "文件服务调用凭据无效。");
        context.HttpContext.Items[typeof(FileClientOptions)] = client;
        return await next(context);
    }
    public static string Module(HttpContext context) => ((FileClientOptions)context.Items[typeof(FileClientOptions)]!).Module;
}
