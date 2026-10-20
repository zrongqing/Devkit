using System.Security.Cryptography;
using Devkit.Identity.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Devkit.Identity.Infrastructure.Identity;

public sealed class JwtKeyMaterial : IDisposable
{
    private readonly RSA rsa = RSA.Create();
    public SecurityKey ValidationKey { get; }
    public string Algorithm => SecurityAlgorithms.RsaSha256;
    public SigningCredentials SigningCredentials { get; }

    public JwtKeyMaterial(IOptions<JwtOptions> options)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(options.Value.SigningPrivateKeyPath))
                throw new InvalidOperationException("Jwt:SigningPrivateKeyPath is required by the identity service.");
            rsa.ImportFromPem(File.ReadAllText(options.Value.SigningPrivateKeyPath));
            if (rsa.KeySize < 2048) throw new InvalidOperationException("JWT RSA keys must have at least 2048 bits.");
            rsa.ExportParameters(includePrivateParameters: true);
            ValidationKey = new RsaSecurityKey(rsa) { KeyId = Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo())) };
            SigningCredentials = new SigningCredentials(ValidationKey, Algorithm);
        }
        catch { rsa.Dispose(); throw; }
    }

    public void Dispose() => rsa.Dispose();
}
