using System.Net.Http.Json;
using Devkit.Contracts;

namespace Devkit.Services;

public sealed class SystemInfoClient(HttpClient httpClient) : ISystemInfoClient
{
    public async Task<SystemInfoDto> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<ApiResponse<SystemInfoDto>>("api/v1/system/info", cancellationToken);
        return result?.Data ?? throw new InvalidOperationException("The server returned an empty system info response.");
    }
}
