namespace Barcode2.Configuration;

/// <summary>
/// Stable environment identifiers retained for XAML command parameters.
/// Runtime configuration is provided by <see cref="IBarcode2ConfigurationService"/>.
/// </summary>
public static class Barcode2Environment
{
    public const string ProductionEnvironment = Barcode2Defaults.ProductionEnvironment;
    public const string TestEnvironment = Barcode2Defaults.TestEnvironment;
    public const string DevelopmentEnvironment = Barcode2Defaults.DevelopmentEnvironment;
}
