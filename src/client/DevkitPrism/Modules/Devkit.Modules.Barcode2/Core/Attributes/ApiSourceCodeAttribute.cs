namespace Barcode2.Core.Attributes;

/// <summary>
/// 标记方法
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class ApiSourceCodeAttribute : Attribute
{
    public ApiSourceCodeAttribute()
    {
    }

    public ApiSourceCodeAttribute(string code)
    {
        Code = code;
    }

    public string Code { get; } = string.Empty;

    public string? Name { get; set; }
}
