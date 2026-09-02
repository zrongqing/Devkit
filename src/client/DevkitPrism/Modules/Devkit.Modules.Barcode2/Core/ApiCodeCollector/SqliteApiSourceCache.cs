using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Data.Sqlite;

namespace Barcode2.Core.ApiCodeCollector;

internal sealed class SqliteApiSourceCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);
    private readonly string _databasePath;

    public SqliteApiSourceCache(string databasePath)
    {
        _databasePath = Path.GetFullPath(databasePath);
    }

    public static string GetDefaultDatabasePath()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var cacheRoot = string.IsNullOrWhiteSpace(localApplicationData)
                            ? Path.Combine(Path.GetTempPath(), "Devkit")
                            : Path.Combine(localApplicationData, "Devkit");
        return Path.Combine(cacheRoot, "Cache", "barcode2-api-source-cache.db");
    }

    public bool TrySynchronize(
        string sourcePath,
        string searchPattern,
        IReadOnlyList<string> files,
        string parserKey,
        Func<string, string, SourceFileAnalysis> analyze)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
            using var connection = OpenConnection();
            EnsureSchema(connection);
            using var transaction = connection.BeginTransaction();

            var sourceRoot = NormalizeSourceRoot(sourcePath);
            var existingFiles = ReadExistingFiles(connection, transaction, sourceRoot, searchPattern);
            var currentFiles = files.ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var deletedFile in existingFiles.Keys.Where(file => !currentFiles.Contains(file)))
            {
                DeleteFile(connection, transaction, sourceRoot, searchPattern, deletedFile);
            }

            foreach (var filePath in files)
            {
                try
                {
                    var file = new FileInfo(filePath);
                    var fileLength = file.Length;
                    var lastWriteUtcTicks = file.LastWriteTimeUtc.Ticks;
                    existingFiles.TryGetValue(filePath, out var cachedFile);

                    if (cachedFile is not null &&
                        cachedFile.FileLength == fileLength &&
                        cachedFile.LastWriteUtcTicks == lastWriteUtcTicks &&
                        string.Equals(cachedFile.ParserKey, parserKey, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var sourceText = File.ReadAllText(filePath);
                    var contentHash = ComputeContentHash(sourceText);
                    if (cachedFile is not null &&
                        string.Equals(cachedFile.ContentHash, contentHash, StringComparison.Ordinal) &&
                        string.Equals(cachedFile.ParserKey, parserKey, StringComparison.Ordinal))
                    {
                        UpdateFileMetadata(
                            connection,
                            transaction,
                            sourceRoot,
                            searchPattern,
                            filePath,
                            fileLength,
                            lastWriteUtcTicks);
                        continue;
                    }

                    var analysis = analyze(filePath, sourceText);
                    ReplaceFileAnalysis(
                        connection,
                        transaction,
                        sourceRoot,
                        searchPattern,
                        filePath,
                        fileLength,
                        lastWriteUtcTicks,
                        contentHash,
                        parserKey,
                        analysis);
                }
                catch (Exception ex)
                {
                    // Do not serve stale analysis for a file that can no longer be read or parsed.
                    DeleteFile(connection, transaction, sourceRoot, searchPattern, filePath);
                    Console.WriteLine($"更新源代码缓存 {filePath} 时出错: {ex.Message}");
                }
            }

            transaction.Commit();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SQLite 源代码缓存不可用，已降级为直接扫描: {ex.Message}");
            return false;
        }
    }

    public bool TryGetAllApiSources(
        string sourcePath,
        string searchPattern,
        out List<ApiSourceInfo> apiSources)
    {
        return TryRead(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                                  SELECT PayloadJson
                                  FROM SourceFileAnalysisCache
                                  WHERE SourceRoot = $sourceRoot AND SearchPattern = $searchPattern
                                  ORDER BY FilePath COLLATE NOCASE;
                                  """;
            AddScopeParameters(command, sourcePath, searchPattern);
            return ReadApiSources(command, null, false);
        }, out apiSources);
    }

    public bool TryGetApiSources(
        string sourcePath,
        string searchPattern,
        string apiCode,
        out List<ApiSourceInfo> apiSources)
    {
        return TryRead(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                                  SELECT source.PayloadJson
                                  FROM SourceFileAnalysisCache AS source
                                  INNER JOIN ApiSourceCodeIndex AS api
                                      ON api.SourceRoot = source.SourceRoot
                                      AND api.SearchPattern = source.SearchPattern
                                      AND api.FilePath = source.FilePath
                                  WHERE source.SourceRoot = $sourceRoot
                                      AND source.SearchPattern = $searchPattern
                                      AND api.ApiCode = $apiCode
                                  ORDER BY source.FilePath COLLATE NOCASE;
                                  """;
            AddScopeParameters(command, sourcePath, searchPattern);
            command.Parameters.AddWithValue("$apiCode", apiCode);
            return ReadApiSources(command, apiCode, false);
        }, out apiSources);
    }

    public bool TryGetApiSourcesByName(
        string sourcePath,
        string searchPattern,
        string apiName,
        out List<ApiSourceInfo> apiSources)
    {
        return TryRead(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                                  SELECT source.PayloadJson
                                  FROM SourceFileAnalysisCache AS source
                                  INNER JOIN ApiSourceNameIndex AS api
                                      ON api.SourceRoot = source.SourceRoot
                                      AND api.SearchPattern = source.SearchPattern
                                      AND api.FilePath = source.FilePath
                                  WHERE source.SourceRoot = $sourceRoot
                                      AND source.SearchPattern = $searchPattern
                                      AND api.ApiName = $apiName
                                  ORDER BY source.FilePath COLLATE NOCASE;
                                  """;
            AddScopeParameters(command, sourcePath, searchPattern);
            command.Parameters.AddWithValue("$apiName", apiName);
            return ReadApiSources(command, apiName, true);
        }, out apiSources);
    }

    public bool TryGetExecutionSources(
        string sourcePath,
        string searchPattern,
        string apiCode,
        out List<string> executionSources)
    {
        return TryRead(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                                  SELECT SourceCode
                                  FROM ApiExecutionSourceCache
                                  WHERE SourceRoot = $sourceRoot
                                      AND SearchPattern = $searchPattern
                                      AND ApiCode = $apiCode
                                  ORDER BY FilePath COLLATE NOCASE, SourceOrder;
                                  """;
            AddScopeParameters(command, sourcePath, searchPattern);
            command.Parameters.AddWithValue("$apiCode", apiCode);

            var sources = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                sources.Add(reader.GetString(0));
            }

            return sources;
        }, out executionSources);
    }

    public bool TryGetExecutionSourcesByName(
        string sourcePath,
        string searchPattern,
        string apiName,
        out List<string> executionSources)
    {
        return TryRead(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                                  SELECT SourceCode
                                  FROM ApiExecutionSourceNameCache
                                  WHERE SourceRoot = $sourceRoot
                                      AND SearchPattern = $searchPattern
                                      AND ApiName = $apiName
                                  ORDER BY FilePath COLLATE NOCASE, SourceOrder;
                                  """;
            AddScopeParameters(command, sourcePath, searchPattern);
            command.Parameters.AddWithValue("$apiName", apiName);

            var sources = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                sources.Add(reader.GetString(0));
            }

            return sources;
        }, out executionSources);
    }

    private bool TryRead<T>(Func<SqliteConnection, T> read, out T value)
    {
        try
        {
            using var connection = OpenConnection();
            value = read(connection);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"读取 SQLite 源代码缓存失败，已降级为直接扫描: {ex.Message}");
            value = default!;
            return false;
        }
    }

    private SqliteConnection OpenConnection()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            DefaultTimeout = 5
        }.ToString();
        var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout = 5000;";
        command.ExecuteNonQuery();
        return connection;
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              PRAGMA journal_mode = WAL;
                              PRAGMA synchronous = NORMAL;

                              CREATE TABLE IF NOT EXISTS SourceFileAnalysisCache (
                                  SourceRoot TEXT NOT NULL COLLATE NOCASE,
                                  SearchPattern TEXT NOT NULL,
                                  FilePath TEXT NOT NULL COLLATE NOCASE,
                                  FileLength INTEGER NOT NULL,
                                  LastWriteUtcTicks INTEGER NOT NULL,
                                  ContentHash TEXT NOT NULL,
                                  ParserKey TEXT NOT NULL,
                                  PayloadJson TEXT NOT NULL,
                                  PRIMARY KEY (SourceRoot, SearchPattern, FilePath)
                              );

                              CREATE TABLE IF NOT EXISTS ApiSourceCodeIndex (
                                  SourceRoot TEXT NOT NULL COLLATE NOCASE,
                                  SearchPattern TEXT NOT NULL,
                                  FilePath TEXT NOT NULL COLLATE NOCASE,
                                  ApiCode TEXT NOT NULL,
                                  PRIMARY KEY (SourceRoot, SearchPattern, FilePath, ApiCode)
                              );

                              CREATE INDEX IF NOT EXISTS IX_ApiSourceCodeIndex_Lookup
                                  ON ApiSourceCodeIndex (SourceRoot, SearchPattern, ApiCode);

                              CREATE TABLE IF NOT EXISTS ApiSourceNameIndex (
                                  SourceRoot TEXT NOT NULL COLLATE NOCASE,
                                  SearchPattern TEXT NOT NULL,
                                  FilePath TEXT NOT NULL COLLATE NOCASE,
                                  ApiName TEXT NOT NULL,
                                  PRIMARY KEY (SourceRoot, SearchPattern, FilePath, ApiName)
                              );

                              CREATE INDEX IF NOT EXISTS IX_ApiSourceNameIndex_Lookup
                                  ON ApiSourceNameIndex (SourceRoot, SearchPattern, ApiName);

                              CREATE TABLE IF NOT EXISTS ApiExecutionSourceCache (
                                  SourceRoot TEXT NOT NULL COLLATE NOCASE,
                                  SearchPattern TEXT NOT NULL,
                                  FilePath TEXT NOT NULL COLLATE NOCASE,
                                  ApiCode TEXT NOT NULL,
                                  SourceOrder INTEGER NOT NULL,
                                  SourceCode TEXT NOT NULL,
                                  PRIMARY KEY (SourceRoot, SearchPattern, FilePath, ApiCode, SourceOrder)
                              );

                              CREATE INDEX IF NOT EXISTS IX_ApiExecutionSourceCache_Lookup
                                  ON ApiExecutionSourceCache (SourceRoot, SearchPattern, ApiCode);

                              CREATE TABLE IF NOT EXISTS ApiExecutionSourceNameCache (
                                  SourceRoot TEXT NOT NULL COLLATE NOCASE,
                                  SearchPattern TEXT NOT NULL,
                                  FilePath TEXT NOT NULL COLLATE NOCASE,
                                  ApiName TEXT NOT NULL,
                                  SourceOrder INTEGER NOT NULL,
                                  SourceCode TEXT NOT NULL,
                                  PRIMARY KEY (SourceRoot, SearchPattern, FilePath, ApiName, SourceOrder)
                              );

                              CREATE INDEX IF NOT EXISTS IX_ApiExecutionSourceNameCache_Lookup
                                  ON ApiExecutionSourceNameCache (SourceRoot, SearchPattern, ApiName);
                              """;
        command.ExecuteNonQuery();
    }

    private static Dictionary<string, CachedFileRecord> ReadExistingFiles(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sourceRoot,
        string searchPattern)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              SELECT FilePath, FileLength, LastWriteUtcTicks, ContentHash, ParserKey
                              FROM SourceFileAnalysisCache
                              WHERE SourceRoot = $sourceRoot AND SearchPattern = $searchPattern;
                              """;
        command.Parameters.AddWithValue("$sourceRoot", sourceRoot);
        command.Parameters.AddWithValue("$searchPattern", searchPattern);

        var files = new Dictionary<string, CachedFileRecord>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            files[reader.GetString(0)] = new CachedFileRecord
            {
                FileLength = reader.GetInt64(1),
                LastWriteUtcTicks = reader.GetInt64(2),
                ContentHash = reader.GetString(3),
                ParserKey = reader.GetString(4)
            };
        }

        return files;
    }

    private static void ReplaceFileAnalysis(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sourceRoot,
        string searchPattern,
        string filePath,
        long fileLength,
        long lastWriteUtcTicks,
        string contentHash,
        string parserKey,
        SourceFileAnalysis analysis)
    {
        DeleteFileDetails(connection, transaction, sourceRoot, searchPattern, filePath);

        var payload = new CachedSourceFilePayload
        {
            ApiSources = analysis.ApiSources.Select(CachedApiSourceInfo.FromApiSourceInfo).ToList()
        };

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                                  INSERT INTO SourceFileAnalysisCache (
                                      SourceRoot, SearchPattern, FilePath, FileLength, LastWriteUtcTicks,
                                      ContentHash, ParserKey, PayloadJson)
                                  VALUES (
                                      $sourceRoot, $searchPattern, $filePath, $fileLength, $lastWriteUtcTicks,
                                      $contentHash, $parserKey, $payloadJson)
                                  ON CONFLICT (SourceRoot, SearchPattern, FilePath) DO UPDATE SET
                                      FileLength = excluded.FileLength,
                                      LastWriteUtcTicks = excluded.LastWriteUtcTicks,
                                      ContentHash = excluded.ContentHash,
                                      ParserKey = excluded.ParserKey,
                                      PayloadJson = excluded.PayloadJson;
                                  """;
            AddFileParameters(command, sourceRoot, searchPattern, filePath);
            command.Parameters.AddWithValue("$fileLength", fileLength);
            command.Parameters.AddWithValue("$lastWriteUtcTicks", lastWriteUtcTicks);
            command.Parameters.AddWithValue("$contentHash", contentHash);
            command.Parameters.AddWithValue("$parserKey", parserKey);
            command.Parameters.AddWithValue("$payloadJson", JsonSerializer.Serialize(payload, JsonOptions));
            command.ExecuteNonQuery();
        }

        foreach (var apiCode in analysis.ApiSources
                     .SelectMany(source => source.ApiCodes)
                     .Where(code => !string.IsNullOrWhiteSpace(code))
                     .Distinct(StringComparer.Ordinal))
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                                  INSERT INTO ApiSourceCodeIndex (SourceRoot, SearchPattern, FilePath, ApiCode)
                                  VALUES ($sourceRoot, $searchPattern, $filePath, $apiCode);
                                  """;
            AddFileParameters(command, sourceRoot, searchPattern, filePath);
            command.Parameters.AddWithValue("$apiCode", apiCode);
            command.ExecuteNonQuery();
        }

        foreach (var apiName in analysis.ApiSources
                     .SelectMany(source => source.ApiNames)
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Distinct(StringComparer.Ordinal))
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                                  INSERT INTO ApiSourceNameIndex (SourceRoot, SearchPattern, FilePath, ApiName)
                                  VALUES ($sourceRoot, $searchPattern, $filePath, $apiName);
                                  """;
            AddFileParameters(command, sourceRoot, searchPattern, filePath);
            command.Parameters.AddWithValue("$apiName", apiName);
            command.ExecuteNonQuery();
        }

        foreach (var (apiCode, sources) in analysis.ExecutionSources)
        {
            for (var index = 0; index < sources.Count; index++)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                                      INSERT INTO ApiExecutionSourceCache (
                                          SourceRoot, SearchPattern, FilePath, ApiCode, SourceOrder, SourceCode)
                                      VALUES (
                                          $sourceRoot, $searchPattern, $filePath, $apiCode, $sourceOrder, $sourceCode);
                                      """;
                AddFileParameters(command, sourceRoot, searchPattern, filePath);
                command.Parameters.AddWithValue("$apiCode", apiCode);
                command.Parameters.AddWithValue("$sourceOrder", index);
                command.Parameters.AddWithValue("$sourceCode", sources[index]);
                command.ExecuteNonQuery();
            }
        }

        foreach (var (apiName, sources) in analysis.ExecutionSourcesByName)
        {
            for (var index = 0; index < sources.Count; index++)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                                      INSERT INTO ApiExecutionSourceNameCache (
                                          SourceRoot, SearchPattern, FilePath, ApiName, SourceOrder, SourceCode)
                                      VALUES (
                                          $sourceRoot, $searchPattern, $filePath, $apiName, $sourceOrder, $sourceCode);
                                      """;
                AddFileParameters(command, sourceRoot, searchPattern, filePath);
                command.Parameters.AddWithValue("$apiName", apiName);
                command.Parameters.AddWithValue("$sourceOrder", index);
                command.Parameters.AddWithValue("$sourceCode", sources[index]);
                command.ExecuteNonQuery();
            }
        }
    }

    private static void UpdateFileMetadata(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sourceRoot,
        string searchPattern,
        string filePath,
        long fileLength,
        long lastWriteUtcTicks)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              UPDATE SourceFileAnalysisCache
                              SET FileLength = $fileLength, LastWriteUtcTicks = $lastWriteUtcTicks
                              WHERE SourceRoot = $sourceRoot
                                  AND SearchPattern = $searchPattern
                                  AND FilePath = $filePath;
                              """;
        AddFileParameters(command, sourceRoot, searchPattern, filePath);
        command.Parameters.AddWithValue("$fileLength", fileLength);
        command.Parameters.AddWithValue("$lastWriteUtcTicks", lastWriteUtcTicks);
        command.ExecuteNonQuery();
    }

    private static void DeleteFile(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sourceRoot,
        string searchPattern,
        string filePath)
    {
        DeleteFileDetails(connection, transaction, sourceRoot, searchPattern, filePath);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              DELETE FROM SourceFileAnalysisCache
                              WHERE SourceRoot = $sourceRoot
                                  AND SearchPattern = $searchPattern
                                  AND FilePath = $filePath;
                              """;
        AddFileParameters(command, sourceRoot, searchPattern, filePath);
        command.ExecuteNonQuery();
    }

    private static void DeleteFileDetails(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sourceRoot,
        string searchPattern,
        string filePath)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              DELETE FROM ApiSourceCodeIndex
                              WHERE SourceRoot = $sourceRoot
                                  AND SearchPattern = $searchPattern
                                  AND FilePath = $filePath;

                              DELETE FROM ApiExecutionSourceCache
                              WHERE SourceRoot = $sourceRoot
                                  AND SearchPattern = $searchPattern
                                  AND FilePath = $filePath;

                              DELETE FROM ApiSourceNameIndex
                              WHERE SourceRoot = $sourceRoot
                                  AND SearchPattern = $searchPattern
                                  AND FilePath = $filePath;

                              DELETE FROM ApiExecutionSourceNameCache
                              WHERE SourceRoot = $sourceRoot
                                  AND SearchPattern = $searchPattern
                                  AND FilePath = $filePath;
                              """;
        AddFileParameters(command, sourceRoot, searchPattern, filePath);
        command.ExecuteNonQuery();
    }

    private static List<ApiSourceInfo> ReadApiSources(
        SqliteCommand command,
        string? identifier,
        bool byName)
    {
        var apiSources = new List<ApiSourceInfo>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var payload = JsonSerializer.Deserialize<CachedSourceFilePayload>(reader.GetString(0), JsonOptions)
                       ?? throw new InvalidDataException("SQLite 源代码缓存内容无效。");
            apiSources.AddRange(payload.ApiSources
                .Select(source => source.ToApiSourceInfo())
                .Where(source => identifier is null ||
                                 (byName
                                      ? source.ApiNames.Contains(identifier, StringComparer.Ordinal)
                                      : source.ApiCodes.Contains(identifier, StringComparer.Ordinal))));
        }

        return apiSources;
    }

    private static string ComputeContentHash(string sourceText)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceText)));
    }

    private static string NormalizeSourceRoot(string sourcePath)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourcePath));
    }

    private static void AddScopeParameters(
        SqliteCommand command,
        string sourcePath,
        string searchPattern)
    {
        command.Parameters.AddWithValue("$sourceRoot", NormalizeSourceRoot(sourcePath));
        command.Parameters.AddWithValue("$searchPattern", searchPattern);
    }

    private static void AddFileParameters(
        SqliteCommand command,
        string sourceRoot,
        string searchPattern,
        string filePath)
    {
        command.Parameters.AddWithValue("$sourceRoot", sourceRoot);
        command.Parameters.AddWithValue("$searchPattern", searchPattern);
        command.Parameters.AddWithValue("$filePath", filePath);
    }

    #region Nested type: CachedApiSourceInfo
    private sealed class CachedApiSourceInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public List<string> ApiCodes { get; set; } = [];
        public List<string> ApiNames { get; set; } = [];
        public List<string> Descriptions { get; set; } = [];
        public string ClassName { get; set; } = string.Empty;
        public string FullSourceCode { get; set; } = string.Empty;
        public string SourceCodeWithoutApiAttributes { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string LinePath { get; set; } = string.Empty;
        public int StartLine { get; set; }
        public int StartCharacter { get; set; }
        public int EndLine { get; set; }
        public int EndCharacter { get; set; }

        public static CachedApiSourceInfo FromApiSourceInfo(ApiSourceInfo source)
        {
            return new CachedApiSourceInfo
            {
                FilePath = source.FilePath,
                ApiCodes = source.ApiCodes,
                ApiNames = source.ApiNames,
                Descriptions = source.Descriptions,
                ClassName = source.ClassName,
                FullSourceCode = source.FullSourceCode,
                SourceCodeWithoutApiAttributes = source.SourceCodeWithoutApiAttributes,
                Namespace = source.Namespace,
                LinePath = source.LineSpan.Path,
                StartLine = source.LineSpan.StartLinePosition.Line,
                StartCharacter = source.LineSpan.StartLinePosition.Character,
                EndLine = source.LineSpan.EndLinePosition.Line,
                EndCharacter = source.LineSpan.EndLinePosition.Character
            };
        }

        public ApiSourceInfo ToApiSourceInfo()
        {
            return new ApiSourceInfo
            {
                FilePath = FilePath,
                ApiCodes = ApiCodes,
                ApiNames = ApiNames,
                Descriptions = Descriptions,
                ClassName = ClassName,
                FullSourceCode = FullSourceCode,
                SourceCodeWithoutApiAttributes = SourceCodeWithoutApiAttributes,
                Namespace = Namespace,
                LineSpan = new FileLinePositionSpan(
                    LinePath,
                    new LinePositionSpan(
                        new LinePosition(StartLine, StartCharacter),
                        new LinePosition(EndLine, EndCharacter)))
            };
        }
    }
    #endregion

    #region Nested type: CachedFileRecord
    private sealed class CachedFileRecord
    {
        public long FileLength { get; init; }
        public long LastWriteUtcTicks { get; init; }
        public string ContentHash { get; init; } = string.Empty;
        public string ParserKey { get; init; } = string.Empty;
    }
    #endregion

    #region Nested type: CachedSourceFilePayload
    private sealed class CachedSourceFilePayload
    {
        public List<CachedApiSourceInfo> ApiSources { get; set; } = [];
    }
    #endregion
}
