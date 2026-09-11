namespace Barcode2.Servers;

public enum ApiLookupKind
{
    Code,
    Name
}

/// <summary>
/// 单条接口代码更新请求。
/// <paramref name="ExtendCode"/> 对应 SYS_PAGE_EVENT_CODE.STR_EXTEND（扩展源代码）；
/// <paramref name="ExecutionSource"/> 对应 SYS_PAGE_EVENT_CODE.STR_SOURCE（执行源代码）。
/// 某一项为空/空白表示扫描源代码时未找到，更新时数据库对应列保留旧值不动。
/// </summary>
public sealed record ApiExtendUpdateRequest(
    ApiLookupKind LookupKind,
    string Identifier,
    string ExtendCode,
    string? ExecutionSource = null);
