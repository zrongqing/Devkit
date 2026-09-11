# Docker 发布与 Visual Studio

## 前置条件

- .NET 10 SDK。
- Docker Desktop 使用 Linux containers。
- SQL Server 2022 已启用 TCP/IP，监听固定端口 1433，并允许 Docker 网络访问。
- 容器访问 Windows 主机时使用 `host.docker.internal`；Linux 容器通常不能直接使用 Windows 集成认证，因此使用权限受限的 SQL 登录。

## 本机 Compose 发布

复制环境模板并替换全部示例值：

```powershell
Copy-Item .env.example .env
notepad .env
```

`.env` 已被 Git 忽略，不要提交。连接串中的 `$` 如被 Compose 解释，需要写成 `$$` 或改从安全 secret provider 注入。
如果本机 6379 已被其他 Redis 占用，可把 `DEVKIT_REDIS_HOST_PORT` 改为 6380；API 容器仍通过 Compose 内部地址 `redis:6379` 访问。

先使用主机可访问的连接串迁移数据库，再构建并启动：

```powershell
server/scripts/Update-Database.ps1 `
  -ConnectionString "Server=localhost;Database=Devkit;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True"
server/scripts/Start-Development.ps1
docker compose ps
```

单独构建带版本的镜像：

```powershell
server/scripts/Publish-Server.ps1 -Tag "devkit-server:0.1.0" -Version "0.1.0"
```

停止服务但保留 Redis 数据：

```powershell
docker compose down
```

只有明确需要清空本地 Redis 数据时才使用 `docker compose down --volumes`。

## Visual Studio 设置

1. 安装完整 Visual Studio 的 ASP.NET 和 Web 开发、容器开发工具工作负载；本机当前只检测到 Build Tools，GUI 步骤需要完整 IDE。
2. 打开 `server/Devkit.Server.slnx`，将 `Devkit.Server.Api` 设为启动项目。
3. 使用 Manage User Secrets 设置 `Jwt:SigningKey`；如需覆盖数据库或管理员配置，也放在 User Secrets。
4. 选择 `Devkit.Server.Api` profile 可在 Windows 主机调试，默认地址为 `https://localhost:12510` 和 `http://localhost:12511`。
5. 选择 Docker profile 可使用 Dockerfile 调试；如 IDE 未识别该 profile，在项目上执行 Add > Docker Support，使 Visual Studio 安装/补齐容器工具 targets。
6. Docker 调试前先运行数据库迁移，并确保 SQL Server TCP/IP、SQL 登录、Windows 防火墙和证书信任配置正确。

常见错误：

- API 启动提示 JWT key 不足 32 字节：检查 User Secrets 或 `Jwt__SigningKey`。
- 容器无法连接 SQL Server：检查是否使用 `host.docker.internal,1433`、SQL Server Configuration Manager 中的 TCP/IP、SQL 登录模式和防火墙。
- TLS/证书错误：本机开发可使用 `Encrypt=True;TrustServerCertificate=True`；生产应部署可信服务器证书。
- API 启动时提示表不存在：先执行 `Update-Database.ps1`，服务不会自动迁移。

参考：[Visual Studio Container Tools](https://learn.microsoft.com/en-us/visualstudio/containers/container-tools?view=vs-2022)。
