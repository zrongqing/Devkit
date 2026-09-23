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

Visual Studio 中的“生成解决方案”和 F5 用于编译、调试；它们不会更新 Compose 正在运行的 Release 镜像。改完服务端代码后，在仓库根目录执行 `docker compose up -d --build devkit-server`，或者使用上面的脚本构建镜像，并让 Compose 的 `DEVKIT_SERVER_IMAGE` 指向相同标签后重新创建服务。

若目标是将镜像推送到镜像仓库，在 VS 中右击 `Devkit.Server.Api` → **发布** → **Docker Container Registry**，选择 Docker Hub、Azure Container Registry 或其他仓库并设置发布凭据。此操作发布到仓库；本机 Docker Desktop 的 Compose 启动仍使用仓库根目录的 `compose.yaml` 和 `.env` 中配置的镜像标签。不要把含密码的发布 profile 提交到仓库。

## Web 容器发布

Compose 的 `nginx` 服务直接使用 `nginx:stable-alpine`。本机先执行 `npm run build`，Nginx 只读挂载 `web/dist` 和 `web/nginx.conf`，提供静态页面并把 `/api/` 转发给 Compose 中的 `devkit-server:8080`。`web/.env.production` 将 API 根地址设为 `/`，浏览器始终请求页面所在的域名和端口；无需为生产环境单独开放 CORS。Vite 的 `VITE_` 变量在构建时写入页面，修改后必须重新执行 `npm run build`。

先完成上面的 `.env`、数据库迁移及后端配置，再在仓库根目录运行：

```powershell
Push-Location web
npm ci
npm run build
Pop-Location
docker compose --profile web up -d --build
docker compose ps
```

默认浏览器地址是 `http://127.0.0.1:8081`，API 仍可在主机上通过 `http://127.0.0.1:8080` 单独访问。Nginx 使用可选 `web` profile，因此原有 `server/scripts/Start-Development.ps1` 仍只启动后端及其依赖。只改 Web 后，重新运行 `npm run build` 并刷新浏览器；Nginx 会立即读取更新后的 `web/dist`。需要从局域网访问时，在 `.env` 中设置 `DEVKIT_WEB_HOST_IP` 和 `DEVKIT_WEB_HOST_PORT`，并按实际网络配置防火墙与 HTTPS 入口。

Nginx 的访问与错误日志写入 `devkit_nginx-logs` 命名卷；静态文件位于宿主机 `web/dist`，配置位于仓库 `web/nginx.conf`，两者都不会随容器重建丢失。可以用 `docker compose --profile web exec nginx ls -lh /var/log/nginx` 检查日志，并定期轮转或归档。`docker compose down` 保留现有命名卷和宿主机数据；不要为普通更新执行 `down --volumes`。Windows 宿主机运行的后端和 Linux 容器运行的后端不应共用含 Windows 路径的 StorageLocations 数据库；切换运行方式时按项目的数据迁移说明处理。

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

### Web 与后端联调地址

在 `web` 目录复制 `.env.example` 为 `.env.local`，执行 `npm ci`、`npm run dev`。前端调试使用 Vite 热更新和浏览器开发者工具；生产 Nginx 镜像没有 Vite 热更新。保持 `.env.local` 中 `VITE_API_BASE_URL=/`，根据当前后端启动方式设置 `VITE_DEV_API_TARGET`；Vite 将同源 `/api` 请求代理到目标后端：

| 后端启动方式 | API 地址 | Web `.env.local` |
| --- | --- | --- |
| VS `Devkit.Server.Api` profile（主机 F5） | `http://localhost:12511` | `VITE_DEV_API_TARGET=http://localhost:12511` |
| `docker compose up -d --build devkit-server` | `http://localhost:8080` | `VITE_DEV_API_TARGET=http://localhost:8080` |
| VS `Docker` profile（容器 F5） | VS 启动后显示的 HTTP 映射端口 | 使用该端口，例如 `VITE_DEV_API_TARGET=http://localhost:<端口>` |

改动 `.env.local` 后重启 Vite。Vite 和完整 Compose 部署分别通过开发代理和 Nginx 代理访问后端，不需要为生产后端开放跨域。

VS 的 `Docker` F5 是独立的调试容器，不会读取 Compose `.env` 或自动连接 Compose 的 Redis/Qdrant 网络；需要在该调试 profile 中配置容器可访问的数据库、Redis、Qdrant 地址及 JWT 密钥。日常断点调试最直接的是主机 `Devkit.Server.Api` profile + Vite；验证容器部署时使用完整 Compose。

常见错误：

- API 启动提示 JWT key 不足 32 字节：检查 User Secrets 或 `Jwt__SigningKey`。
- 容器无法连接 SQL Server：检查是否使用 `host.docker.internal,1433`、SQL Server Configuration Manager 中的 TCP/IP、SQL 登录模式和防火墙。
- TLS/证书错误：本机开发可使用 `Encrypt=True;TrustServerCertificate=True`；生产应部署可信服务器证书。
- API 启动时提示表不存在：先执行 `Update-Database.ps1`，服务不会自动迁移。

参考：[Visual Studio Container Tools](https://learn.microsoft.com/en-us/visualstudio/containers/container-tools?view=vs-2022)。
