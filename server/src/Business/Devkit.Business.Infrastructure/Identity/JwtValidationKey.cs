using System.Security.Cryptography;
using Devkit.Business.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Devkit.Business.Infrastructure.Identity;

public sealed class JwtValidationKey : IDisposable
{
    private readonly RSA rsa = RSA.Create();
    public SecurityKey ValidationKey { get; }
    public string Algorithm => SecurityAlgorithms.RsaSha256;

    public JwtValidationKey(IOptions<JwtOptions> options)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(options.Value.ValidationPublicKeyPath))
                throw new InvalidOperationException("Jwt:ValidationPublicKeyPath is required by the business service.");
            var pem = File.ReadAllText(options.Value.ValidationPublicKeyPath);
            if (pem.Contains("PRIVATE KEY", StringComparison.Ordinal))
                throw new InvalidOperationException("The business service accepts only a public key.");
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < 2048) throw new InvalidOperationException("JWT RSA keys must have at least 2048 bits.");
            ValidationKey = new RsaSecurityKey(rsa) { KeyId = Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo())) };
        }
        catch { rsa.Dispose(); throw; }
    }

    public void Dispose() => rsa.Dispose();
}
