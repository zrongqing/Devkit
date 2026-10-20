# Docker 发布与 Visual Studio

服务端现在是两个独立项目、两个数据库、两个镜像。架构、旧库升级和故障语义见 [认证与业务分离部署](认证与业务分离部署.md)。

## 前置条件

- 支持 .NET 10 的 Visual Studio 2026、ASP.NET 和 Web 开发及容器工具组件；Docker Desktop 使用 Linux containers。
- 本机 .NET SDK 满足仓库 `global.json`。SQL Server 开启 TCP/IP；Linux 容器连接 Windows 主机时使用 `host.docker.internal,1433` 和 SQL 登录。
- 本地调试与容器使用不同数据库/数据根目录，避免混用 Windows 与 Linux 存储路径。

## 初次准备

在仓库外生成 RSA 密钥，例如使用已有 OpenSSL（仅首次生成，不要覆盖正在使用的密钥）：

```powershell
New-Item -ItemType Directory -Force 'D:/.server data/devkit/keys'
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out 'D:/.server data/devkit/keys/identity-private.pem'
openssl pkey -in 'D:/.server data/devkit/keys/identity-private.pem' -pubout -out 'D:/.server data/devkit/keys/business-public.pem'
```

限制私钥文件权限。仓库根目录首次从 `.env.example` 复制 `.env`，不要覆盖已有本机配置，填写两个数据库连接串及密钥路径。`.env` 不会被本机 F5 自动读取；它供 Compose 使用。

## VS 本机断点调试

1. 打开 `server/Devkit.Server.slnx`，解决方案配置启动项目，将 `Devkit.Identity.Api`、`Devkit.Business.Api` 都设为启动。各自已有 Project 类型 launch profile，HTTP 端口分别为 12512、12511。
2. 右击各自 API 项目 → 管理用户机密。Identity 配置 `Jwt:SigningPrivateKeyPath`、`ConnectionStrings:Identity` 和 `Redis:ConnectionString`；Business 配置 `Jwt:ValidationPublicKeyPath`、`ConnectionStrings:Business` 以及自己的 `Workspace:DataRoot`。默认开发环境 `Identity:BaseUrl=http://localhost:12512`、`Identity:RequireHttps=false`。
3. Identity 的首次管理员通过自己的 `BootstrapAccount` 用户机密配置姓名、邮箱和强密码；不要在源码中存储生产管理员密码。Business 没有管理员播种或登录服务。
4. 从仓库根目录启动开发依赖（先确保 Redis 绑定目录存在），再迁移两个调试数据库：

```powershell
./server/scripts/Start-Development.ps1 -DependenciesOnly
./server/scripts/Update-Database.ps1 -Context Identity -ConnectionString 'Server=localhost;Database=DevkitIdentity;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True'
./server/scripts/Update-Database.ps1 -Context Business -ConnectionString 'Server=localhost;Database=DevkitBusiness;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True'
```

5. 按 F5。认证 Swagger 为 `http://localhost:12512/swagger`，业务 Swagger 为 `http://localhost:12511/swagger`。也可分别执行 `dotnet run --project server/src/Identity/Devkit.Identity.Api` 与 `dotnet run --project server/src/Business/Devkit.Business.Api`，再附加对应进程。

仅使用 User Secrets 配置时，也可通过以下命令设置两个密钥路径：

```powershell
dotnet user-secrets set 'Jwt:SigningPrivateKeyPath' 'D:/.server data/devkit/keys/identity-private.pem' --project server/src/Identity/Devkit.Identity.Api
dotnet user-secrets set 'Jwt:ValidationPublicKeyPath' 'D:/.server data/devkit/keys/business-public.pem' --project server/src/Business/Devkit.Business.Api
```

## Web 联调

复制 `web/.env.example` 为 `.env.local`，保持以下设置，然后运行 `npm --prefix web ci`、`npm --prefix web run dev`：

```dotenv
VITE_API_BASE_URL=/
VITE_DEV_API_TARGET=http://localhost:12511
VITE_DEV_AUTH_TARGET=http://localhost:12512
```

Vite 将认证、身份管理和导航菜单转发到 Identity，其余业务接口转发到 Business。若联调容器实例，将两个目标分别改为 8080、8082，修改后重启 Vite。

## Docker Compose 发布

先完成容器目标库的两次迁移；User Secrets 不会自动传入生产容器。两个应用应使用各自受限 SQL 登录，不能依赖 Windows 集成认证。连接串中的 `$` 要注意 Compose 插值（例如使用 `$$`）。

```powershell
dotnet build server/Devkit.Server.slnx
dotnet test server/Devkit.Server.slnx
npm --prefix web ci
npm --prefix web run build
docker compose --profile web config --quiet
docker compose --profile web up -d --build
docker compose ps
```

| 用途 | 默认地址 |
| --- | --- |
| Web 与统一网关 | `http://127.0.0.1:8081` |
| Business Swagger | `http://127.0.0.1:8080/swagger` |
| Identity Swagger | `http://127.0.0.1:8082/swagger` |

Nginx 只读挂载 `web/dist` 与 `web/nginx.conf`。生产 Web 构建使用同源 API 地址 `/`，两个服务的 OpenAPI 不由网关聚合。公网入口需配置 HTTPS。

后端改动后执行 `docker compose up -d --build devkit-identity devkit-server`；只改其中一侧时可选择对应服务。VS 的“生成解决方案”不会更新正在运行的 Compose 镜像。Web 改动后重新构建静态文件并刷新页面。

单独构建/发布镜像：

```powershell
./server/scripts/Publish-Server.ps1 -Context Identity -Tag 'devkit-identity:0.1.0' -Version '0.1.0'
./server/scripts/Publish-Server.ps1 -Context Business -Tag 'devkit-business:0.1.0' -Version '0.1.0'
```

该脚本只构建本机镜像；远程发布需使用目标仓库标签执行 `docker push`，目标主机再拉取镜像并配置 Compose、数据库、密钥和静态文件。`.env` 中的 `DEVKIT_IDENTITY_IMAGE` 与 `DEVKIT_BUSINESS_IMAGE` 可分别固定版本。

## 附加容器调试与排错

Compose 启动后，在 VS 选择 调试 → 附加到进程 → Docker (Linux Container)，查找 `devkit-identity` 或 `devkit-server` 的实际容器，附加对应 `dotnet` 进程。源码/PDB 必须匹配镜像；当前镜像为 Release，单步和变量查看可能受优化影响。

本次提供独立 Project F5 与 Compose 附加调试；没有额外引入 VS Container Tools targets 或承诺 Docker Fast Mode 一键 F5。

```powershell
docker compose logs -f devkit-identity devkit-server
docker compose ps
docker compose down
```

普通更新不要使用 `down --volumes`，该选项会删除命名卷中的持久化数据。Identity 的 readiness 检查数据库和 Redis；Business 的 readiness 检查业务数据库与身份服务依赖。API 不会自动执行数据库迁移。
