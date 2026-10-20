# IConfiguration 配置与识别原则

三个服务分别使用自己的 `WebApplication.CreateBuilder(args)` 和配置文件，彼此不共享配置实例。

默认应用配置优先级从低到高为：`appsettings.json`、环境 JSON、Development User Secrets、环境变量、命令行。`launchSettings.json` 提供启动环境变量，不会被 Compose 读取；仓库根目录 `.env` 用于 Compose 插值，不会被 VS Project F5 自动读取。

各服务可显式加载外部 JSON 文件，随后重新添加环境变量和命令行，最终优先级为：默认配置 → 外部文件 → 环境变量 → 命令行。原 `DEVKIT_CONFIG_FILE` 和旧的单一 UserSecretsId 不再使用。

上述加载流程由 `Devkit.AspNetCore` 的 `AddDevkitExternalConfiguration` 统一实现，各宿主显式传入自己的环境变量名。`AddDevkitApi` 在 CORS 选项构建时读取允许来源；`AddDevkitSqlServer<TContext>` 在上下文选项构建时读取显式指定的连接键。公共扩展不合并各服务的配置。

| 配置项 | Identity | FileStorage | Business |
| --- | --- | --- | --- |
| API 项目 / UserSecretsId | `Devkit.Identity.Api` | `Devkit.FileStorage.Api` | `Devkit.Business.Api` |
| 外部文件环境变量 | `DEVKIT_IDENTITY_CONFIG_FILE` | `DEVKIT_FILESTORAGE_CONFIG_FILE` | `DEVKIT_BUSINESS_CONFIG_FILE` |
| 数据库 JSON 键 | `ConnectionStrings:Identity` | `ConnectionStrings:FileStorage` | `ConnectionStrings:Business` |
| 数据库环境变量 | `ConnectionStrings__Identity` | `ConnectionStrings__FileStorage` | `ConnectionStrings__Business` |
| 密钥文件键 | `Jwt:SigningPrivateKeyPath` | `Jwt:ValidationPublicKeyPath` | `Jwt:ValidationPublicKeyPath` |

FileStorage 与 Business 分别读取 `Identity:BaseUrl`、`Identity:RequireHttps` 和 `Identity:TimeoutSeconds`；生产默认要求 HTTPS。只在明确受控的开发/容器内部网络中配置 `RequireHttps=false`。Identity 独自读取管理员、注册、Redis 和令牌清理配置；Business 独自读取 Workspace、Qdrant、模型和模块配置。

环境变量使用双下划线映射 JSON 层级，例如 `Jwt__ValidationPublicKeyPath`。`GetConnectionString("Identity")` 等价于读取 `ConnectionStrings:Identity`。IOptions 在服务注册时绑定对应配置节并在启动验证；密钥文件也在接受请求之前加载验证。

设计时 EF 工厂只读取各自 `ConnectionStrings__Identity` / `ConnectionStrings__FileStorage` / `ConnectionStrings__Business`，不加载整个应用的 User Secrets 或外部 JSON。迁移时通过 `Update-Database.ps1 -Context ... -ConnectionString ...` 明确传入目标；普通迁移不执行管理员播种。

排错时依次检查：当前启动的是哪一个 API、环境名称、对应 User Secrets、外部配置文件路径、进程环境变量及命令行。不要输出密钥内容、密码或真实连接串到日志。迁移脚本、VS 与 Compose 应明确使用各自环境的数据地址，不能把容器内 `localhost` 当成宿主机。

FileStorage 的 API/UserSecretsId 为 `Devkit.FileStorage.Api`，外部配置环境变量为 `DEVKIT_FILESTORAGE_CONFIG_FILE`，数据库键为 `ConnectionStrings:FileStorage`。它读取公钥、`FileStorage:DataRoot`、上传限制、用途策略和模块客户端凭据。Business 改读 `FileService`（地址、HTTPS 要求、客户端名称、服务密钥、超时和文档下载上限），不再读取文件磁盘与上传限制。完整配置见 [文件服务独立部署](文件服务独立部署.md)。
