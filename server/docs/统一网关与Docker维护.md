# 统一网关与 Docker 维护

Identity、FileStorage 和 Business 保持独立领域与独立部署。Nginx 提供统一 HTTP 入口，浏览器只访问 `http://localhost:8081`，前端默认使用相对路径 `/api/v1/...`。Docker Compose 项目固定为 `devkit`，Nginx 默认启用，无需额外的 `web` profile。

| 请求 | 转发目标 |
| --- | --- |
| `/api/v1/auth`、`identity`、`web`、`client/navigation` | Identity |
| `/api/v1/files`、`storage` | FileStorage |
| 其他 `/api/` | Business |
| 页面与静态资源 | `web/dist`，支持 SPA 路由 |

## VS / dotnet run 调试

先构建 Web，再启动基础设施与网关：

```powershell
npm --prefix web ci
npm --prefix web run build
./server/scripts/Start-Development.ps1 -DependenciesOnly
```

该脚本使用 `compose.yaml` 和 `compose.development.yaml`，只启动同一分组内的 Nginx、Redis、Qdrant；网关分别转发到 `host.docker.internal:12512`、`:12513`、`:12511`。在 VS 启动三个 API 后可直接访问 8081 页面。API 未启动时，对应 API 请求返回 502，网关本身仍可正常提供页面。

需要 Vite 热更新时，Web 只需：

```dotenv
VITE_API_BASE_URL=/
VITE_DEV_API_TARGET=http://localhost:8081
```

Vite 将所有 API 送到 Nginx，由网关选择后端。`VITE_DEV_AUTH_TARGET`、`VITE_DEV_FILE_TARGET` 和生产直连地址仍可显式覆盖，统一网关场景无需配置。已有 `.env.local` 若还指向 12511，请将其改为 8081，并去掉不需要的直连覆盖项。

## 完整容器部署

补齐 `.env`、三个数据库及服务密钥后，从仓库根目录执行：

```powershell
docker compose config --quiet
docker compose up -d --build
```

默认配置将网关转发到组内的 `devkit-identity:8080`、`devkit-files:8080`、`devkit-server:8080`。从 VS 调试切换到容器部署时，执行上述命令即可重新应用容器上游配置，前端仍使用相同的 8081 入口。

`web/nginx.conf` 作为 `/etc/nginx/templates/default.conf.template` 挂载，由官方 Nginx 镜像入口脚本替换上游环境变量。Docker DNS 在请求时解析，网关可以先于 API 启动，API 重建更换地址后也会重新解析。三个 API 的 Swagger 仍分别位于其独立端口，未进行 OpenAPI 聚合。

## Redis / Qdrant 镜像策略

默认使用官方 `redis:latest`、`qdrant/qdrant:latest`。可在 `.env` 覆盖 `DEVKIT_REDIS_IMAGE`、`DEVKIT_QDRANT_IMAGE` 来固定版本。`latest` 是可移动标签，不会后台替换正在运行的容器；更新时显式拉取并重建：

```powershell
docker compose pull redis qdrant
docker compose up -d redis qdrant
```

VS 调试模式下，up 命令继续同时指定两个 Compose 文件。升级前备份持久化数据并核对实际版本；Qdrant 跨次版本升级按[官方升级顺序](https://qdrant.tech/documentation/operations/upgrades/)执行，避免跳过要求的中间版本。Redis 镜像标签见[官方镜像说明](https://hub.docker.com/_/redis)，Qdrant 的 latest 配置见[官方安装说明](https://qdrant.tech/documentation/installation/)。

本次切换已验证 Redis 8.10.2、Qdrant 1.19.2；升级前停止依赖并归档原 Redis 数据目录及 Qdrant storage/snapshots。备份保存在仓库外的 `D:\.server data\devkit\backups\docker\20261020-231811`，不属于镜像清理范围。Qdrant 仍使用原命名卷，Redis 仍使用原绑定目录。

## 镜像与数据维护

本次验证镜像已整理为三个可复用标签：`devkit-identity:local`、`devkit-business:local`、`devkit-filestorage:local`。临时 `*-check` 标签、旧合并服务镜像及已替换的依赖镜像已清理；其他项目的镜像、容器和持久化数据保留。

同时清理了未使用的构建缓存，下次 Docker 构建会重新生成需要的缓存。日常检查使用 `docker image ls`、`docker system df`；删除前核对所有运行和已停止容器的镜像引用。不要用 `docker system prune --volumes` 或 `docker compose down --volumes` 处理镜像堆积。
