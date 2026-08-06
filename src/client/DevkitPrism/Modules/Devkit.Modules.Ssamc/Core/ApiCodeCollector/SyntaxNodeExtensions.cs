using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Devkit.Modules.Ssamc.Core.ApiCodeCollector;

public static class SyntaxNodeExtensions
{
    /// <summary>
    /// 从类声明中移除指定的特性
    /// </summary>
    public static ClassDeclarationSyntax RemoveAttribute(this ClassDeclarationSyntax classDecl, string attributeName)
    {
        var newAttributeLists = new List<AttributeListSyntax>();

        foreach (var attributeList in classDecl.AttributeLists)
        {
            // 过滤掉指定名称的特性
            var newAttributes = attributeList.Attributes
                .Where(attr =>
                {
                    var name = attr.Name.ToString();
                    return name != attributeName && name != $"{attributeName}Attribute";
                })
                .ToList();

            if (newAttributes.Count > 0)
            {
                // 创建新的特性列表
                var newAttributeList = SyntaxFactory.AttributeList(
                    SyntaxFactory.SeparatedList(newAttributes))
                    .WithTriviaFrom(attributeList);

                newAttributeLists.Add(newAttributeList);
            }
            // 如果移除了所有特性，但原始特性列表有格式信息，可以添加注释
            else if (attributeList.Attributes.Count > 0)
            {
                // 可以在这里添加一个空的特性列表以保持格式，或者什么都不做
                // 这里选择添加一个空的特性列表
                var emptyAttributeList = SyntaxFactory.AttributeList()
                    .WithOpenBracketToken(attributeList.OpenBracketToken)
                    .WithCloseBracketToken(attributeList.CloseBracketToken);

                newAttributeLists.Add(emptyAttributeList);
            }
            else
            {
                // 保留空的特性列表
                newAttributeLists.Add(attributeList);
            }
        }

        return classDecl.WithAttributeLists(SyntaxFactory.List(newAttributeLists));
    }

    /// <summary>
    /// 简化版：直接移除所有特性（如果只有Api特性）
    /// </summary>
    public static string RemoveApiAttributesSimple(this ClassDeclarationSyntax classDecl)
    {
        var source = classDecl.ToFullString();

        // 移除 [Api("...")] 或 [ApiAttribute("...")]
        var lines = source.Split('\n');
        var resultLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // 跳过包含Api特性的行
            if (trimmedLine.StartsWith("[Api") && trimmedLine.Contains("Attribute"))
            {
                continue;
            }

            resultLines.Add(line);
        }

        return string.Join("\n", resultLines);
    }
}
