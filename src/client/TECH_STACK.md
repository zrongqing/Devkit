# Desktop 技术栈

- UI：WPF。
- 应用框架：Prism + DryIoc，保留模块化注册、区域导航和依赖注入模式。
- UI 组件库：Syncfusion WPF，版本由 `DevkitPrism/Directory.Packages.props` 集中管理。
- 菜单：Shell 左侧使用树状菜单；菜单来源支持本地 Prism 子模块注册和远端配置，远端同 ID 配置覆盖本地注册。
- 内容区：Shell 右侧使用标签页承载菜单页面；菜单可配置为单标签复用或多标签新开。
- 网络：当前使用注入式 `HttpClient` 模板；API 地址由 `DEVKIT_API_BASE_URL` 读取。
- 远端菜单配置：可通过 `DEVKIT_MENU_CONFIG_URL` 指定菜单 JSON 地址；未配置时只使用本地菜单。
- 安装包：Inno Setup 6 生成当前用户安装的 Windows x64 EXE；应用采用依赖 .NET 10 Desktop Runtime x64 的框架依赖发布。
- 发布渠道：本地 PowerShell 与 GitHub Actions 共用 `DevkitPrism/packaging/Package-Devkit.ps1`；`main` 自动打包并保存为 Actions Artifact，也支持 `workflow_dispatch` 手动触发。
- 版本：`DevkitPrism/Directory.Build.props` 的 `VersionPrefix` 为基线；CI 追加 `ci.<run_number>` 预发布后缀。
- 配置、日志、自动更新与正式 Release 渠道：待定。安装包首版不签名，也不自动下载运行时。
