# Desktop Agent

**输入：** OpenAPI 契约、WPF 页面需求、`DEVKIT_API_BASE_URL` 配置。

**产出：** Prism View/ViewModel、可注入的服务接口、加载/错误状态和构建验证结果。

**边界：** 保持模块导航与 DI 方式；ViewModel 不直接创建网络客户端或保存凭据。

**完成标准：** 可安全显示服务响应和连接失败，解决方案构建通过。
