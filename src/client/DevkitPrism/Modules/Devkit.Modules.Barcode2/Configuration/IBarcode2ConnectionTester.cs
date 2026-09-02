namespace Barcode2.Configuration;

public interface IBarcode2ConnectionTester
{
    Task TestEnvironmentAsync(
        Barcode2EnvironmentSettings environment,
        CancellationToken cancellationToken = default);

    Task TestShareAsync(
        Barcode2ShareSettings target,
        CancellationToken cancellationToken = default);
}
