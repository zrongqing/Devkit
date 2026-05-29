using Devkit.Server.Infrastructure.SystemInfo;

namespace Devkit.Server.Application.Tests;

public sealed class SystemInfoServiceTests
{
    [Fact]
    public void GetInfo_returns_runtime_metadata()
    {
        var service = new SystemInfoService(new FakeTimeProvider(), new ServerRuntimeOptions("Devkit Server", "1.0.0", "Test"));

        var result = service.GetInfo();

        Assert.Equal("Devkit Server", result.ServiceName);
        Assert.Equal("Test", result.Environment);
        Assert.Equal(new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.Zero), result.ServerTime);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 7, 31, 0, 0, 0, TimeSpan.Zero);
    }
}
