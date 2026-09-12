# IConfiguration 配置与识别原则

本文说明 Devkit Server 如何加载、合并和读取配置，并给出排查“配置文件看起来正确，但程序使用了其他值”的统一方法。

## 1. 配置的核心原则

ASP.NET Core 的配置不是只读取一个 JSON 文件，而是把多个配置源按顺序合并成一个 `IConfiguration`。同一个键在多个配置源中出现时，后加载的值覆盖先加载的值。

因此，判断程序最终使用什么配置，不能只看 `appsettings.json`，必须检查实际启动方式和所有更高优先级的配置源。

本项目在 [Program.cs](../src/Devkit.Server.Api/Program.cs) 中使用：

```csharp
var builder = WebApplication.CreateBuilder(args);
```

这会启用 ASP.NET Core 默认配置。对应用配置而言，通常按以下顺序从低到高合并：

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Development 环境下的 User Secrets
4. 环境变量
5. 命令行参数

后面的配置源优先级更高。例如，`appsettings.json` 中的 `Database=Devkit` 会被环境变量中的同名配置覆盖。

`ASPNETCORE_ENVIRONMENT` 主要决定当前环境名称，从而决定是否加载 `appsettings.Development.json`、`appsettings.Production.json` 等环境文件。Visual Studio 或 `dotnet run` 的 `launchSettings.json` 会为启动进程提供环境变量，但它不是一个会自动覆盖所有配置的特殊 JSON 配置源。

## 2. 配置键的识别规则

### 2.1 JSON 使用冒号表示层级

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=Devkit;..."
  },
  "Jwt": {
    "SigningKey": "..."
  }
}
```

上述配置对应的键分别是：

```text
ConnectionStrings:Default
Jwt:SigningKey
```

配置键通常不区分大小写，但应保持项目既有大小写，避免不同操作系统或外部工具产生歧义。

### 2.2 环境变量使用双下划线表示层级

跨平台设置环境变量时，应使用双下划线 `__` 替代冒号 `:`：

```text
ConnectionStrings__Default=Server=localhost;Database=Devkit;...
Jwt__SigningKey=...
Redis__ConnectionString=localhost:6379,abortConnect=false
```

ASP.NET Core 环境变量配置提供程序会把 `__` 归一化为 `:`，所以 `ConnectionStrings__Default` 对应配置键 `ConnectionStrings:Default`。

在 Windows 中，当前项目也可能看到带冒号的环境变量名，例如：

```text
ConnectionStrings:Default=Server=.;Database=demo1;...
```

它会作为同名配置键参与合并。为了跨平台和与 Docker、脚本保持一致，新增配置时统一使用双下划线形式，不要依赖 Windows 专有的冒号环境变量名。

### 2.3 命令行参数也按层级键识别

命令行可以使用冒号或双下划线表达嵌套键，例如：

```text
dotnet run --ConnectionStrings:Default="Server=localhost;Database=Devkit;..."
```

命令行参数优先级高于环境变量，适合一次性覆盖配置，不适合放置密码或长期连接串。

### 2.4 数组使用数字下标

数组项使用数字下标表示：

```text
AllowedHosts__0=localhost
AllowedHosts__1=example.com
```

配置键的基本识别形式可以概括为：

```text
JSON 层级             -> A:B
环境变量层级          -> A__B
数组                  -> A__0
更深层级              -> A__B__C
```

## 3. 本项目如何读取配置

### 3.1 SQL Server 运行时连接串

服务端在 [DependencyInjection.cs](../src/Devkit.Server.Infrastructure/DependencyInjection.cs) 中读取：

```csharp
var connectionString = configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required");
```

`GetConnectionString("Default")` 是下面调用的简写：

```csharp
configuration.GetSection("ConnectionStrings").GetSection("Default").Value
```

因此，以下配置都会指向同一个运行时键：

```text
ConnectionStrings:Default
ConnectionStrings__Default
```

当前默认值位于 [appsettings.json](../src/Devkit.Server.Api/appsettings.json)：

```text
Server=localhost;Database=Devkit;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True
```

运行时数据库由 `GetConnectionString("Default")` 返回的最终值决定，而不是由 EF Core 迁移文件中的数据库名决定。

### 3.2 强类型选项

JWT、Redis、注册和后台任务等配置通过 `IOptions<T>` 绑定。例如：

```csharp
services.AddOptions<JwtOptions>()
    .Bind(configuration.GetSection(JwtOptions.SectionName));
```

这意味着：

```text
Jwt:Issuer                 -> JwtOptions.Issuer
Jwt:SigningKey             -> JwtOptions.SigningKey
Redis:ConnectionString     -> RedisOptions.ConnectionString
```

部分选项使用 `ValidateOnStart()`，配置缺失或不合法时会在启动阶段失败。连接串当前只检查是否为 `null`；如果外部变量为空字符串，仍可能在创建 SQL Server provider 时才暴露错误。

### 3.3 EF Core 设计时配置是另一条路径

[DevkitDbContextFactory.cs](../src/Devkit.Server.Infrastructure/Persistence/DevkitDbContextFactory.cs) 不使用完整的 `IConfiguration`，而是直接读取：

```csharp
Environment.GetEnvironmentVariable("ConnectionStrings__Default")
```

如果没有这个环境变量，它回退到 `Database=Devkit` 的默认连接串。

所以运行 API 和执行 `dotnet ef` 时要注意：

| 场景 | 读取方式 | 影响 |
| --- | --- | --- |
| API 启动 | 完整 `IConfiguration`，读取 `ConnectionStrings:Default` | 会受到 JSON、User Secrets、环境变量和命令行覆盖 |
| `dotnet ef` | 直接读取 `ConnectionStrings__Default` | 不读取 `appsettings.json`、User Secrets，也不会读取仅存在的其他配置键 |

例如，只设置 Windows 环境变量 `ConnectionStrings:Default=...demo1...` 时，API 运行时可能连接 `demo1`，但 `DevkitDbContextFactory` 仍可能回退到 `Devkit`。迁移前应明确设置 `ConnectionStrings__Default`，不要仅依赖运行时配置。

## 4. 各种启动方式的配置入口

### Visual Studio 或本地项目启动

通常会加载：

- `appsettings.json`
- 与 `ASPNETCORE_ENVIRONMENT` 匹配的环境文件
- Development 环境的 User Secrets
- 启动进程继承的用户级、机器级环境变量
- `launchSettings.json` 中声明的环境变量
- 启动参数

`launchSettings.json` 当前只设置了 `ASPNETCORE_ENVIRONMENT=Development`，没有设置数据库连接串。因此本地启动时尤其要检查当前进程、用户级和机器级环境变量。

### Docker Compose 启动

[compose.yaml](../../compose.yaml) 显式注入：

```yaml
ConnectionStrings__Default: ${DEVKIT_SQL_CONNECTION_STRING}
```

`${DEVKIT_SQL_CONNECTION_STRING}` 通常来自仓库根目录的 `.env` 或启动 Compose 的 shell 环境。该文件不应提交到仓库；模板见 [.env.example](../../.env.example)。

Docker 启动时应检查：

1. `.env` 中的 `DEVKIT_SQL_CONNECTION_STRING`。
2. 当前 shell 是否已经设置了同名变量。
3. 容器实际注入的 `ConnectionStrings__Default`。

Docker Compose 的变量和本地 Visual Studio 进程的环境变量是两套入口，排查时不要混用。

## 5. “程序到底使用了什么配置”的识别流程

按以下顺序检查，先确认读取键，再检查覆盖来源：

### 第一步：确认代码读取的键

搜索以下内容：

```text
GetConnectionString(
GetSection(
Bind(
Environment.GetEnvironmentVariable(
```

本项目 SQL Server 运行时键是 `ConnectionStrings:Default`，EF Core 设计时键是 `ConnectionStrings__Default`。

### 第二步：检查 JSON 和环境文件

检查：

```text
server/src/Devkit.Server.Api/appsettings.json
server/src/Devkit.Server.Api/appsettings.{Environment}.json
```

先根据 `ASPNETCORE_ENVIRONMENT` 确认实际环境名，再判断对应环境文件是否存在以及是否覆盖了目标键。

### 第三步：检查 User Secrets

项目文件中的 `UserSecretsId` 是 `Devkit.Server.Api`。Development 环境会加载该项目对应的 User Secrets。查看配置名称时不要把密钥值、密码或完整连接串输出到日志或聊天记录：

```powershell
dotnet user-secrets list `
  --project server/src/Devkit.Server.Api/Devkit.Server.Api.csproj
```

### 第四步：检查当前进程、用户级和机器级环境变量

当前进程看到的值最接近程序实际看到的值：

```powershell
Get-ChildItem Env: |
  Where-Object Name -match 'Connection|ASPNETCORE|DOTNET'
```

Windows 的用户级和机器级值可分别检查：

```powershell
[Environment]::GetEnvironmentVariable('ConnectionStrings:Default', 'User')
[Environment]::GetEnvironmentVariable('ConnectionStrings:Default', 'Machine')
[Environment]::GetEnvironmentVariable('ConnectionStrings__Default', 'User')
[Environment]::GetEnvironmentVariable('ConnectionStrings__Default', 'Machine')
```

不要直接把输出的完整连接串贴到日志或工单中。只需确认服务器和数据库名，密码应脱敏。

修改用户级或机器级环境变量后，必须重启 Visual Studio、终端、IDE 调试会话或 Docker 容器；已经启动的进程不会自动刷新其环境变量副本。

### 第五步：检查命令行参数和容器实际环境

如果前面的值都正确，继续检查启动参数，以及容器实际环境：

```powershell
docker compose config
docker compose exec devkit-server printenv ConnectionStrings__Default
```

命令行和容器注入值的优先级高，可能覆盖文件中的配置。

## 6. 本次 `demo1` 问题的结论

本项目文件中的默认值是 `Database=Devkit`：

- `appsettings.json` 使用 `Database=Devkit`。
- `appsettings.Development.json` 没有覆盖 `ConnectionStrings:Default`。
- `launchSettings.json` 只设置开发环境，没有设置数据库。
- 当前 Windows 进程中存在 `ConnectionStrings:Default=...Database=demo1...`。
- 用户级和机器级环境变量中也都存在同名的 `demo1` 配置。

因此，API 启动时 `IConfiguration.GetConnectionString("Default")` 得到的是环境变量中的 `demo1` 连接串，覆盖了 `appsettings.json` 的 `Devkit` 默认值。

修正时应同时清理或修改用户级、机器级环境变量，并重启启动进程。跨平台场景建议统一使用：

```text
ConnectionStrings__Default
```

而不是 Windows 专有的：

```text
ConnectionStrings:Default
```

## 7. 配置安全与维护约定

- 不把真实密码、JWT 签名密钥和生产连接串提交到仓库。
- 开发机密使用 User Secrets；部署机密使用部署平台的 Secret 或环境变量能力。
- 新增嵌套环境变量统一使用双下划线 `__`。
- 需要迁移数据库时显式设置 `ConnectionStrings__Default`，并确认目标数据库名。
- 不在普通启动日志中打印完整连接串；如需诊断，只记录来源、键名、服务器和数据库名，密码始终脱敏。
- 修改配置后确认重启了真正运行 API 的进程，而不只是修改了配置文件。
