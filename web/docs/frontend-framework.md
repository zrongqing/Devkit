# Web 前端框架

## 启动

```powershell
Copy-Item .env.example .env.local
npm install
npm run dev
```

默认开发接口地址为 `http://localhost:12511`。服务端 Development 配置允许来自 `localhost:5173` 和 `127.0.0.1:5173` 的请求；如端口或域名不同，请同步调整 Web 环境变量和服务端 CORS 白名单。

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
