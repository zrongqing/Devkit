namespace Ssamc.Core.Attributes;

/// <summary>
/// 标记方法
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ApiSourceCodeAttribute : Attribute
{
    public string Code { get; }

    public ApiSourceCodeAttribute(string code)
    {
        Code = code;
    }
}
