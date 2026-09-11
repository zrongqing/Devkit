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

```powershell
dotnet test src/server/Devkit.Server.slnx
```

API 集成测试使用临时 SQLite 数据库和内存/故障缓存替身，覆盖注册开关、管理员引导、登录锁定、DTO 敏感字段、refresh 轮换与重放、注销、审计/软删除和 Redis 降级。

## 实际服务冒烟

冒烟脚本要求：

- SQL Server 已迁移。
- API 和 Redis 已由 Compose 启动。
- `DEVKIT_REGISTRATION_ENABLED=true`，只用于本机验证环境。

运行：

```powershell
src/server/scripts/Smoke-Test.ps1 -BaseUrl http://localhost:8080
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
