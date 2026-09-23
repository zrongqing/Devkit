# Server 协作规则

- `server` 是服务端根目录，业务源码位于 `server/src`，解决方案入口为 `server/Devkit.Server.slnx`。
- 服务端开发文档默认写入 `server/docs`；用户明确指定其他位置时按用户要求执行，不主动迁移已有文档。
- 开始服务端任务前读取仓库根目录的 `.agents/harness/server.md`。
- 分层依赖只允许 `Api -> Infrastructure -> Application -> Domain`；Domain 不依赖任何基础设施。
- HTTP 契约在 `Api/Contracts` 与 OpenAPI 端点中维护；修改后同步通知 Web 与 Desktop。
- 新增持久化实现必须先保留 Domain 抽象。
- 所有新端点必须有版本化路径与 ProblemDetails 错误行为。
- 服务端接口新增、修改或修复时，必须同步新增或更新 `server/tests` 中的相关用例，覆盖正常流程、参数校验、认证 / 权限及关键失败路径；不得仅修改断言以绕过行为缺陷。

- 验证执行 `dotnet build server/Devkit.Server.slnx`；接口变更完成前执行 `dotnet test server/Devkit.Server.slnx`，失败后修复根因并重新运行，报告命令与结果。
- 测试使用隔离数据库、文件目录和可控外部服务替身，不读写真实业务数据或依赖个人密钥。
