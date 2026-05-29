# Desktop 协作规则

- `DevkitPrism` 使用 .NET 10、WPF、Prism 和 DryIoc；保留现有模块化导航模式。
- 远程调用只能通过服务接口注入，ViewModel 不直接创建 `HttpClient`。
- API 地址从 `DEVKIT_API_BASE_URL` 读取；未来统一配置方案替换时保持该服务边界。
- 不提交 Syncfusion 许可证或真实服务凭据。
