# Web 技术栈

- UI 组件库：Element Plus 2 与 `@element-plus/icons-vue`，用于后台布局、菜单、页签、表单和状态反馈。
- 表格呈现：`TableToolbar.vue` 统一顶部条件标签 / 刷新 / 业务动作，不提供通用搜索框或筛选按钮，保留列头筛选与直接展示的业务下拉选项，“表格设置”收纳列显示、固定位置、自动适应与重置；底部只显示总数和分页。默认固定首个关键数据列与右侧操作列，窄屏解除固定，列头分割线及筛选 / 排序按钮始终显示，生效状态蓝色高亮并显示排序优先级；显示列偏好保存到 `devkit:table-hidden:v1:*`，兼容既有固定与列宽缓存。表格行默认水平左对齐、垂直居中。
- 列宽：公共 `DataTable` 基于 Element Plus 原生列间边界拖动（含辅助列），禁止拖动最后一列右边界；实际测量列名、按钮与内边距决定最小宽度，标题完整单行显示，过窄缓存自动抬高；自动适应按当前页内容计算，通常上限 480 px，长内容省略并悬浮查看。按表格与列标识缓存到 `devkit:table-widths:v1:*`，重置时恢复默认宽度。表格随容器自适应，列总宽度超出时显示内部横向滚动条，不新增依赖。
- 表格：统一使用 Element Plus 封装 `DataTable.ts` / `TableColumnHeader.vue`，强制通过 `src/api/tableQueries.ts` 服务端分页、组合筛选和多列排序；输入框后图标显式提交所有列草稿，上下三角排序并显示优先级；固定列偏好仍按表格独立缓存。契约与派生视图成本见 `../server/docs/统一分页查询.md`。
- 字段筛选：`TableFilterInput.vue` 根据 `filter-schema` 渲染文本、可搜索多选、数字 / 时间范围；`tableFilters.ts` 统一草稿复制、请求和条件标签。`src/api/queryOptions.ts` 维护编码与显示字典，角色选项来自既有授权角色接口，使用 `roleIds/anyOf` 查询。支持远程选项加载、取消与重试，不新增依赖。规范见 `docs/表格筛选开发规范.md`。
- 路由：Vue Router 4，使用 Hash 模式，避免静态部署必须配置 history fallback。
- 状态管理：Pinia 3；菜单加载状态和页签状态分别管理。
- HTTP 封装：当前使用原生 `fetch`，可替换为团队标准客户端
- 导航接口：运行时由 `GET /api/v1/web/navigation/menus` 返回数据库中的可访问菜单；`routeKey` 必须在本地声明注册器中解析。菜单管理调用封装集中于 `src/api/menuManagement.ts`。
- 构建类型：使用 `@types/node` 为 Vite 配置提供 Node.js 类型，仅参与开发与 CI 类型检查
- 测试：待选择单元测试与端到端测试工具
- 备考业务：`src/modules/exam-study`；文件存储：`src/modules/file-storage`；权限管理：`src/modules/identity`。沿用现有 UI、路由、Pinia，不新增依赖。
- 登录会话：原生 fetch + 单次刷新令牌队列；会话和未提交答案暂存在 sessionStorage。请求及菜单分别检查权限，服务端承担最终访问控制。
- 备考 HTTP 契约：`src/api/examStudy.ts` 对应服务端 `/api/v1/exam-study`、`/files`、`/storage`、`/identity`。部署与使用说明见 `../server/docs/知识库与备考系统.md`。
- 部署：本机执行 `npm run build`，Compose 使用通过 digest 锁定的 Nginx 1.30.5 Alpine 镜像，只读挂载 `dist` 和 `nginx.conf`，同源反向代理 `/api/` 到后端；Nginx 日志使用命名卷持久化。`VITE_API_BASE_URL=/` 在生产构建时写入页面。开发默认 `npm run dev` 使用 `.env.direct` 直连本机三个 API；`npm run dev:gateway` 使用 `.env.gateway` 切换至 Docker/Nginx 网关，自定义地址分别写入 `.env.direct.local`、`.env.gateway.local`。

- 模块导航：项目维护、检索、刷题分路由复用业务组件；用户、角色与权限分页面。权限目录由服务端下发，用户直接授权与角色授权合并，沿用现有依赖。系统监控调用 `/api/v1/system/runtime`。

- 菜单声明：业务目录的 `menu.ts` 由 `import.meta.glob` 自动收集，同时生成页面注册、菜单元数据及业务权限提示。编码与路由保持稳定；组件加载器不上传。菜单权限与业务权限独立，详见 `../server/docs/菜单管理与同步.md`。

- 菜单编码规范：`menuCode` 使用点分小写业务命名空间，各段以字母开头，与展示层级和 `routeKey` 独立。`page(menuCode, routeKey, ...)` 显式保留旧路由；前后端均拒绝混用横线、下划线或空段。
