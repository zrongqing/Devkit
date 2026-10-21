# IConfiguration 配置与识别原则

三个服务分别使用自己的 `WebApplication.CreateBuilder(args)` 和配置实例。数据库集中配置于 `server/database.json`，通过 `DEVKIT_DATABASE_CONFIG_FILE` 指定仓库外的同结构 JSON，详见 [数据库与认证](数据库与认证.md)。

默认应用配置优先级从低到高为：`appsettings.json`、环境 JSON、Development User Secrets、环境变量、命令行。`launchSettings.json` 提供启动环境变量，不会被 Compose 读取；仓库根目录 `.env` 用于 Compose 插值，不会被 VS Project F5 自动读取。

各服务可显式加载外部 JSON 文件，随后重新添加环境变量和命令行，非数据库项最终优先级为：默认配置 → 服务外部文件 → 环境变量 → 命令行。数据库配置在服务外部文件之后加载：统一默认文件 → `DEVKIT_DATABASE_CONFIG_FILE` → 环境变量 → 命令行，覆盖旧的服务级数据库文件设置。原 `DEVKIT_CONFIG_FILE` 和旧的单一 UserSecretsId 不再使用。

上述加载流程由 `Devkit.AspNetCore` 的 `AddDevkitExternalConfiguration` 统一实现，各宿主显式传入自己的环境变量名。`AddDevkitApi` 在 CORS 选项构建时读取允许来源；`AddDevkitDatabaseConfiguration` 统一加载数据库文件；`AddDevkitSqlServer<TContext>` 在启动时验证共享连接，再创建各自上下文。共用同一配置入口不会合并各服务的 DbContext 或实体。

三个宿主加载外部配置后调用 `AddDevkitDevelopmentJwtKey`。仅 Development 环境且本服务对应密钥路径为空时，自动补充当前用户本地应用数据目录 `Devkit/development/jwt` 下的持久化密钥路径；可通过 `Jwt:DevelopmentKeyDirectory` 指定共同目录。任意服务首次启动会协调创建同一对 RSA 密钥，后续启动复用；Identity 使用私钥，Business / FileStorage 使用公钥。此默认行为不写入配置文件或 User Secrets，不覆盖显式密钥路径，不适用于其他环境。自定义密钥必须分别显式配置配对的私钥和公钥路径。详见 [Docker 发布与 Visual Studio](Docker发布与VisualStudio.md)。

| 配置项 | Identity | FileStorage | Business |
| --- | --- | --- | --- |
| API 项目 / UserSecretsId | `Devkit.Identity.Api` | `Devkit.FileStorage.Api` | `Devkit.Business.Api` |
| 外部文件环境变量 | `DEVKIT_IDENTITY_CONFIG_FILE` | `DEVKIT_FILESTORAGE_CONFIG_FILE` | `DEVKIT_BUSINESS_CONFIG_FILE` |
| 数据库 JSON 键 | `ConnectionStrings:Devkit` | `ConnectionStrings:Devkit` | `ConnectionStrings:Devkit` |
| 数据库环境变量 | `ConnectionStrings__Devkit` | `ConnectionStrings__Devkit` | `ConnectionStrings__Devkit` |
| 密钥文件键 | `Jwt:SigningPrivateKeyPath` | `Jwt:ValidationPublicKeyPath` | `Jwt:ValidationPublicKeyPath` |

FileStorage 与 Business 分别读取 `Identity:BaseUrl`、`Identity:RequireHttps` 和 `Identity:TimeoutSeconds`；生产默认要求 HTTPS。只在明确受控的开发/容器内部网络中配置 `RequireHttps=false`。Identity 独自读取管理员、注册、Redis 和令牌清理配置；Business 独自读取 Workspace、Qdrant、模型和模块配置。

环境变量使用双下划线映射 JSON 层级，例如 `Jwt__ValidationPublicKeyPath`。`GetConnectionString("Devkit")` 等价于读取 `ConnectionStrings:Devkit`。IOptions 在服务注册时绑定对应配置节并在启动验证；密钥文件也在接受请求之前加载验证。

设计时 EF 工厂与运行时使用相同的 `DatabaseConfiguration` 加载和校验数据库：统一默认文件、统一外部文件、环境变量、命令行。它不加载 API 的 User Secrets 或服务级外部 JSON。迁移使用 `Update-Database.ps1 -Context ...`，可选 `-DatabaseConfigFile`；`-ConnectionString` 为单次覆盖。普通迁移不执行管理员播种。

排错时依次检查：当前 API、环境名称、统一数据库文件、进程环境变量及命令行；其他配置另检查对应 User Secrets 与服务外部文件。不要输出密钥内容、密码或真实连接串到日志。迁移脚本、VS 与 Compose 应明确使用各自环境的数据地址，不能把容器内 `localhost` 当成宿主机。

FileStorage 的 API/UserSecretsId 为 `Devkit.FileStorage.Api`，外部配置环境变量为 `DEVKIT_FILESTORAGE_CONFIG_FILE`，数据库键为 `ConnectionStrings:Devkit`。它读取公钥、`FileStorage:DataRoot`、上传限制、用途策略和模块客户端凭据。Business 改读 `FileService`（地址、HTTPS 要求、客户端名称、服务密钥、超时和文档下载上限），不再读取文件磁盘与上传限制。完整配置见 [文件服务独立部署](文件服务独立部署.md)。
