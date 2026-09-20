# Desktop 菜单接入

## 接口选择

Desktop 默认使用 `DEVKIT_API_BASE_URL` 作为服务端基地址，并请求：

```text
GET /api/v1/client/navigation/menus
```

未设置基地址时，开发默认值为 `http://localhost:12511/`。

`DEVKIT_MENU_CONFIG_URL` 仍可覆盖完整菜单地址，适合兼容独立配置服务。默认接口和覆盖地址都必须返回：

```json
{
  "data": [],
  "traceId": "..."
}
```

其中 `data` 元素使用 Client 契约，页面目标字段为 `viewKey`。

## 安全视图映射

`RemoteMenuConfigurationClient` 不把服务端字符串当作 CLR 类型或程序集路径。它通过 `ClientNavigationViewRegistry` 将允许的 `viewKey` 映射为本地 Prism 导航名：

| viewKey | Prism 视图 |
| --- | --- |
| `home` | `HomeView` |
| `system-status` | `SystemStatusView` |
| `settings` | `SettingView` |
| `about` | `AboutView` |

未知键统一映射到 `UnavailableView`。菜单和页签仍然可见，但内容会提示当前客户端未部署对应页面。

## 覆盖和本地兜底

- 服务端先组合 `Navigation:Common` 与 `Navigation:Client`。
- Desktop 将远端菜单注册为 Remote；同 ID 远端项覆盖本地项。
- 本地 `home` 在远端不可用时仍可打开，保证 Shell 有一个不可关闭的首页。
- 动态 Prism 模块仍可注册自己的本地菜单，但服务端返回的同 ID 配置优先。

## 新增 Client 页面

1. 创建 View 和 ViewModel，并启用 Prism ViewModel 自动绑定。
2. 在 `App.RegisterTypes` 中用固定导航名注册 View。
3. 在 `ClientNavigationViewRegistry` 中增加 `viewKey` 到该导航名的映射。
4. 在 `Navigation:Common` 或 `Navigation:Client` 增加菜单配置。
5. 执行 `dotnet build client/DevkitPrism/DevkitPrism.slnx`。

不得在菜单 JSON 中放置 CLR 类型名、DLL 路径或需要反射加载的类型信息。
