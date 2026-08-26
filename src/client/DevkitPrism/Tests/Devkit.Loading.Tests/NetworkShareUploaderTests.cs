using System.IO;
using Devkit.Core.Helpers;
using Xunit;

namespace Devkit.Loading.Tests;

public sealed class NetworkShareUploaderTests
{
    [Fact]
    public void Upload_uses_current_windows_access_before_configured_credentials()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            $"devkit-network-share-{Guid.NewGuid():N}");
        var sourcePath = Path.Combine(testRoot, "source.txt");
        var shareRoot = Path.Combine(testRoot, "share");

        Directory.CreateDirectory(shareRoot);
        File.WriteAllText(sourcePath, "content");

        try
        {
            using var uploader = new NetworkShareUploader(
                shareRoot,
                "credentials-must-not-be-used",
                "credentials-must-not-be-used");

            uploader.UploadFile(sourcePath, @"nested\uploaded.txt");

            Assert.Equal(
                "content",
                File.ReadAllText(Path.Combine(shareRoot, "nested", "uploaded.txt")));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public void Connect_without_access_or_fallback_credentials_reports_clear_error()
    {
        var unavailableRoot = Path.Combine(
            Path.GetTempPath(),
            $"devkit-missing-share-{Guid.NewGuid():N}");
        using var uploader = new NetworkShareUploader(unavailableRoot, string.Empty, string.Empty);

        var exception = Assert.Throws<InvalidOperationException>(uploader.Connect);

        Assert.Contains("当前 Windows 用户无法访问", exception.Message);
        Assert.Contains("未配置", exception.Message);
    }
}
