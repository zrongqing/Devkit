using Ssamc.Core.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ssamc.Core.ApiCodeCollector;


/// Roslyn实现的API扫描器
/// </summary>
public class RoslynApiScanner : IApiScanner
{
    private static readonly HashSet<string> ExecutionSourceAttributes =
    [
        nameof(ApiSourceCodeAttribute),
        "ApiSourceCode"
    ];

    private readonly SqliteApiSourceCache _cache;
    private readonly object _cacheSync = new();

    public RoslynApiScanner()
        : this(new SqliteApiSourceCache(SqliteApiSourceCache.GetDefaultDatabasePath()))
    {
    }

    private RoslynApiScanner(SqliteApiSourceCache cache)
    {
        _cache = cache;
        Attributes =
        [
            nameof(ApiExtendCodeAttribute),
            "ApiExtendCode"
        ];
    }

    /// <summary>
    /// 创建使用指定 SQLite 数据库的扫描器。主要用于隔离测试或宿主自定义缓存位置。
    /// </summary>
    public static RoslynApiScanner CreateWithCacheDatabase(string cacheDatabasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDatabasePath);
        return new RoslynApiScanner(new SqliteApiSourceCache(cacheDatabasePath));
    }

    public List<string> Attributes { get; set; }

    /// <inheritdoc />
    public string GetExecutionSourceCode(string sourcePath, string apiCode)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) ||
            string.IsNullOrWhiteSpace(apiCode) ||
            !Directory.Exists(sourcePath))
        {
            return string.Empty;
        }

        lock (_cacheSync)
        {
            var files = GetSourceFiles(sourcePath, "*.cs");
            if (TrySynchronizeCache(sourcePath, "*.cs", files) &&
                _cache.TryGetExecutionSources(sourcePath, "*.cs", apiCode, out var cachedSources))
            {
                return JoinExecutionSources(cachedSources);
            }

            var analyses = AnalyzeFilesWithoutCache(files);
            var sources = analyses
                .SelectMany(analysis => analysis.ExecutionSources.TryGetValue(apiCode, out var matches)
                    ? matches
                    : [])
                .ToList();
            return JoinExecutionSources(sources);
        }
    }

    private static string JoinExecutionSources(IEnumerable<string> sources)
    {
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            sources.Where(source => !string.IsNullOrWhiteSpace(source)));
    }

    private static string GetMethodBodySource(MethodDeclarationSyntax method)
    {
        if (method.Body is not null)
        {
            return string.Concat(method.Body.Statements.Select(statement => statement.ToFullString())).Trim();
        }

        if (method.ExpressionBody is not null)
        {
            var isVoid = method.ReturnType is PredefinedTypeSyntax predefinedType &&
                         predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword);
            var returnPrefix = isVoid ? string.Empty : "return ";
            return $"{returnPrefix}{method.ExpressionBody.Expression};";
        }

        return string.Empty;
    }

    /// <summary>
    /// 扫描指定路径下的所有C#源代码文件
    /// </summary>
    public List<ApiSourceInfo> ScanSourceFiles(string sourcePath, string searchPattern = "*.cs")
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !Directory.Exists(sourcePath))
        {
            return [];
        }

        lock (_cacheSync)
        {
            var files = GetSourceFiles(sourcePath, searchPattern);
            if (TrySynchronizeCache(sourcePath, searchPattern, files) &&
                _cache.TryGetAllApiSources(sourcePath, searchPattern, out var cachedSources))
            {
                return cachedSources;
            }

            return AnalyzeFilesWithoutCache(files)
                .SelectMany(analysis => analysis.ApiSources)
                .ToList();
        }
    }

    /// <inheritdoc />
    public List<ApiSourceInfo> GetApiSourceInfos(string sourcePath, string apiCode)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) ||
            string.IsNullOrWhiteSpace(apiCode) ||
            !Directory.Exists(sourcePath))
        {
            return [];
        }

        lock (_cacheSync)
        {
            const string searchPattern = "*.cs";
            var files = GetSourceFiles(sourcePath, searchPattern);
            if (TrySynchronizeCache(sourcePath, searchPattern, files) &&
                _cache.TryGetApiSources(sourcePath, searchPattern, apiCode, out var cachedSources))
            {
                return cachedSources;
            }

            return AnalyzeFilesWithoutCache(files)
                .SelectMany(analysis => analysis.ApiSources)
                .Where(info => info.ApiCodes.Contains(apiCode, StringComparer.Ordinal))
                .ToList();
        }
    }

    /// <summary>
    /// 解析单个源代码文件
    /// </summary>
    private SourceFileAnalysis ParseSourceFile(string filePath, string sourceCode)
    {
        var apiInfos = new List<ApiSourceInfo>();
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        // 查找所有类声明
        var classDeclarations = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDeclarations)
        {
            // 查找所有Api特性
            var apiAttributes = FindAllApiAttributes(classDecl.AttributeLists);
            if (apiAttributes.Count > 0)
            {
                // 获取所有ApiCode和描述
                var apiCodes = new List<string>();
                var descriptions = new List<string>();

                foreach (var apiAttr in apiAttributes)
                {
                    var apiCode = GetApiCodeValue(apiAttr);
                    var description = GetDescriptionValue(apiAttr);

                    if (!string.IsNullOrEmpty(apiCode))
                    {
                        apiCodes.Add(apiCode);
                        descriptions.Add(description);
                    }
                }

                // 获取命名空间
                var namespaceDecl = classDecl.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
                var namespaceName = namespaceDecl?.Name.ToString() ?? string.Empty;

                // 获取移除所有Api特性后的代码
                var sourceWithoutAttributes = RemoveAllApiAttributes(classDecl);

                apiInfos.Add(new ApiSourceInfo
                {
                    FilePath = filePath,
                    ApiCodes = apiCodes,
                    Descriptions = descriptions,
                    ClassName = classDecl.Identifier.Text,
                    FullSourceCode = classDecl.ToFullString(),
                    SourceCodeWithoutApiAttributes = sourceWithoutAttributes,
                    Namespace = namespaceName,
                    LineSpan = classDecl.GetLocation().GetLineSpan()
                });
            }
        }

        var executionSources = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            var methodSource = GetMethodBodySource(method);
            if (string.IsNullOrWhiteSpace(methodSource))
            {
                continue;
            }

            var apiCodes = method.AttributeLists
                .SelectMany(attributeList => attributeList.Attributes)
                .Where(attribute => ExecutionSourceAttributes.Contains(attribute.Name.ToString()))
                .Select(GetApiCodeValue)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.Ordinal);

            foreach (var apiCode in apiCodes)
            {
                if (!executionSources.TryGetValue(apiCode, out var sources))
                {
                    sources = [];
                    executionSources.Add(apiCode, sources);
                }

                sources.Add(methodSource);
            }
        }

        return new SourceFileAnalysis
        {
            ApiSources = apiInfos,
            ExecutionSources = executionSources
        };
    }

    private bool TrySynchronizeCache(
        string sourcePath,
        string searchPattern,
        IReadOnlyList<string> files)
    {
        var parserKey = $"2|{string.Join('|', Attributes.OrderBy(attribute => attribute, StringComparer.Ordinal))}";
        return _cache.TrySynchronize(
            sourcePath,
            searchPattern,
            files,
            parserKey,
            ParseSourceFile);
    }

    private List<SourceFileAnalysis> AnalyzeFilesWithoutCache(IEnumerable<string> files)
    {
        var analyses = new List<SourceFileAnalysis>();
        foreach (var file in files)
        {
            try
            {
                analyses.Add(ParseSourceFile(file, File.ReadAllText(file)));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析源代码文件 {file} 时出错: {ex.Message}");
            }
        }

        return analyses;
    }

    private static List<string> GetSourceFiles(string sourcePath, string searchPattern)
    {
        try
        {
            return Directory.GetFiles(sourcePath, searchPattern, SearchOption.AllDirectories)
                .Where(file => !IsBuildOutputFile(sourcePath, file))
                .Select(Path.GetFullPath)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"枚举源代码目录 {sourcePath} 时出错: {ex.Message}");
            return [];
        }
    }

    private static bool IsBuildOutputFile(string sourcePath, string filePath)
    {
        var relativePath = Path.GetRelativePath(sourcePath, filePath);
        return relativePath
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries)
            .Any(segment =>
                string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 查找所有的Api特性
    /// </summary>
    private List<AttributeSyntax> FindAllApiAttributes(SyntaxList<AttributeListSyntax> attributeLists)
    {
        var apiAttributes = new List<AttributeSyntax>();

        foreach (var attrList in attributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var attrName = attr.Name.ToString();
                if(this.Attributes.Contains(attrName))
                {
                    apiAttributes.Add(attr);
                }
            }
        }

        return apiAttributes;
    }

    /// <summary>
    /// 移除所有Api特性
    /// </summary>
    private string RemoveAllApiAttributes(ClassDeclarationSyntax classDecl)
    {
        return RemoveApiAttributesUsingSyntaxTree(classDecl);
    }

    /// <summary>
    /// 使用语法树重构
    /// </summary>
    private string RemoveApiAttributesUsingSyntaxTree(ClassDeclarationSyntax classDecl)
    {
        // 创建新的特性列表，排除所有Api特性
        var newAttributeLists = new List<AttributeListSyntax>();

        foreach (var attributeList in classDecl.AttributeLists)
        {
            var nonApiAttributes = attributeList.Attributes
                .Where(attr =>
                {
                    var attrName = attr.Name.ToString();
                    return !this.Attributes.Contains(attrName);
                })
                .ToList();

            if (nonApiAttributes.Count > 0)
            {
                // 创建新的特性列表，包含非Api特性
                var newAttributeList = SyntaxFactory.AttributeList(
                        SyntaxFactory.SeparatedList(nonApiAttributes))
                    .WithTriviaFrom(attributeList)
                    .WithOpenBracketToken(attributeList.OpenBracketToken)
                    .WithCloseBracketToken(attributeList.CloseBracketToken);

                newAttributeLists.Add(newAttributeList);
            }
            else if (attributeList.Attributes.Count > 0)
            {
                // 这个特性列表只有Api特性，移除整个列表
                // 但要保持格式，可能需要处理相关的空白
            }
        }

        // 构建新的类声明
        var newClassDecl = classDecl
            .WithAttributeLists(SyntaxFactory.List(newAttributeLists))
            .WithTriviaFrom(classDecl);

        // 获取源代码并清理格式
        var result = newClassDecl.ToFullString();
        return CleanupSourceCode(result);
    }

    /// <summary>
    /// 清理源代码格式
    /// </summary>
    private string CleanupSourceCode(string source)
    {
        var lines = source.Split('\n');
        var result = new StringBuilder();

        bool lastLineWasEmpty = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimEnd();

            // 跳过完全空白的行
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                if (!lastLineWasEmpty && result.Length > 0)
                {
                    result.AppendLine(); // 保留一个空行
                    lastLineWasEmpty = true;
                }
                continue;
            }

            result.AppendLine(trimmedLine);
            lastLineWasEmpty = false;
        }

        return result.ToString().TrimEnd();
    }

    /// <summary>
    /// 根据ApiCode将源代码写入到对应文件中
    /// 注意：一个类可能有多个ApiCode，需要复制到多个文件中
    /// </summary>
    public void WriteSourceCodeByApiCode(List<ApiSourceInfo> apiInfos, string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // 创建一个字典来按ApiCode组织
        var apiCodeDictionary = new Dictionary<string, List<ApiSourceInfo>>();

        foreach (var info in apiInfos)
        {
            foreach (var apiCode in info.ApiCodes)
            {
                if (!apiCodeDictionary.ContainsKey(apiCode))
                {
                    apiCodeDictionary[apiCode] = new List<ApiSourceInfo>();
                }

                // 添加到对应的ApiCode列表中
                apiCodeDictionary[apiCode].Add(info);
            }
        }

        Console.WriteLine($"找到 {apiCodeDictionary.Count} 个不同的ApiCode");

        // 为每个ApiCode创建文件
        foreach (var kvp in apiCodeDictionary)
        {
            var apiCode = kvp.Key;
            var fileName = Path.Combine(outputDirectory, $"{apiCode}.cs");

            using (var writer = new StreamWriter(fileName, false, Encoding.UTF8))
            {
                WriteSourceCodeToFile(writer, apiCode, kvp.Value);
            }

            Console.WriteLine($"已写入: {fileName} ({kvp.Value.Count} 个类)");
        }

        // 创建索引文件
        CreateIndexFile(outputDirectory, apiCodeDictionary);
    }

    /// <summary>
    /// 将源代码写入到文件
    /// </summary>
    private void WriteSourceCodeToFile(StreamWriter writer, string apiCode, List<ApiSourceInfo> apiInfos)
    {
        writer.WriteLine("// ============================================");
        writer.WriteLine($"// ApiCode: {apiCode}");
        writer.WriteLine($"// 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"// 包含 {apiInfos.Count} 个类");
        writer.WriteLine("// ============================================");
        writer.WriteLine();

        foreach (var info in apiInfos)
        {
            writer.WriteLine("// ----------------------------------------------------");
            writer.WriteLine($"// 类名: {info.ClassName}");
            writer.WriteLine($"// 文件: {Path.GetFileName(info.FilePath)}");
            writer.WriteLine($"// 命名空间: {info.Namespace}");
            writer.WriteLine($"// 其他ApiCode: {string.Join(", ", info.ApiCodes.Where(c => c != apiCode))}");

            // 显示对应ApiCode的描述
            var apiCodeIndex = info.ApiCodes.IndexOf(apiCode);
            if (apiCodeIndex >= 0 && apiCodeIndex < info.Descriptions.Count)
            {
                var description = info.Descriptions[apiCodeIndex];
                if (!string.IsNullOrEmpty(description))
                {
                    writer.WriteLine($"// 描述: {description}");
                }
            }

            writer.WriteLine("// ----------------------------------------------------");
            writer.WriteLine(info.SourceCodeWithoutApiAttributes);
            writer.WriteLine();
            writer.WriteLine();
        }
    }

    /// <summary>
    /// 创建索引文件
    /// </summary>
    private void CreateIndexFile(string outputDirectory, Dictionary<string, List<ApiSourceInfo>> apiCodeDictionary)
    {
        var indexPath = Path.Combine(outputDirectory, "_ApiIndex.md");

        using (var writer = new StreamWriter(indexPath, false, Encoding.UTF8))
        {
            writer.WriteLine("# API 源代码索引");
            writer.WriteLine();
            writer.WriteLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"总ApiCode数: {apiCodeDictionary.Count}");
            writer.WriteLine();

            writer.WriteLine("| ApiCode | 类数量 | 包含的类 | 描述 |");
            writer.WriteLine("|---------|--------|----------|------|");

            foreach (var kvp in apiCodeDictionary.OrderBy(k => k.Key))
            {
                var apiCode = kvp.Key;
                var infos = kvp.Value;

                var classNames = string.Join("、", infos.Select(i => i.ClassName).Distinct());

                // 获取第一个类的描述（如果有）
                var firstInfo = infos.First();
                var apiCodeIndex = firstInfo.ApiCodes.IndexOf(apiCode);
                var description = apiCodeIndex >= 0 && apiCodeIndex < firstInfo.Descriptions.Count
                    ? firstInfo.Descriptions[apiCodeIndex]
                    : "";

                writer.WriteLine($"| {apiCode} | {infos.Count} | {classNames} | {description} |");
            }

            writer.WriteLine();
            writer.WriteLine("## 详细列表");

            foreach (var kvp in apiCodeDictionary.OrderBy(k => k.Key))
            {
                writer.WriteLine($"### {kvp.Key}");
                writer.WriteLine();

                foreach (var info in kvp.Value)
                {
                    writer.WriteLine($"- **{info.ClassName}**");
                    writer.WriteLine($"  - 文件: {Path.GetFileName(info.FilePath)}");
                    writer.WriteLine($"  - 命名空间: {info.Namespace}");

                    // 显示所有ApiCode
                    writer.WriteLine($"  - 所有ApiCode: {string.Join(", ", info.ApiCodes)}");

                    // 显示当前ApiCode的描述
                    var apiCodeIndex = info.ApiCodes.IndexOf(kvp.Key);
                    if (apiCodeIndex >= 0 && apiCodeIndex < info.Descriptions.Count)
                    {
                        var description = info.Descriptions[apiCodeIndex];
                        if (!string.IsNullOrEmpty(description))
                        {
                            writer.WriteLine($"  - 描述: {description}");
                        }
                    }
                }
                writer.WriteLine();
            }
        }

        Console.WriteLine($"索引文件已创建: {indexPath}");
    }
  
    /// <summary>
    /// 智能移除Api特性的方法（处理复杂情况）
    /// </summary>
    private string SmartRemoveApiAttributes(ClassDeclarationSyntax classDecl)
    {
        // 获取所有Api特性节点
        var apiAttributes = classDecl.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attr =>
            {
                var attrName = attr.Name.ToString();
                return attrName == "Api" || attrName == "ApiAttribute";
            })
            .ToList();

        if (apiAttributes.Count == 0)
            return classDecl.ToFullString();

        // 获取所有特性列表节点
        var attributeLists = classDecl.DescendantNodes()
            .OfType<AttributeListSyntax>()
            .Where(list => list.Attributes.Any(attr =>
            {
                var attrName = attr.Name.ToString();
                return attrName == "Api" || attrName == "ApiAttribute";
            }))
            .ToList();

        // 构建移除节点后的新语法树
        var root = classDecl.SyntaxTree.GetRoot();
        var nodesToRemove = new List<SyntaxNode>();

        // 收集所有需要移除的节点
        nodesToRemove.AddRange(apiAttributes);

        // 对于完全由Api特性组成的特性列表，移除整个列表
        foreach (var attrList in attributeLists)
        {
            var nonApiAttributes = attrList.Attributes
                .Where(attr =>
                {
                    var attrName = attr.Name.ToString();
                    return attrName != "Api" && attrName != "ApiAttribute";
                })
                .Count();

            if (nonApiAttributes == 0)
            {
                nodesToRemove.Add(attrList);
            }
        }

        // 移除节点
        var newRoot = root.RemoveNodes(nodesToRemove, SyntaxRemoveOptions.KeepLeadingTrivia | SyntaxRemoveOptions.KeepTrailingTrivia);

        // 重新获取类声明
        var newClassDecl = newRoot?.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == classDecl.Identifier.Text);

        if (newClassDecl == null)
            return classDecl.ToFullString();

        // 清理格式
        var result = newClassDecl.ToFullString();
        result = CleanupAttributeFormat(result);

        return result;
    }

    /// <summary>
    /// 清理特性相关的格式
    /// </summary>
    private string CleanupAttributeFormat(string source)
    {
        var lines = source.Split('\n');
        var result = new StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // 跳过空特性列表 []
            if (line.Trim() == "[]")
            {
                // 检查下一行
                if (i + 1 < lines.Length && lines[i + 1].Trim().StartsWith("["))
                {
                    // 下一行也是特性，跳过空行
                    continue;
                }
            }

            // 处理特性间的空行
            if (string.IsNullOrWhiteSpace(line))
            {
                if (i > 0 && i < lines.Length - 1)
                {
                    var prevLine = lines[i - 1].Trim();
                    var nextLine = lines[i + 1].Trim();

                    // 如果前后都是特性，跳过空行
                    if (prevLine.StartsWith("[") && prevLine.EndsWith("]") &&
                        nextLine.StartsWith("[") && nextLine.EndsWith("]"))
                    {
                        continue;
                    }
                }
            }

            result.AppendLine(line);
        }

        return result.ToString().TrimEnd();
    }

    /// <summary>
    /// 获取移除Api特性后的类定义代码
    /// </summary>
    private string GetSourceCodeWithoutApiAttribute(ClassDeclarationSyntax classDecl)
    {
        // 创建新的特性列表（排除Api特性）
        var newAttributeLists = new SyntaxList<AttributeListSyntax>();

        foreach (var attributeList in classDecl.AttributeLists)
        {
            var newAttributes = new SeparatedSyntaxList<AttributeSyntax>();
            var hasApiAttribute = false;

            foreach (var attribute in attributeList.Attributes)
            {
                var attrName = attribute.Name.ToString();
                // 如果不是Api或ApiAttribute特性，则保留
                if (attrName != "Api" && attrName != "ApiAttribute")
                {
                    newAttributes = newAttributes.Add(attribute);
                }
                else
                {
                    hasApiAttribute = true;
                }
            }

            // 如果这个特性列表还有其它特性，则保留这个列表
            if (newAttributes.Count > 0)
            {
                var newAttributeList = attributeList
                    .WithAttributes(newAttributes)
                    .WithTriviaFrom(attributeList);
                newAttributeLists = newAttributeLists.Add(newAttributeList);
            }
            // 如果只有Api特性被移除了，而且原特性列表有换行等格式，可能需要保留空列表的格式
            else if (hasApiAttribute)
            {
                // 如果移除了所有特性，这里可以选择：
                // 1. 不添加任何特性列表
                // 2. 添加一个空的特性列表以保留格式（这里选择不添加）
            }
            else
            {
                // 保留原始的特性列表
                newAttributeLists = newAttributeLists.Add(attributeList);
            }
        }

        // 构建新的类声明
        var newClassDecl = classDecl
            .WithAttributeLists(newAttributeLists)
            .WithTriviaFrom(classDecl); // 保留原始格式（缩进、换行等）

        return newClassDecl.ToFullString();
    }

    /// <summary>
    /// 查找Api特性
    /// </summary>
    private AttributeSyntax FindApiAttribute(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var attrList in attributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var attrName = attr.Name.ToString();
                if (attrName == "Api" || attrName == "ApiAttribute")
                {
                    return attr;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 获取ApiCode值
    /// </summary>
    private string GetApiCodeValue(AttributeSyntax attr)
    {
        if (attr.ArgumentList?.Arguments.Count > 0)
        {
            var firstArg = attr.ArgumentList.Arguments[0];
            return firstArg.Expression.ToString().Trim('"');
        }
        return string.Empty;
    }

    /// <summary>
    /// 获取Description值
    /// </summary>
    private string GetDescriptionValue(AttributeSyntax attr)
    {
        if (attr.ArgumentList?.Arguments.Count > 1)
        {
            // 查找名为Description的参数
            var descriptionArg = attr.ArgumentList.Arguments
                .FirstOrDefault(arg => arg.NameEquals?.Name.ToString() == "Description");

            if (descriptionArg != null)
            {
                return descriptionArg.Expression.ToString().Trim('"');
            }
        }
        return string.Empty;
    }
    
    /// <summary>
    /// 清理源代码格式
    /// </summary>
    private string CleanUpSourceCode(string sourceCode)
    {
        var lines = sourceCode.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedLine))
            {
                // 移除因删除特性而产生的多余空白行
                result.AppendLine(trimmedLine);
            }
        }

        return result.ToString();
    }
 
    /// <summary>
    /// 第四种方法：最精确的方法 - 使用语法树重构
    /// </summary>
    private string GetSourceCodeWithoutApiAttributeExact(ClassDeclarationSyntax classDecl)
    {
        // 获取类的前导空白和修饰符
        var leadingTrivia = classDecl.GetLeadingTrivia();
        var trailingTrivia = classDecl.GetTrailingTrivia();
        var modifiers = classDecl.Modifiers;

        // 获取基类和接口
        var baseList = classDecl.BaseList;

        // 获取类型参数
        var typeParameterList = classDecl.TypeParameterList;

        // 获取约束子句
        var constraintClauses = classDecl.ConstraintClauses;

        // 获取成员
        var members = classDecl.Members;

        // 构建新的类声明（不带特性）
        var newClassDecl = SyntaxFactory.ClassDeclaration(classDecl.Identifier)
            .WithModifiers(modifiers)
            .WithTypeParameterList(typeParameterList)
            .WithBaseList(baseList)
            .WithConstraintClauses(constraintClauses)
            .WithMembers(members)
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(trailingTrivia);

        // 添加除了Api之外的其他特性
        foreach (var attributeList in classDecl.AttributeLists)
        {
            var nonApiAttributes = attributeList.Attributes
                .Where(attr =>
                {
                    var attrName = attr.Name.ToString();
                    return attrName != "Api" && attrName != "ApiAttribute";
                })
                .ToList();

            if (nonApiAttributes.Count > 0)
            {
                var newAttributeList = SyntaxFactory.AttributeList(
                    SyntaxFactory.SeparatedList(nonApiAttributes))
                    .WithTriviaFrom(attributeList);

                newClassDecl = newClassDecl.AddAttributeLists(newAttributeList);
            }
        }

        return newClassDecl.NormalizeWhitespace().ToFullString();
    }
}
