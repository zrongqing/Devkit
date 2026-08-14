using System.Diagnostics;
namespace Ssamc.Servers;

public sealed class SystemWebPageLauncher : IWebPageLauncher
{
    public Task OpenTabAsync(WebTabOpenRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var address = BuildDeepLink(request);

        Process.Start(new ProcessStartInfo
        {
            FileName = address.AbsoluteUri,
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }

    public static Uri BuildDeepLink(WebTabOpenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Uri.TryCreate(request.BaseAddress, UriKind.Absolute, out var baseAddress) ||
            (baseAddress.Scheme != Uri.UriSchemeHttp && baseAddress.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"页面环境地址无效：{request.BaseAddress}");
        }

        if (request.ModuleId <= 0)
        {
            throw new InvalidOperationException("菜单未配置有效的模块 ID。");
        }

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? request.ModuleId.ToString()
            : request.Title.Trim();
        var pageUrl = $"/Page/?moduleid={request.ModuleId}";
        var builder = new UriBuilder(baseAddress)
        {
            Path = $"{pageUrl}",
        };

        return builder.Uri;
    }
}
