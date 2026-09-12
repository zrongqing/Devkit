# Server 技术栈

- 平台：.NET 10、ASP.NET Core Minimal API，接口统一使用 `/api/v1` 版本路径与 ProblemDetails 错误响应。
- 分层：`Api -> Infrastructure -> Application -> Domain`；Domain 不依赖持久化、缓存或 HTTP 框架。
- 数据库与 ORM：EF Core 10.0.12；首版 provider 为 SQL Server，迁移独立保存在 Infrastructure。模型避免依赖 schema、sequence 和 rowversion，以便以后增加 MySQL、SQLite provider。
- DTO 映射：Mapster 10.0.12，使用显式映射配置并在启动注册时编译验证。
- 身份认证与授权：标准 JWT Bearer、短期 access token、数据库哈希保存且单次轮换的 refresh token、角色声明和认证版本失效机制。
- 缓存：Redis 8，通过 `IDistributedCache` 使用；缓存失败时业务回退 SQL Server，readiness 仍报告异常。
- 后台作业：ASP.NET Core `BackgroundService` + `PeriodicTimer`；当前用于清理过期 refresh token。
- 部署：Linux .NET 10 容器，Docker Compose 编排 API 与 Redis；SQL Server 2022 使用外部实例。
- OpenAPI：ASP.NET Core 内置 OpenAPI，文档地址 `/openapi/v1.json`；Swagger UI 页面地址 `/swagger`。
- 消息队列、PLC、ERP 等外部集成：待定。
- 日志、指标与分布式追踪平台：当前使用结构化控制台日志，集中式可观测性平台待定。
