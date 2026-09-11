using System.Diagnostics;

namespace Barcode2.Servers;

public sealed class SystemWebPageLauncher : IWebPageLauncher
{
    #region IWebPageLauncher Members
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
    #endregion

    public static Uri BuildDeepLink(WebTabOpenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Uri.TryCreate(request.BaseAddress, UriKind.Absolute, out var baseAddress) ||
            baseAddress.Scheme != Uri.UriSchemeHttp && baseAddress.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"页面环境地址无效：{request.BaseAddress}");
        }

        if (request.MainPageId <= 0)
        {
            throw new InvalidOperationException("菜单模块未配置有效的主页面 ID。");
        }

        var builder = new UriBuilder(baseAddress)
        {
            Path = "/Page/",
            Query = $"moduleid={request.MainPageId}"
        };

        return builder.Uri;
    }
}
