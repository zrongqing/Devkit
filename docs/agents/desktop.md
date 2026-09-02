# Desktop Agent

**输入：** OpenAPI 契约、WPF 页面需求、`DEVKIT_API_BASE_URL` 配置、`DEVKIT_MENU_CONFIG_URL` 菜单配置。

**产出：** Prism View/ViewModel、可注入服务接口、Syncfusion WPF 界面、菜单注册/合并逻辑、加载/错误状态和构建验证结果。

**边界：** 保持模块导航和 DI 方式；ViewModel 不直接创建网络客户端或保存凭据；客户端不绕过服务端契约访问数据库。

**菜单规则：** 本地 Prism 子模块可注册菜单；远端服务器菜单配置可覆盖相同 ID 的本地菜单；Shell 右侧标签页按菜单配置选择单标签复用或多标签新开。

**完成标准：** ShellWindow 可显示左侧树状菜单和右侧标签页内容；点击菜单能打开或跳转对应页面；服务响应和连接失败可安全显示；受影响解决方案构建通过。
