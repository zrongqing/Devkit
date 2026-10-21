# Docker 发布与 Visual Studio

服务端现在是 Identity、FileStorage、Business 三个独立宿主、三个数据库、三个镜像。架构、旧库升级和故障语义见 [认证与业务分离部署](认证与业务分离部署.md)。

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

限制私钥文件权限。仓库根目录首次从 `.env.example` 复制 `.env`，不要覆盖已有本机配置，填写三个数据库连接串、公钥/私钥路径及文件服务随机密钥。`.env` 不会被本机 F5 自动读取；它供 Compose 使用。

## VS 本机断点调试

1. 打开 `server/Devkit.Server.slnx`，解决方案配置启动项目，将 `Devkit.Identity.Api`、`Devkit.FileStorage.Api`、`Devkit.Business.Api` 都设为启动。各自已有 Project 类型 launch profile，HTTP 端口分别为 12512、12513、12511。
2. 右击各自 API 项目 → 管理用户机密。Identity 配置 `Jwt:SigningPrivateKeyPath`、`ConnectionStrings:Identity` 和 `Redis:ConnectionString`；Business 配置 `Jwt:ValidationPublicKeyPath`、`ConnectionStrings:Business` 以及 `FileService:ApiKey`；FileStorage 配置 `Jwt:ValidationPublicKeyPath`、`ConnectionStrings:FileStorage`、`FileStorage:DataRoot` 和同值的 `FileStorage:Clients:Business:ApiKey`（至少 32 UTF-8 字节的随机密钥）。默认开发环境 `Identity:BaseUrl=http://localhost:12512`、`Identity:RequireHttps=false`。
3. Identity 的首次管理员通过自己的 `BootstrapAccount` 用户机密配置姓名、邮箱和强密码；不要在源码中存储生产管理员密码。Business 没有管理员播种或登录服务。
4. 从仓库根目录启动开发依赖（先确保 Redis 绑定目录存在），再迁移三个调试数据库：

```powershell
./server/scripts/Start-Development.ps1 -DependenciesOnly
./server/scripts/Update-Database.ps1 -Context Identity -ConnectionString 'Server=localhost;Database=DevkitIdentity;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True'
./server/scripts/Update-Database.ps1 -Context FileStorage -ConnectionString 'Server=localhost;Database=DevkitFileStorage;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True'
./server/scripts/Update-Database.ps1 -Context Business -ConnectionString 'Server=localhost;Database=DevkitBusiness;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True'
```

5. 按 F5。认证 Swagger 为 `http://localhost:12512/swagger`，文件 Swagger 为 `http://localhost:12513/swagger`，业务 Swagger 为 `http://localhost:12511/swagger`。也可分别执行 `dotnet run --project server/src/Identity/Devkit.Identity.Api` 与 `dotnet run --project server/src/Business/Devkit.Business.Api`，再附加对应进程。

文件服务的密钥生成、配置与已有文件数据复制参见 [文件服务独立部署](文件服务独立部署.md)。仅使用 User Secrets 配置时，也可通过以下命令设置 RSA 密钥路径：

```powershell
dotnet user-secrets set 'Jwt:SigningPrivateKeyPath' 'D:/.server data/devkit/keys/identity-private.pem' --project server/src/Identity/Devkit.Identity.Api
dotnet user-secrets set 'Jwt:ValidationPublicKeyPath' 'D:/.server data/devkit/keys/business-public.pem' --project server/src/Business/Devkit.Business.Api
```

## Web 联调

复制 `web/.env.example` 为 `.env.local`，保持以下设置，然后运行 `npm --prefix web ci`、`npm --prefix web run dev`：

```dotenv
VITE_API_BASE_URL=/
VITE_DEV_API_TARGET=http://localhost:8081
```

Vite 将全部 API 转发到 Nginx 的 8081 端口，由 Nginx 分发三个服务。`Start-Development.ps1 -DependenciesOnly` 使用 `compose.development.yaml` 启动同一 devkit 分组内的 Nginx、Redis、Qdrant，Nginx 转发到宿主机 F5 的 12512、12513、12511 端口。先执行 Web 构建以生成静态入口；API 尚未启动时相关请求返回 502。完整 Compose 部署直接使用 `compose.yaml`，Nginx 转发到容器内的三个 API，前端地址不变。

## Docker Compose 发布

先完成容器三个目标库的迁移；User Secrets 不会自动传入生产容器。三个应用应使用各自受限 SQL 登录，不能依赖 Windows 集成认证。连接串中的 `$` 要注意 Compose 插值（例如使用 `$$`）。

```powershell
dotnet build server/Devkit.Server.slnx
dotnet test server/Devkit.Server.slnx
npm --prefix web ci
npm --prefix web run build
docker compose config --quiet
docker compose up -d --build
docker compose ps
```

| 用途 | 默认地址 |
| --- | --- |
| Web 与统一网关 | `http://127.0.0.1:8081` |
| Business Swagger | `http://127.0.0.1:8080/swagger` |
| Identity Swagger | `http://127.0.0.1:8082/swagger` |
| FileStorage Swagger | `http://127.0.0.1:8083/swagger` |

Nginx 只读挂载 `web/dist` 与 `web/nginx.conf`。生产 Web 构建使用同源 API 地址 `/`，各服务的 OpenAPI 不由网关聚合。公网入口需配置 HTTPS。

后端改动后执行 `docker compose up -d --build devkit-identity devkit-files devkit-server`；只改其中一侧时可选择对应服务。VS 的“生成解决方案”不会更新正在运行的 Compose 镜像。Web 改动后重新构建静态文件并刷新页面。

单独构建/发布镜像：

```powershell
./server/scripts/Publish-Server.ps1 -Context Identity -Tag 'devkit-identity:0.1.0' -Version '0.1.0'
./server/scripts/Publish-Server.ps1 -Context Business -Tag 'devkit-business:0.1.0' -Version '0.1.0'
./server/scripts/Publish-Server.ps1 -Context FileStorage -Tag 'devkit-filestorage:0.1.0' -Version '0.1.0'
```

该脚本只构建本机镜像；远程发布需使用目标仓库标签执行 `docker push`，目标主机再拉取镜像并配置 Compose、数据库、密钥和静态文件。`.env` 中的 `DEVKIT_IDENTITY_IMAGE`、`DEVKIT_FILESTORAGE_IMAGE` 与 `DEVKIT_BUSINESS_IMAGE` 可分别固定版本。

## 附加容器调试与排错

Compose 启动后，在 VS 选择 调试 → 附加到进程 → Docker (Linux Container)，查找 `devkit-identity`、`devkit-files` 或 `devkit-server` 的实际容器，附加对应 `dotnet` 进程。源码/PDB 必须匹配镜像；当前镜像为 Release，单步和变量查看可能受优化影响。

本次提供独立 Project F5 与 Compose 附加调试；没有额外引入 VS Container Tools targets 或承诺 Docker Fast Mode 一键 F5。

```powershell
docker compose logs -f devkit-identity devkit-files devkit-server
docker compose ps
docker compose down
```

普通更新不要使用 `down --volumes`，该选项会删除命名卷中的持久化数据。Identity 的 readiness 检查数据库和 Redis；FileStorage 的 readiness 检查文件数据库与身份服务；Business 的 readiness 检查业务数据库、身份服务与文件服务依赖。API 不会自动执行数据库迁移。

Nginx 现在默认启用，Compose 项目固定命名为 `devkit`。配置以官方模板机制注入上游地址，Docker DNS 在请求时解析，因此 API 容器尚未启动或重建更换 IP 时无需手动改网关。Redis、Qdrant 默认使用 `latest`，可通过 `DEVKIT_REDIS_IMAGE`、`DEVKIT_QDRANT_IMAGE` 覆盖；标签不会自动更新运行容器，需要显式 pull 和 up。详见 [统一网关与 Docker 维护](统一网关与Docker维护.md)。
