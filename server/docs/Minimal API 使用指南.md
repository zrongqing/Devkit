# Minimal API 使用指南

## 适用场景

Minimal API 适合路由数量较少、希望把路由、授权、限流和 OpenAPI 元数据集中写在一起的功能模块。本项目的认证接口和模块控制接口采用这种形式。

Minimal API 不包含 MVC Controller 的程序集发现机制。项目通过 `IEndpointModule` 和 `EndpointModuleExtensions` 提供等价的自动发现：新增模块只需实现接口，不需要修改 `Program.cs`。

## 创建 endpoint 模块

在 `src/Devkit.Server.Api/Endpoints` 中创建一个公开、非抽象、非泛型类，并实现 `IEndpointModule`：

```csharp
using Devkit.Server.Api.Contracts;

namespace Devkit.Server.Api.Endpoints;

public sealed class ExampleEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/v1/examples")
            .WithTags("Examples");

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetExample")
            .WithSummary("Returns an example")
            .Produces<ApiResponse<ExampleResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        HttpContext context,
        IExampleService service,
        CancellationToken cancellationToken)
    {
        var value = await service.FindAsync(id, cancellationToken);
        return value is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Example was not found.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "example_not_found",
                    ["traceId"] = context.TraceIdentifier
                })
            : Results.Ok(new ApiResponse<ExampleResponse>(value, context.TraceIdentifier));
    }
}
```

示例中的 `IExampleService` 和 `ExampleResponse` 是占位名称，需要替换为实际的 Application 服务和契约类型。

## 自动注册原理

`Program.cs` 已配置以下两步：

```csharp
builder.Services.AddEndpointModules(typeof(Program).Assembly);
```

启动时扫描指定程序集，将所有实现 `IEndpointModule` 的具体类注册到依赖注入容器。

```csharp
app.MapEndpointModules();
```

应用构建后从容器取得所有模块，并逐一执行 `MapEndpoints`。因此新增 endpoint 模块后不需要再添加 `app.MapXxxEndpoints()`。

模块以单例注册，只应负责声明路由。请求级服务应像示例一样通过 handler 参数注入，不要把 Scoped 服务注入模块构造函数。

如果模块位于其他程序集，需要把该程序集也传给 `AddEndpointModules`：

```csharp
builder.Services.AddEndpointModules(
    typeof(Program).Assembly,
    typeof(ExternalEndpointMarker).Assembly);
```

## 项目约定

- 路径必须使用版本前缀，例如 `/api/v1/examples`。
- 使用 `WithName`、`WithSummary`、`Produces` 和 `ProducesProblem` 完整描述 OpenAPI 契约。
- 错误响应使用 ProblemDetails，并至少提供稳定的 `code` 和请求 `traceId`。
- handler 通过 Application 抽象访问业务逻辑，不直接访问数据库。
- HTTP 契约发生变化时，同步更新 Web 与 Desktop 调用端。

## 何时改用 Controller

当一个资源包含较多 Action、需要 MVC Filter、复杂模型绑定或希望使用 MVC 的统一约定时，优先使用 Controller。两种风格可以共存，但同一路径只能由其中一种方式映射。
