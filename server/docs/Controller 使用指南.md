# Controller 使用指南

## 适用场景

Controller 适合 Action 数量较多、依赖 MVC Filter、复杂模型绑定、统一 Action 约定或需要复用 MVC 生态能力的接口。本项目的 `SystemController` 是一个保持现有 `/api/v1/system/info` 契约不变的简洁模板。

## 创建 Controller

在 `src/Devkit.Server.Api/Controllers` 中创建公开、非抽象的 Controller：

```csharp
using Devkit.Server.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Devkit.Server.Api.Controllers;

[ApiController]
[Route("api/v1/examples")]
public sealed class ExamplesController(IExampleService service) : ControllerBase
{
    [HttpGet("{id:guid}", Name = "GetExample")]
    [EndpointSummary("Returns an example")]
    [ProducesResponseType(typeof(ApiResponse<ExampleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ExampleResponse>>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var value = await service.FindAsync(id, cancellationToken);
        if (value is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Example was not found.",
                Extensions =
                {
                    ["code"] = "example_not_found",
                    ["traceId"] = HttpContext.TraceIdentifier
                }
            });
        }

        return Ok(new ApiResponse<ExampleResponse>(value, HttpContext.TraceIdentifier));
    }
}
```

示例中的 `IExampleService` 和 `ExampleResponse` 是占位名称，需要替换为实际的 Application 服务和契约类型。Controller 可以通过主构造函数注入服务；默认情况下每次请求创建一个 Controller 实例，因此可以安全使用 Scoped 服务。

## 自动注册原理

`Program.cs` 已启用 MVC Controller 服务：

```csharp
builder.Services.AddControllers();
```

并统一映射所有通过 MVC 发现的 Attribute Routing Action：

```csharp
app.MapControllers();
```

MVC 会从 API 程序集中自动发现 Controller。新增 Controller 后无需修改 `Program.cs`，但必须满足以下条件：

- 类是公开、非抽象、非泛型类。
- 类名以 `Controller` 结尾，或者显式标记 `[Controller]`。
- 类未标记 `[NonController]`。
- Action 是公开实例方法，并使用 `[HttpGet]`、`[HttpPost]`、`[HttpPut]`、`[HttpDelete]` 等路由特性。
- Controller 或 Action 上存在版本化路由，例如 `api/v1/...`。

## 当前模板

`SystemController` 展示了最小可用结构：

```csharp
[ApiController]
[Route("api/v1/system")]
public sealed class SystemController(ISystemInfoService systemInfoService) : ControllerBase
{
    [HttpGet("info", Name = "GetSystemInfo")]
    [EndpointSummary("Returns server identity and runtime status")]
    [ProducesResponseType(typeof(ApiResponse<SystemInfoResponse>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<SystemInfoResponse>> GetInfo() =>
        Ok(new ApiResponse<SystemInfoResponse>(
            systemInfoService.GetInfo(),
            HttpContext.TraceIdentifier));
}
```

复制该结构并替换路由、服务和契约即可。由于 Controller 使用框架原生发现机制，不需要实现 `IEndpointModule`。

## ProblemDetails 与 OpenAPI

- 在每个 Action 上声明成功和错误响应的 `ProducesResponseType`。
- 错误响应返回 `ProblemDetails`，并提供稳定的业务错误 `code` 和请求 `traceId`。
- 使用 `[HttpXxx(Name = "...")]` 设置唯一的 endpoint 名称。
- 使用 `[EndpointSummary]` 提供 OpenAPI 摘要。
- `[ApiController]` 会自动处理常见的模型验证错误并生成 400 响应。

## 与 Minimal API 共存

Controller 由 `MapControllers` 映射，Minimal API 模块由 `MapEndpointModules` 映射。二者共享认证、授权、限流、异常处理和 OpenAPI 配置；不要同时声明相同的 HTTP 方法与路径，否则请求匹配时会产生歧义。
