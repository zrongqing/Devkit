using Devkit.Infrastructure.Security;
using System.Security.Cryptography;
using Devkit.Identity.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Devkit.Identity.Infrastructure.Identity;

public sealed class JwtKeyMaterial : IDisposable
{
    private readonly RSA rsa;
    public SecurityKey ValidationKey { get; }
    public string Algorithm => SecurityAlgorithms.RsaSha256;
    public SigningCredentials SigningCredentials { get; }

    public JwtKeyMaterial(IOptions<JwtOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.SigningPrivateKeyPath))
            throw new InvalidOperationException("Jwt:SigningPrivateKeyPath is required by the identity service.");
        rsa = RsaKeyLoader.ReadPrivateKey(options.Value.SigningPrivateKeyPath);
        ValidationKey = RsaKeyLoader.CreateSecurityKey(rsa);
        SigningCredentials = new SigningCredentials(ValidationKey, Algorithm);
    }

    public void Dispose() => rsa.Dispose();
}
