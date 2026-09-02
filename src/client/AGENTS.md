# Desktop 协作规则

- `DevkitPrism` 使用 .NET 10、WPF、Prism、DryIoc 和 Syncfusion WPF；保持现有模块化、View/ViewModel 和 DI 模式。
- ShellWindow 采用左侧树状菜单、右侧标签页内容区的经典桌面布局。
- 菜单来源包括本地 Prism 子模块注册和远端服务器配置；遇到相同菜单 ID 时，远端配置优先覆盖本地注册。
- 菜单项通过配置决定是否允许多标签：单标签模式点击菜单复用/跳转到已有标签，多标签模式每次点击打开新标签。
- 远程调用只能通过服务接口注入，ViewModel 不直接创建 `HttpClient`。
- API 地址从 `DEVKIT_API_BASE_URL` 读取；远端菜单配置地址从 `DEVKIT_MENU_CONFIG_URL` 读取。
- 不提交 Syncfusion 许可证、密钥、真实服务凭据或本机配置。
