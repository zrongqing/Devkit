using System.Security.Cryptography;
using System.Text;

namespace Devkit.Infrastructure.Security;

public static class DevelopmentJwtKeyStore
{
    public static (string PrivateKeyPath, string PublicKeyPath) GetOrCreate(string directory)
    {
        directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var lockIdentity = OperatingSystem.IsWindows() ? directory.ToUpperInvariant() : directory;
        using var mutex = new Mutex(false, "Devkit.DevelopmentJwt." +
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lockIdentity))));
        try
        {
            if (!mutex.WaitOne(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("Timed out waiting for the shared development JWT keys.");
        }
        catch (AbandonedMutexException)
        {
            // The previous process exited during initialization; validate the persisted files below.
        }

        try
        {
            if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
            else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var privatePath = Path.Combine(directory, "identity-private.pem");
            var publicPath = Path.Combine(directory, "validation-public.pem");
            if (!File.Exists(privatePath))
            {
                if (File.Exists(publicPath))
                    throw new InvalidOperationException("The development JWT private key is missing while its public key exists. Restore the private key or explicitly reset both development keys.");
                using var generated = RSA.Create(3072);
                WriteNewPem(privatePath, generated.ExportPkcs8PrivateKeyPem());
            }

            using var privateKey = RsaKeyLoader.ReadPrivateKey(privatePath);
            if (!File.Exists(publicPath))
                WriteNewPem(publicPath, privateKey.ExportSubjectPublicKeyInfoPem());

            using var publicKey = RsaKeyLoader.ReadPublicKey(publicPath);
            if (RsaKeyLoader.CreateSecurityKey(privateKey).KeyId != RsaKeyLoader.CreateSecurityKey(publicKey).KeyId)
                throw new InvalidOperationException("The persisted development JWT private and public keys do not match. Restore a matching pair or explicitly reset both development keys.");
            return (privatePath, publicPath);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    private static void WriteNewPem(string path, string pem)
    {
        // Publish complete files only. A failed startup must never leave a partially written key.
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var stream = new FileStream(temporaryPath, options))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(pem);
            File.Move(temporaryPath, path, overwrite: false);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
