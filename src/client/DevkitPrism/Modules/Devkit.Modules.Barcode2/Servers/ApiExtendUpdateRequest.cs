namespace Barcode2.Servers;

public enum ApiLookupKind
{
    Code,
    Name
}

public sealed record ApiExtendUpdateRequest(
    ApiLookupKind LookupKind,
    string Identifier,
    string ExtendCode);
