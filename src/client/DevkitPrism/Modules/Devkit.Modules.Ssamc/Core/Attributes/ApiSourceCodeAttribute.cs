namespace Ssamc.Core.Attributes;

/// <summary>
/// 标记方法
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ApiSourceCodeAttribute : Attribute
{
    public ApiSourceCodeAttribute(string code)
    {
        Code = code;
    }
    public string Code { get; }
}
