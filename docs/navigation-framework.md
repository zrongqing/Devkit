# Devkit 分端导航框架

## 目标与边界

Devkit 的 Web 和 Desktop 都采用“左侧菜单 + 右侧页签”工作区，但两端通过独立 HTTP 契约演进：

- Web：`GET /api/v1/web/navigation/menus`
- Desktop：`GET /api/v1/client/navigation/menus`

两个端点不互相发起 HTTP 调用。它们调用同一个服务端菜单查询服务，由该服务负责配置合并、隐藏处理、树结构校验和排序，再分别映射为 Web DTO 与 Client DTO。

服务端只能下发语义化目标键，不能下发 Vue 组件路径、CLR 类型名或程序集路径。Web 使用本地 `routeKey` 注册表，Desktop 使用本地 `viewKey` 注册表。这条边界保证配置可以调整菜单展示，但不能远程指定任意可执行代码。

## 数据流

```text
Navigation:Common ─┐
                   ├─ 合并/校验 ─ WebNavigationMenuItemDto ─ Web routeKey 注册表
Navigation:Web ────┘

Navigation:Common ─┐
                   ├─ 合并/校验 ─ ClientNavigationMenuItemDto ─ Desktop viewKey 注册表
Navigation:Client ─┘
```

合并时先读取公共项，再用端侧配置按不区分大小写的 `id` 整项覆盖。端侧也可以新增菜单；将覆盖项设为 `IsVisible: false` 会隐藏该项及其子树。返回结果保持扁平结构，客户端根据 `parentId` 构建任意深度的树。

## 公共约束

- `id` 在每个合并结果中唯一。
- `parentId` 必须引用存在的菜单，菜单树不得成环。
- 有子节点的目录不能配置目标键；叶子页面必须配置目标键。
- 每端都必须有可见的根菜单 `home`，目标键为 `home` 且不可关闭。
- 同级菜单按 `order`、标题和 ID 稳定排序。
- 默认公共菜单提供首页、系统状态、设置和关于；Web 与 Client 可覆盖标题或目标。

## 新模块接入清单

1. 为模块选择稳定、跨版本不变的菜单 ID。
2. 在公共配置或对应端侧配置中添加菜单；父目录与页面叶子分开定义。
3. Web 页面在 `web/src/router/pageRegistry.ts` 注册对应 `routeKey`。
4. Desktop 页面在 `ClientNavigationViewRegistry` 注册对应 `viewKey`，并向 Prism 注册本地视图。
5. 若两端都提供该模块，分别确认两个接口的返回值和两端本地映射。
6. 构建 Server、Web 和 Desktop；不把组件路径或类型名写入服务端配置。

## 延伸文档

- [Web 前端框架](../web/docs/frontend-framework.md)
- [服务端菜单接口](../server/docs/navigation-menu-api.md)
- [Desktop 菜单接入](../client/docs/navigation-client.md)
