using System.IO;
using Barcode2.Core.ApiCodeCollector;
using Barcode2.Servers;
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

    [Fact]
    public void Name_only_sources_are_cached_and_retrieved_independently()
    {
        File.WriteAllText(
            _sourceFile,
            """
            [ApiExtendCode(Name = "Api One")]
            public class NameApi
            {
                [ApiSourceCode(Name = "Api One")]
                public string Execute()
                {
                    return "nameValue";
                }
            }
            """);
        var firstServer = CreateServer(_cacheDatabase);

        Assert.Contains("nameValue", firstServer.GetSourceCodeByApiName(_sourceDirectory, "Api One"));
        Assert.Contains("nameValue", firstServer.GetExecutionSourceCodeByApiName(_sourceDirectory, "Api One"));
        Assert.Empty(firstServer.GetSourceCodeByApiCode(_sourceDirectory, "Api One"));

        using var lockedSource = File.Open(_sourceFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var secondServer = CreateServer(_cacheDatabase);
        Assert.Contains("nameValue", secondServer.GetSourceCodeByApiName(_sourceDirectory, "Api One"));
        Assert.Contains("nameValue", secondServer.GetExecutionSourceCodeByApiName(_sourceDirectory, "Api One"));
    }

    [Fact]
    public void Code_wins_within_one_attribute_but_separate_name_attribute_is_retained()
    {
        File.WriteAllText(
            _sourceFile,
            """
            [ApiExtendCode("CODE001", Name = "Ignored Name")]
            [ApiExtendCode(Name = "Retained Name")]
            public class MixedApi
            {
                [ApiSourceCode("CODE001", Name = "Ignored Name")]
                [ApiSourceCode(Name = "Retained Name")]
                public string Execute()
                {
                    return "mixedValue";
                }
            }
            """);
        var server = CreateServer(_cacheDatabase);

        Assert.Contains("mixedValue", server.GetSourceCodeByApiCode(_sourceDirectory, "CODE001"));
        Assert.Contains("mixedValue", server.GetSourceCodeByApiName(_sourceDirectory, "Retained Name"));
        Assert.Empty(server.GetSourceCodeByApiName(_sourceDirectory, "Ignored Name"));
        Assert.Contains("mixedValue", server.GetExecutionSourceCodeByApiCode(_sourceDirectory, "CODE001"));
        Assert.Contains("mixedValue", server.GetExecutionSourceCodeByApiName(_sourceDirectory, "Retained Name"));
        Assert.Empty(server.GetExecutionSourceCodeByApiName(_sourceDirectory, "Ignored Name"));
    }

    [Fact]
    public void Equal_code_and_name_values_do_not_share_cache_entries()
    {
        File.WriteAllText(
            _sourceFile,
            """
            [ApiExtendCode("Shared")]
            public class CodeApi { public string Value => "codeValue"; }

            [ApiExtendCode(Name = "Shared")]
            public class NameApi { public string Value => "nameValue"; }
            """);
        var server = CreateServer(_cacheDatabase);

        var codeSource = server.GetSourceCodeByApiCode(_sourceDirectory, "Shared");
        var nameSource = server.GetSourceCodeByApiName(_sourceDirectory, "Shared");
        Assert.Contains("codeValue", codeSource);
        Assert.DoesNotContain("nameValue", codeSource);
        Assert.Contains("nameValue", nameSource);
        Assert.DoesNotContain("codeValue", nameSource);
    }

    [Fact]
    public void Modified_file_invalidates_name_extension_and_execution_sources()
    {
        File.WriteAllText(_sourceFile, CreateNameSource("oldNameValue"));
        var originalWriteTime = File.GetLastWriteTimeUtc(_sourceFile);
        var firstServer = CreateServer(_cacheDatabase);
        Assert.Contains("oldNameValue", firstServer.GetSourceCodeByApiName(_sourceDirectory, "Api Name"));

        File.WriteAllText(_sourceFile, CreateNameSource("newNameValue"));
        File.SetLastWriteTimeUtc(_sourceFile, originalWriteTime.AddSeconds(2));
        var secondServer = CreateServer(_cacheDatabase);

        var extensionSource = secondServer.GetSourceCodeByApiName(_sourceDirectory, "Api Name");
        var executionSource = secondServer.GetExecutionSourceCodeByApiName(_sourceDirectory, "Api Name");
        Assert.Contains("newNameValue", extensionSource);
        Assert.DoesNotContain("oldNameValue", extensionSource);
        Assert.Contains("newNameValue", executionSource);
        Assert.DoesNotContain("oldNameValue", executionSource);
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

    private static string CreateNameSource(string value)
    {
        return $$"""
            [ApiExtendCode(Name = "Api Name")]
            public class SampleApi
            {
                [ApiSourceCode(Name = "Api Name")]
                public string Execute()
                {
                    return "{{value}}";
                }
            }
            """;
    }
}
