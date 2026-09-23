# API 验证

## 接口

| 方法与路径 | 认证 | 说明 |
| --- | --- | --- |
| `POST /api/v1/auth/register` | 无 | 开关允许时注册普通用户 |
| `POST /api/v1/auth/login` | 无 | 用户名或邮箱登录 |
| `POST /api/v1/auth/refresh` | 无 | 单次轮换 refresh token |
| `POST /api/v1/auth/logout` | 无 | 撤销一个 refresh token |
| `POST /api/v1/auth/logout-all` | Bearer | 撤销全部会话与 access token |
| `GET /api/v1/auth/me` | Bearer | 当前用户资料，使用 Redis 缓存 |

成功响应使用：

```json
{
  "data": {},
  "traceId": "request-trace-id"
}
```

错误响应为 `application/problem+json`，并包含 `code` 与 `traceId`。完整 schema 以 `/openapi/v1.json` 为准。

## 自动测试

服务端接口新增、修改和缺陷修复必须同步新增或更新相关用例，并运行验证。推荐入口将构建、临时文件和 TRX 结果保存到统一 D 盘根目录，且不占用正在运行的开发 API 输出文件：

```powershell
./server/scripts/Test-Server.ps1
# 可按用例名称在开发过程中先运行受影响范围
./server/scripts/Test-Server.ps1 -Filter 'FullyQualifiedName~StudyEndpointTests'
# 不需独立构建目录时，也可直接运行
dotnet test server/Devkit.Server.slnx -m:1
```

API 集成测试使用独立 SQLite 数据库和内存 / 故障缓存替身，覆盖注册开关、管理员引导、登录锁定、DTO 敏感字段、refresh 轮换与重放、注销、审计 / 软删除和 Redis 降级。

备考、文件和权限模块的用例位于 `server/tests/Devkit.Server.Api.IntegrationTests`：

| 文件 | 验证行为 |
| --- | --- |
| `StudyEndpointTests.cs` | 资料版本与项目范围、查询后权限范围复核、旧题待复核、考试答案隐藏与快照判分、超时交卷、错题掌握、冲突与空值校验、模型失败、引用和生成任务防重、OpenAPI |
| `FileStorageEndpointTests.cs` | DOCX 原件与文本提取、引用保留及清理限制、上传大小与内容校验、跨用户下载限制、迁移恢复和校验、独立清理、路径边界 |
| `IdentityPermissionEndpointTests.cs` | 默认管理员、登录要求、菜单授权与撤销、数据隔离、防止越权授予管理员、最后管理员保护、改密使会话失效、空值校验 |
| `DevkitApiFactory.cs` | 隔离宿主、SQLite、文件目录及任务执行；不启动真实后台索引工作线程 |
| `TestExternalServices.cs` | 受控的 Qdrant HTTP 和模型替身；仍执行生产索引代码的序列化与过滤逻辑 |

默认测试文件位于 `D:\server data\devkit\temp\tests\devkit-tests-<随机ID>`，每个宿主独立创建并仅清理自己的目录。TRX 结果保留于 `D:\server data\devkit\temp\test-results`。可通过启动脚本的 `-DataRoot` 或直接测试时的 `DEVKIT_TEST_ROOT` 调整测试根路径。

这些用例不访问真实 SQL Server、共享 Docker 卷或模型密钥。SQLite 用例验证 HTTP 和业务行为，不替代 SQL Server 并发隔离验证；Qdrant 替身不验证真实引擎的中文分词和相关性排序。外部服务的实际联调另在隔离部署中执行。

## 实际服务冒烟

冒烟脚本要求：

- SQL Server 已迁移。
- API 和 Redis 已由 Compose 启动。
- `DEVKIT_REGISTRATION_ENABLED=true`，只用于本机验证环境。

运行：

```powershell
server/scripts/Smoke-Test.ps1 -BaseUrl http://localhost:8080
```

如果 Redis 由现有容器运行而不是本仓库 Compose 管理，可指定容器名，例如 `-RedisContainer myredis`。

脚本创建随机名称的测试用户，依次验证 readiness、注册、登录、Bearer `/me`、refresh token 轮换、注销，并通过 `redis-cli` 确认资料缓存键存在。测试账号会保留在指定数据库中，因此不要对生产数据库运行该脚本。

## 手工检查

```powershell
Invoke-RestMethod http://localhost:8080/health/live
Invoke-RestMethod http://localhost:8080/health/ready
Invoke-RestMethod http://localhost:8080/openapi/v1.json
```

停止 Redis 后，`/health/ready` 应失败；已经登录的 `/me` 请求仍应从 SQL Server 返回成功。重新启动 Redis 后，后续请求会自动回填缓存。
