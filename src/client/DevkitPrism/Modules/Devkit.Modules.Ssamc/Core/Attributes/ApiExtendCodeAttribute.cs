using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ssamc.Core.Attributes;

/// <summary>
/// BARCODE ApiExtendCode 
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class ApiExtendCodeAttribute : Attribute
{
    public string ApiCode { get; }

    public ApiExtendCodeAttribute(string apiCode)
    {
        ApiCode = apiCode;
    }

    /// <summary>
    /// 可选的描述信息
    /// </summary>
    public string Description { get; set; }
}
