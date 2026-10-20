using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Devkit.Infrastructure.Security;

public static class RsaKeyLoader
{
    public static RSA ReadPrivateKey(string path) => Read(path, privateKey: true);
    public static RSA ReadPublicKey(string path) => Read(path, privateKey: false);

    public static RsaSecurityKey CreateSecurityKey(RSA key) => new(key)
    {
        KeyId = Convert.ToHexString(SHA256.HashData(key.ExportSubjectPublicKeyInfo()))
    };

    private static RSA Read(string path, bool privateKey)
    {
        var pem = File.ReadAllText(path);
        if (!privateKey && pem.Contains("PRIVATE KEY", StringComparison.Ordinal))
            throw new InvalidOperationException("RSA validation requires a public key; private key PEM is not accepted.");
        var key = RSA.Create();
        try
        {
            key.ImportFromPem(pem);
            if (key.KeySize < 2048)
                throw new InvalidOperationException("JWT RSA keys must have at least 2048 bits.");
            if (privateKey) key.ExportParameters(includePrivateParameters: true);
            return key;
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }
}
