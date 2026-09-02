# Server 协作规则

- 分层依赖只允许 `Api -> Infrastructure -> Application -> Domain`；Domain 不依赖任何基础设施。
- HTTP 契约在 `Api/Contracts` 与 OpenAPI 端点中维护；修改后同步通知 Web 与 Desktop。
- 首轮不引入数据库、EF Core、认证或业务实体。新增持久化实现必须先保留 Domain 抽象。
- 所有新端点必须有版本化路径、ProblemDetails 错误行为与自动化测试。
