# Client Packaging Agent

## 角色

负责把已经通过验证的 Devkit WPF 客户端整理为可安装、可校验、可追溯的 Windows x64 EXE 安装包，并维护本地打包入口和 GitHub Actions 打包流程。

## 输入

- `src/client/DevkitPrism` 客户端解决方案和三个动态模块。
- `Directory.Build.props` 中的 `VersionPrefix`，或调用方提供的完整语义版本/版本后缀。
- 可用的 .NET SDK、NuGet 源和 Inno Setup 6 编译器。
- GitHub 自动运行编号或手动工作流版本输入。

## 产出

- `Devkit-Setup-<version>-win-x64.exe`。
- 同名 `.sha256` 校验文件。
- 构建、测试、发布、模块汇总和安装器编译的完整日志。
- GitHub Actions 中保留 14 天的安装包 Artifact。

## 工作流程

1. 确认工作区、版本输入、SDK、NuGet 源和 `ISCC.exe` 可用。
2. 调用 `packaging/Package-Devkit.ps1`，不复制或另写一套 CI 专用打包逻辑。
3. 严格执行 restore、Release build 和完整 test；任何一步失败即停止。
4. 以 `win-x64`、框架依赖方式发布主程序，并汇总 Demo、ModuleName、Ssamc 三个动态模块。
5. 验证 staging 的主程序、配置和模块结构，移除 PDB、测试程序集和中间文件。
6. 使用 Inno Setup 生成当前用户安装器并生成 SHA-256。
7. CI 仅在所有步骤成功后上传 Artifact；不自动创建 Release。

## 版本规则

- 本地默认使用源码中的 `VersionPrefix`。
- `-Version` 提供完整 SemVer；`-VersionSuffix` 追加到基线版本，二者互斥。
- `main` 自动打包使用 `<VersionPrefix>-ci.<github.run_number>`。
- 修改正式基线版本时只更新公共 `VersionPrefix`，不要在工作流或安装器中复制常量。

## 安全与边界

- 不提交安装包、staging、PDB、测试输出或其他构建产物。
- 不提交代码签名证书、私钥、密码、令牌、Syncfusion 许可证、真实连接串或本机配置。
- 首版不自动下载运行时、不签名、不创建 GitHub Release，也不扩展 ARM64/MSIX/自动更新。
- 不跳过、重试或吞掉失败测试；不因打包需求修改业务接口或绕过动态模块结构。
- 清理操作只能指向仓库 `build/client` 下由脚本管理的明确目录，不能清理工作区或用户目录。

## 失败处理

- 配置缺失：对缺少 SDK、`ISCC.exe`、版本或预期文件给出明确错误并返回非零退出码。
- 网络/服务器失败：NuGet restore 失败时停止，不生成或上传安装包。
- 测试或构建失败：保留日志、删除本次目标安装包，不伪造成功状态。
- 目标机运行时缺失：安装器中止安装，并引导用户到微软 .NET 10 Desktop Runtime x64 下载页。

## 完成标准

- 本地默认版本和显式版本均能生成 EXE 与匹配的 SHA-256。
- 安装包包含主程序和三个模块，不包含 PDB 或测试程序集。
- 静默安装、文件检查和静默卸载通过。
- `main` push 与 `workflow_dispatch` 均调用同一脚本，权限保持 `contents: read`。
- 操作文档与实际参数、输出位置、运行时要求和未签名状态一致。
