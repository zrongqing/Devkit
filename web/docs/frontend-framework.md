# Web 前端框架

## 启动

```powershell
npm install
npm run dev
```

可通过启动命令选择完整的代理配置，无需修改 `.env.local`：

| 命令（在 `web` 目录执行） | API 转发目标 | 前置条件 |
| --- | --- | --- |
| `npm run dev`（默认）或 `npm run dev:direct` | Business `12511`、Application `12512`、FileStorage `12513` | 从 VS Project / `dotnet run` 启动三个 API |
| `npm run dev:gateway` | Nginx `8081`，由网关分发三个 API | 完整 Compose 部署，或开发网关加本机三个 API |

开发网关模式先运行 `server/scripts/Start-Development.ps1 -DependenciesOnly`，再启动各 API；完整容器部署使用仓库根目录的 `docker compose up -d --build`。`.env.direct`、`.env.gateway` 提供模式预设并覆盖 `.env.local` 中的同名地址；自定义地址分别写入忽略提交的 `.env.direct.local`、`.env.gateway.local`。切换模式时停止当前 Vite，再执行对应命令。生产构建仍使用同源 Nginx 网关。

服务端 Development 配置允许来自 `localhost:5173` 和 `127.0.0.1:5173` 的请求；如端口或域名不同，请同步调整 Web 环境变量和服务端 CORS 白名单。

默认本机模式由 `.env.direct` 提供以下地址，无需复制环境文件：

```dotenv
VITE_API_BASE_URL=/
VITE_DEV_API_TARGET=http://localhost:12511
VITE_DEV_AUTH_TARGET=http://localhost:12512
VITE_DEV_FILE_TARGET=http://localhost:12513
```

三个地址分别对应 Business、Application 和 FileStorage 的 Project 启动配置。浏览器仍访问 Vite 的 `5173` 端口，Vite 保留 `/api/v1/...` 路径并转发到对应服务。三个后端均需启动，登录、权限和菜单依赖 Application。

连接 Docker 时停止当前 Vite，然后执行 `npm run dev:gateway`；切回本机执行 `npm run dev`。如果 Docker 网关部署在其他机器或使用了不同端口，只需在 `web/.env.gateway.local` 中配置 `VITE_DEV_API_TARGET=http://<宿主机地址>:<网关端口>`。浏览器 API 地址仍保持同源相对路径，不必修改调用代码。

`.env.example` 仅作配置参考，Vite 不会直接加载它；修改模式对应的 `.local` 文件后重启开发服务器。本机配置文件已被 Git 忽略。

生产构建使用：

```powershell
npm run build
```

## 技术组成

- Vue 3 + TypeScript + Vite 6
- Vue Router 4，Hash 路由
- Pinia 3，分别管理导航与页签
- Element Plus 2 与 Element Plus Icons
- 原生 `fetch`，所有业务 API 集中在 `src/api`

Vue Router 保持在 4.x；Router 5 的当前版本要求更新的 Vite 主版本，不适用于本工程现状。

## 目录职责

```text
src/
  api/                    HTTP 调用
  components/navigation/ 递归菜单与导航图标
  layouts/                系统工作区布局
  router/                 Hash 路由与页面注册表
  stores/                 菜单和页签状态
  types/                  Web 导航类型
  views/                  欢迎页、通用页与示例页
  modules/<module>/       后续独立业务模块
```

业务组件不得自行拼接菜单接口 URL。Web 菜单统一由 `src/api/navigation.ts` 请求 `/api/v1/web/navigation/menus`。

## 页面和页签行为

- `#/` 是欢迎页；“进入系统”跳转到 `#/system/home`。
- `#/system/:routeKey` 表示当前工作区页面。
- 首页页签始终位于第一位且不可关闭。
- 菜单 ID 是页签唯一键；重复点击菜单只会激活已有页签。
- 关闭当前页签后激活左侧相邻页签，最后回到首页。
- 浏览器前进/后退会同步活动页签；刷新只恢复首页和当前 URL 指向的页面。
- 菜单接口失败时只保留首页，并在侧栏显示重试操作。
- 服务端返回但本地未注册的 `routeKey` 会打开“页面尚未部署”占位页。

Element Plus 的页签面板在打开后保持挂载，关闭页签时对应页面才被移除。因此普通页签切换可以保留组件的内存状态，但浏览器刷新不会持久化状态。

## 新增 Web 模块

1. 在 `src/modules/<module>` 建立模块目录和页面组件。
2. 在 `src/router/pageRegistry.ts` 增加 `routeKey` 到组件的显式映射。
3. 在服务端 `Navigation:Common` 或 `Navigation:Web` 添加对应菜单配置。
4. 确认目录项没有 `TargetKey`，页面叶子具有 `TargetKey`。
5. 执行 `npm run build`，再检查深链、重复打开、关闭和刷新行为。

不要把服务端传回的字符串用于动态 `import()`；页面代码必须经过本地注册和正常构建发布。
