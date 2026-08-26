using System.IO;
using Ssamc.Core.ApiCodeCollector;
using Ssamc.Servers;
using Xunit;

namespace Devkit.Loading.Tests;

public sealed class ApiUpdateServerCacheTests : IDisposable
{
    private const string ApiCode = "API001";
    private readonly string _testDirectory;
    private readonly string _sourceDirectory;
    private readonly string _sourceFile;
    private readonly string _cacheDatabase;

    public ApiUpdateServerCacheTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"devkit-api-cache-tests-{Guid.NewGuid():N}");
        _sourceDirectory = Path.Combine(_testDirectory, "source");
        _sourceFile = Path.Combine(_sourceDirectory, "SampleApi.cs");
        _cacheDatabase = Path.Combine(_testDirectory, "cache", "source-cache.db");
        Directory.CreateDirectory(_sourceDirectory);
    }

    [Fact]
    public void Unchanged_files_are_served_from_sqlite_across_scanner_instances()
    {
        File.WriteAllText(_sourceFile, CreateSource("cachedValue"));
        var firstServer = CreateServer(_cacheDatabase);

        Assert.Contains("cachedValue", firstServer.GetExecutionSourceCodeByApiCode(_sourceDirectory, ApiCode));
        Assert.Contains("cachedValue", firstServer.GetSourceCodeByApiCode(_sourceDirectory, ApiCode));
        Assert.True(File.Exists(_cacheDatabase));

        using var lockedSource = File.Open(_sourceFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var secondServer = CreateServer(_cacheDatabase);

        Assert.Contains("cachedValue", secondServer.GetExecutionSourceCodeByApiCode(_sourceDirectory, ApiCode));
        Assert.Contains("cachedValue", secondServer.GetSourceCodeByApiCode(_sourceDirectory, ApiCode));
    }

    [Fact]
    public void Modified_file_invalidates_both_execution_and_extension_source()
    {
        File.WriteAllText(_sourceFile, CreateSource("oldValue"));
        var originalWriteTime = File.GetLastWriteTimeUtc(_sourceFile);
        var firstServer = CreateServer(_cacheDatabase);
        Assert.Contains("oldValue", firstServer.GetExecutionSourceCodeByApiCode(_sourceDirectory, ApiCode));

        File.WriteAllText(_sourceFile, CreateSource("newValue"));
        File.SetLastWriteTimeUtc(_sourceFile, originalWriteTime.AddSeconds(2));
        var secondServer = CreateServer(_cacheDatabase);

        var executionSource = secondServer.GetExecutionSourceCodeByApiCode(_sourceDirectory, ApiCode);
        var extensionSource = secondServer.GetSourceCodeByApiCode(_sourceDirectory, ApiCode);
        Assert.Contains("newValue", executionSource);
        Assert.DoesNotContain("oldValue", executionSource);
        Assert.Contains("newValue", extensionSource);
        Assert.DoesNotContain("oldValue", extensionSource);
    }

    [Fact]
    public void Deleted_file_is_removed_from_the_cache()
    {
        File.WriteAllText(_sourceFile, CreateSource("deletedValue"));
        var firstServer = CreateServer(_cacheDatabase);
        Assert.NotEmpty(firstServer.GetExecutionSourceCodeByApiCode(_sourceDirectory, ApiCode));

        File.Delete(_sourceFile);
        var secondServer = CreateServer(_cacheDatabase);

        Assert.Empty(secondServer.GetExecutionSourceCodeByApiCode(_sourceDirectory, ApiCode));
        Assert.Empty(secondServer.GetSourceCodeByApiCode(_sourceDirectory, ApiCode));
    }

    [Fact]
    public void Missing_source_directory_returns_empty_without_creating_cache()
    {
        var missingDirectory = Path.Combine(_testDirectory, "missing");
        var server = CreateServer(_cacheDatabase);

        Assert.Empty(server.GetExecutionSourceCodeByApiCode(missingDirectory, ApiCode));
        Assert.Empty(server.GetSourceCodeByApiCode(missingDirectory, ApiCode));
        Assert.False(File.Exists(_cacheDatabase));
    }

    [Fact]
    public void Cache_failure_falls_back_to_direct_scanning()
    {
        File.WriteAllText(_sourceFile, CreateSource("fallbackValue"));
        var blockedDirectory = Path.Combine(_testDirectory, "blocked");
        File.WriteAllText(blockedDirectory, "this path is a file");
        var server = CreateServer(Path.Combine(blockedDirectory, "source-cache.db"));

        Assert.Contains("fallbackValue", server.GetExecutionSourceCodeByApiCode(_sourceDirectory, ApiCode));
        Assert.Contains("fallbackValue", server.GetSourceCodeByApiCode(_sourceDirectory, ApiCode));
    }

    [Fact]
    public void Execution_source_is_formatted_and_indented_as_method_body_code()
    {
        File.WriteAllText(
            _sourceFile,
            $$"""
              public class SampleApi
              {
                  [ApiSourceCode("{{ApiCode}}")] public string Execute(){var result="formattedValue";if(result.Length>0){return result;}return string.Empty;}
              }
              """);
        var server = CreateServer(_cacheDatabase);

        var executionSource = server.GetExecutionSourceCodeByApiCode(_sourceDirectory, ApiCode);

        Assert.All(
            executionSource.Split(Environment.NewLine).Where(line => !string.IsNullOrWhiteSpace(line)),
            line => Assert.StartsWith("    ", line));
        var expected = string.Join(
            Environment.NewLine,
            "    var result = \"formattedValue\";",
            "    if (result.Length > 0)",
            "    {",
            "        return result;",
            "    }",
            string.Empty,
            "    return string.Empty;");
        Assert.Equal(expected, executionSource);
    }

    public void Dispose()
    {
        Directory.Delete(_testDirectory, recursive: true);
    }

    private static ApiUpdateServer CreateServer(string cacheDatabase)
    {
        return new ApiUpdateServer(RoslynApiScanner.CreateWithCacheDatabase(cacheDatabase));
    }

    private static string CreateSource(string value)
    {
        return $$"""
            [ApiExtendCode("{{ApiCode}}", Description = "{{value}}")] 
            public class SampleApi
            {
                [ApiSourceCode("{{ApiCode}}")]
                public string Execute()
                {
                    var result = "{{value}}";
                    return result;
                }
            }
            """;
    }
}
