namespace Devkit.Business.Infrastructure.Configuration;

public sealed class IdentityServiceOptions
{
    public string BaseUrl { get; set; } = "https://localhost:12512";
    public bool RequireHttps { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 5;

    public static bool IsValid(IdentityServiceOptions options) =>
        Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && (!options.RequireHttps || uri.Scheme == "https")
        && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.UserInfo.Length == 0;
}
