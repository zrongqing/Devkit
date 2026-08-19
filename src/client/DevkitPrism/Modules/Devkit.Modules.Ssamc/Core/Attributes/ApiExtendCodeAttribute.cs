namespace Ssamc.Core.Attributes;

/// <summary>
/// BARCODE ApiExtendCode
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class ApiExtendCodeAttribute : Attribute
{
    public ApiExtendCodeAttribute(string apiCode)
    {
        ApiCode = apiCode;
    }
    public string ApiCode { get; }

    /// <summary>
    /// 可选的描述信息
    /// </summary>
    public string? Description { get; set; }
}
