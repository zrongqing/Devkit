namespace Barcode2.Configuration;

public interface IBarcode2ConfigurationService
{
    Task<Barcode2Settings> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(Barcode2Settings settings, CancellationToken cancellationToken = default);

    Task<string> GetDatabaseConnectionStringAsync(
        string environmentKey,
        CancellationToken cancellationToken = default);
}
