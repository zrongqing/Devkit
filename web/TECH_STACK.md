# Web 技术栈

- UI 组件库：Element Plus 2 与 `@element-plus/icons-vue`，用于后台布局、菜单、页签、表单和状态反馈。
- 路由：Vue Router 4，使用 Hash 模式，避免静态部署必须配置 history fallback。
- 状态管理：Pinia 3；菜单加载状态和页签状态分别管理。
- HTTP 封装：当前使用原生 `fetch`，可替换为团队标准客户端
- 导航接口：只调用 `GET /api/v1/web/navigation/menus`；`routeKey` 必须在本地页面注册表中解析。
- 构建类型：使用 `@types/node` 为 Vite 配置提供 Node.js 类型，仅参与开发与 CI 类型检查
- 测试：待选择单元测试与端到端测试工具
- 部署：待确定静态托管、反向代理和环境变量注入策略
