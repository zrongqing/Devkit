# Server 技术栈

- 知识库：PdfPig 0.1.16 用于跨平台提取文本 PDF，DocumentFormat.OpenXml 3.5.1 用于 DOCX 段落、编号和表格读取；不依赖安装 Office。
- 检索：Qdrant 1.19.1，关键词和模型版本向量索引独立维护；通过 HttpClient 调用 REST，模型采用可配置的兼容 chat/completions 与 embeddings 接口。
- 通用文件：SQL Server 元数据和任务队列 + 可配置本地持久化目录；稳定文件 ID、SHA-256 校验和可恢复迁移。

- 平台：.NET 10、ASP.NET Core Minimal API，接口统一使用 `/api/v1` 版本路径与 ProblemDetails 错误响应。
- 分层：`Api -> Infrastructure -> Application -> Domain`；Domain 不依赖持久化、缓存或 HTTP 框架。
- 数据库与 ORM：EF Core 10.0.12；首版 provider 为 SQL Server，迁移独立保存在 Infrastructure。模型避免依赖 schema、sequence 和 rowversion，以便以后增加 MySQL、SQLite provider。
- DTO 映射：Mapster 10.0.12，使用显式映射配置并在启动注册时编译验证。
- 身份认证与授权：标准 JWT Bearer、短期 access token、数据库哈希保存且单次轮换的 refresh token、角色声明和认证版本失效机制。
- 缓存：Redis 8，通过 `IDistributedCache` 使用；缓存失败时业务回退 SQL Server，readiness 仍报告异常。
- 后台作业：ASP.NET Core `BackgroundService` + `PeriodicTimer`；当前用于清理过期 refresh token。
- 部署：Linux .NET 10 容器，Docker Compose 编排 API 与 Redis；SQL Server 2022 使用外部实例。
- OpenAPI：ASP.NET Core 内置 OpenAPI，文档地址 `/openapi/v1.json`；Swagger UI 页面地址 `/swagger`。
- 模块通信：第一阶段使用 SQL Server 持久化命令缓冲、目标模块内严格顺序、处理租约和幂等键；高吞吐阶段是否切换 Kafka、RabbitMQ 或云消息服务由压测决定。
- PLC、ERP 等外部集成：具体协议和适配器待定，按独立业务服务隔离。
- 日志、指标与分布式追踪平台：当前使用结构化控制台日志，集中式可观测性平台待定。
# 备考模块补充

- Qdrant 固定使用 `v1.19.1`，采用服务端原生 BM25 多语言分词和可选稠密向量；通过 HttpClient 调用 REST API，避免引入另一套客户端版本依赖。参见 [Qdrant 全文检索](https://qdrant.tech/documentation/search/text-search/full-text-search/)。
- 文件原件、临时目录、日志与备份默认统一放在 `D:\.server data\devkit`。生产密钥和本机模型配置通过外部配置文件或环境变量注入。
- Web / Application / Domain / Infrastructure 各自交付；考试、文件存储、身份管理按命名空间隔离，客户端不在本次实施范围。
