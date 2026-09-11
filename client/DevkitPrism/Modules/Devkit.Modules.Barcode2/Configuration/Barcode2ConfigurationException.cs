namespace Barcode2.Configuration;

public sealed class Barcode2ConfigurationException : InvalidOperationException
{
    public Barcode2ConfigurationException(string message) : base(message)
    {
    }

    public Barcode2ConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
