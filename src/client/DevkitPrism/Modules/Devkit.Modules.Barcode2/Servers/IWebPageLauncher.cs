namespace Barcode2.Servers;

/// <summary>
/// Opens a web page. The abstraction keeps navigation policy replaceable and testable.
/// </summary>
public interface IWebPageLauncher
{
    Task OpenTabAsync(WebTabOpenRequest request, CancellationToken cancellationToken);
}

public sealed record WebTabOpenRequest(string Title, string BaseAddress, long MainPageId);
