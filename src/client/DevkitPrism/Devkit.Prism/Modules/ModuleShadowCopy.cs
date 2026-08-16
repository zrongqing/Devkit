using System.IO;

namespace Devkit.Prism.Modules;

internal sealed class ModuleShadowCopy
{
    private static readonly ShadowCopySession Session = ShadowCopySession.Create();
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _copies = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _sourceDirectory;

    private ModuleShadowCopy(string sourceAssemblyPath)
    {
        SourceAssemblyPath = Path.GetFullPath(sourceAssemblyPath);
        _sourceDirectory = Path.GetDirectoryName(SourceAssemblyPath)
            ?? throw new InvalidOperationException("The module assembly does not have a parent directory.");
        DirectoryPath = Path.Combine(
            Session.DirectoryPath,
            $"{Path.GetFileNameWithoutExtension(SourceAssemblyPath)}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(DirectoryPath);

        try
        {
            AssemblyPath = CopyFile(SourceAssemblyPath);
            CopySidecar(Path.ChangeExtension(SourceAssemblyPath, ".deps.json"));
            CopySidecar(Path.ChangeExtension(SourceAssemblyPath, ".runtimeconfig.json"));
            CopySidecar($"{SourceAssemblyPath}.config");
        }
        catch
        {
            TryDeleteDirectory(DirectoryPath);
            throw;
        }
    }

    ~ModuleShadowCopy() => TryDeleteDirectory(DirectoryPath);

    public string SourceAssemblyPath { get; }

    public string AssemblyPath { get; }

    public string DirectoryPath { get; }

    public static ModuleShadowCopy Create(string sourceAssemblyPath) => new(sourceAssemblyPath);

    public string CopyFile(string sourcePath)
    {
        var fullSourcePath = Path.GetFullPath(sourcePath);
        lock (_gate)
        {
            if (_copies.TryGetValue(fullSourcePath, out var existingCopy))
            {
                return existingCopy;
            }

            if (!File.Exists(fullSourcePath))
            {
                throw new FileNotFoundException("A module dependency does not exist.", fullSourcePath);
            }

            var relativePath = Path.GetRelativePath(_sourceDirectory, fullSourcePath);
            if (Path.IsPathRooted(relativePath) ||
                relativePath == ".." ||
                relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                relativePath = Path.Combine("external", Guid.NewGuid().ToString("N"), Path.GetFileName(fullSourcePath));
            }

            var destinationPath = Path.GetFullPath(Path.Combine(DirectoryPath, relativePath));
            if (!destinationPath.StartsWith(
                    DirectoryPath + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Invalid module dependency path: {fullSourcePath}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using (var source = new FileStream(
                       fullSourcePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            using (var destination = new FileStream(
                       destinationPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.Read))
            {
                source.CopyTo(destination);
            }

            _copies[fullSourcePath] = destinationPath;
            return destinationPath;
        }
    }

    private void CopySidecar(string path)
    {
        if (File.Exists(path))
        {
            CopyFile(path);
        }
    }

    private static bool TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed class ShadowCopySession
    {
        private readonly FileStream _lockStream;

        private ShadowCopySession(string directoryPath, FileStream lockStream)
        {
            DirectoryPath = directoryPath;
            _lockStream = lockStream;
        }

        public string DirectoryPath { get; }

        ~ShadowCopySession() => _lockStream.Dispose();

        public static ShadowCopySession Create()
        {
            var rootDirectory = Path.Combine(Path.GetTempPath(), "Devkit", "module-shadow");
            Directory.CreateDirectory(rootDirectory);
            CleanupStaleSessions(rootDirectory);

            var sessionDirectory = Path.Combine(
                rootDirectory,
                $"{Environment.ProcessId}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(sessionDirectory);
            var lockStream = new FileStream(
                Path.Combine(sessionDirectory, ".session.lock"),
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.Read);
            return new ShadowCopySession(sessionDirectory, lockStream);
        }

        private static void CleanupStaleSessions(string rootDirectory)
        {
            foreach (var sessionDirectory in Directory.EnumerateDirectories(rootDirectory))
            {
                var lockPath = Path.Combine(sessionDirectory, ".session.lock");
                try
                {
                    if (File.Exists(lockPath))
                    {
                        using var cleanupLock = new FileStream(
                            lockPath,
                            FileMode.Open,
                            FileAccess.ReadWrite,
                            FileShare.None);
                    }

                    TryDeleteDirectory(sessionDirectory);
                }
                catch (IOException)
                {
                    // Another Devkit process still owns this session.
                }
                catch (UnauthorizedAccessException)
                {
                    // Leave inaccessible stale data for a later cleanup attempt.
                }
            }
        }
    }
}
