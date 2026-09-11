# Redis 与后台服务

## Redis 缓存

服务通过 `Microsoft.Extensions.Caching.StackExchangeRedis` 注册 `IDistributedCache`。`GET /api/v1/auth/me` 使用 cache-aside：先读取 Redis，未命中或连接失败时查询 SQL Server，再尝试回填缓存。

配置示例：

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379,abortConnect=false",
    "InstanceName": "devkit:",
    "ProfileTtlMinutes": 5
  }
}
```

Redis 故障不会阻止注册、登录、刷新或用户资料查询，但会记录 Warning；`/health/ready` 会返回非健康状态。`/health/live` 只判断进程是否存活。

本机只启动 Redis：

```powershell
server/scripts/Start-Development.ps1 -DependenciesOnly
docker compose ps
docker compose logs redis
```

Compose 把 Redis 端口仅绑定到 `127.0.0.1:6379` 并启用 AOF 卷。生产环境应使用专用 Redis、认证/TLS 和密钥系统，不要直接暴露 6379。

实现遵循 [ASP.NET Core 分布式缓存文档](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-10.0)。

## BackgroundService

`RefreshTokenCleanupWorker` 是随 Web Host 启停的单例后台服务：

- 使用 `PeriodicTimer`，默认立即执行一次，之后每 60 分钟执行。
- 使用 `IServiceScopeFactory` 为每轮创建作用域，避免把 scoped `DbContext` 注入单例。
- 所有数据库调用都传递宿主取消令牌，支持容器优雅停止。
- 单轮异常记录为 Error，不终止后续周期。
- 到期 refresh token 被标记为软删除，不绕过审计机制。

配置：

```json
{
  "BackgroundJobs": {
    "RefreshTokenCleanup": {
      "Enabled": true,
      "IntervalMinutes": 60
    }
  }
}
```

设计依据：[ASP.NET Core Hosted Services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0)和用户提供的 [C#.NET BackgroundService 详解](https://www.cnblogs.com/TangQF/articles/18988475)。
