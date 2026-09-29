# Web 技术栈

- UI 组件库：Element Plus 2 与 `@element-plus/icons-vue`，用于后台布局、菜单、页签、表单和状态反馈。
- 路由：Vue Router 4，使用 Hash 模式，避免静态部署必须配置 history fallback。
- 状态管理：Pinia 3；菜单加载状态和页签状态分别管理。
- HTTP 封装：当前使用原生 `fetch`，可替换为团队标准客户端
- 导航接口：运行时由 `GET /api/v1/web/navigation/menus` 返回数据库中的可访问菜单；`routeKey` 必须在本地声明注册器中解析。菜单管理调用封装集中于 `src/api/menuManagement.ts`。
- 构建类型：使用 `@types/node` 为 Vite 配置提供 Node.js 类型，仅参与开发与 CI 类型检查
- 测试：待选择单元测试与端到端测试工具
- 备考业务：`src/modules/exam-study`；文件存储：`src/modules/file-storage`；权限管理：`src/modules/identity`。沿用现有 UI、路由、Pinia，不新增依赖。
- 登录会话：原生 fetch + 单次刷新令牌队列；会话和未提交答案暂存在 sessionStorage。请求及菜单分别检查权限，服务端承担最终访问控制。
- 备考 HTTP 契约：`src/api/examStudy.ts` 对应服务端 `/api/v1/exam-study`、`/files`、`/storage`、`/identity`。部署与使用说明见 `../server/docs/知识库与备考系统.md`。
- 部署：本机执行 `npm run build`，Compose 的 `nginx:stable-alpine` 只读挂载 `dist` 和 `nginx.conf`，同源反向代理 `/api/` 到后端；Nginx 日志使用命名卷持久化。`VITE_API_BASE_URL=/` 在生产构建时写入页面，开发环境由 Vite 代理并通过 `.env.local` 的 `VITE_DEV_API_TARGET` 指向当前调试后端。

- 模块导航：项目维护、检索、刷题分路由复用业务组件；用户、角色与权限分页面。权限目录由服务端下发，用户直接授权与角色授权合并，沿用现有依赖。系统监控调用 `/api/v1/system/runtime`。

- 菜单声明：业务目录的 `menu.ts` 由 `import.meta.glob` 自动收集，同时生成页面注册、菜单元数据及业务权限提示。编码与路由保持稳定；组件加载器不上传。菜单权限与业务权限独立，详见 `../server/docs/菜单管理与同步.md`。

- 菜单编码规范：`menuCode` 使用点分小写业务命名空间，各段以字母开头，与展示层级和 `routeKey` 独立。`page(menuCode, routeKey, ...)` 显式保留旧路由；前后端均拒绝混用横线、下划线或空段。
