# Server 协作规则

- `server` 是服务端根目录，业务源码位于 `server/src`，解决方案入口为 `server/Devkit.Server.slnx`。
- 服务端开发文档默认写入 `server/docs`；用户明确指定其他位置时按用户要求执行，不主动迁移已有文档。
- 开始服务端任务前读取仓库根目录的 `.agents/harness/server.md`。
- 分层依赖只允许 `Api -> Infrastructure -> Application -> Domain`；Domain 不依赖任何基础设施。
- HTTP 契约在 `Api/Contracts` 与 OpenAPI 端点中维护；修改后同步通知 Web 与 Desktop。
- 新增持久化实现必须先保留 Domain 抽象。
- 每张持久化表默认通过 Domain 公共 `IPagedQueryStore` 与 Infrastructure 公共 `PagedDbContext.PageAsync<T>` 提供全字段分页查询，包含组合主键；调用方必须传入数据范围，保留软删除与权限过滤。新增 DbContext 继承此基类，不在各端点复制分页算法。
- 列表接口统一使用 `PageQuery` / `PageResult<T>`，默认每页 20 条，上限 100；筛选条件 AND 合并，排序数组顺序表示优先级（最多 8 列），自动补充唯一键。先过滤授权数据，再统计总数、排序、分页；禁止先 Take 固定条数再查询。
- 对外只暴露应用服务授权后的 DTO 与明确登记的查询字段；密码哈希、令牌、密钥、内部路径等敏感字段必须通过投影排除，不接受客户端任意表名、类型名或 SQL。未知字段、操作符和无效参数返回 400 ProblemDetails（`invalid_query`）。
- 普通查询在数据库执行；权限汇总、菜单树与其他派生视图由服务端在完整授权快照上查询，需在契约中说明成本与分页单位，不得将筛选交给浏览器。新接口及其 Web 调用模板必须同步遵守这些默认规则。
- 所有新端点必须有版本化路径与 ProblemDetails 错误行为。
- 服务端接口新增、修改或修复时，必须同步新增或更新 `server/tests` 中的相关用例，覆盖正常流程、参数校验、认证 / 权限及关键失败路径；不得仅修改断言以绕过行为缺陷。

- 验证执行 `dotnet build server/Devkit.Server.slnx`；接口变更完成前执行 `dotnet test server/Devkit.Server.slnx`，失败后修复根因并重新运行，报告命令与结果。
- 测试使用隔离数据库、文件目录和可控外部服务替身，不读写真实业务数据或依赖个人密钥。
