# Server 协作规则

- `server` 是服务端根目录，业务源码位于 `server/src`，解决方案入口为 `server/Devkit.Server.slnx`。
- 服务端开发文档默认写入 `server/docs`；用户明确指定其他位置时按用户要求执行，不主动迁移已有文档。
- 开始服务端任务前读取仓库根目录的 `.agents/harness/server.md`。
- 分层依赖只允许 `Api -> Infrastructure -> Application -> Domain`；Domain 不依赖任何基础设施。
- HTTP 契约在 `Api/Contracts` 与 OpenAPI 端点中维护；修改后同步通知 Web 与 Desktop。
- 首轮不引入数据库、EF Core、认证或业务实体。新增持久化实现必须先保留 Domain 抽象。
- 所有新端点必须有版本化路径与 ProblemDetails 错误行为。
- 除非用户明确要求，否则不创建、修改或运行测试；默认使用 `dotnet build server/Devkit.Server.slnx` 验证服务端变更。
