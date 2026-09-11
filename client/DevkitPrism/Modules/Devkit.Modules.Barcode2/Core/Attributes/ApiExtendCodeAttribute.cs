namespace Barcode2.Core.Attributes;

/// <summary>
/// BARCODE ApiExtendCode
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class ApiExtendCodeAttribute : Attribute
{
    public ApiExtendCodeAttribute()
    {
    }

    public ApiExtendCodeAttribute(string code)
    {
        Code = code;
    }

    public string Code { get; } = string.Empty;

    public string? Name { get; set; }

    /// <summary>
    /// 可选的描述信息
    /// </summary>
    public string? Description { get; set; }
}
