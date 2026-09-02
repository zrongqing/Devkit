# Devkit 客户端打包与安装

`Package-Devkit.ps1` 是本地和 GitHub Actions 共用的唯一客户端打包入口。它会按顺序还原依赖、执行 Release 构建和完整测试、发布 Windows x64 应用、汇总动态模块，并使用 Inno Setup 生成安装器。

## 环境要求

- Windows 10/11 x64。
- 仓库 `global.json` 指定的 .NET SDK。
- Windows PowerShell 5.1 或 PowerShell 7（`pwsh`）。
- Inno Setup 6。脚本会依次检查显式参数、`PATH`、`C:\Program Files (x86)\Inno Setup 6` 和 `C:\Program Files\Inno Setup 6`。
- 可访问 NuGet 源。网络或源不可用时还原失败，流程立即终止且不会留下本次安装包。

安装器采用框架依赖发布。目标电脑必须先安装 [.NET 10 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/10.0/runtime)，无需安装完整 SDK。安装器会在复制文件前检查运行时；缺失时中止并提供微软下载页面。

## 本地打包

从仓库根目录执行：

```powershell
pwsh ./src/client/DevkitPrism/packaging/Package-Devkit.ps1
```

默认读取 `src/client/DevkitPrism/Directory.Build.props` 中的 `VersionPrefix`，当前为 `0.0.1`。默认产物为：

```text
build/client/package/Devkit-Setup-0.1.0-win-x64.exe
build/client/package/Devkit-Setup-0.1.0-win-x64.exe.sha256
```

指定完整语义版本：

```powershell
pwsh ./src/client/DevkitPrism/packaging/Package-Devkit.ps1 -Version 0.2.0
```

在基线版本后追加预发布后缀：

```powershell
pwsh ./src/client/DevkitPrism/packaging/Package-Devkit.ps1 -VersionSuffix preview.1
```

指定输出目录或 Inno Setup 编译器：

```powershell
pwsh ./src/client/DevkitPrism/packaging/Package-Devkit.ps1 `
  -OutputDirectory C:\Temp\DevkitPackage `
  -InnoCompilerPath 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

`-Version` 与 `-VersionSuffix` 不能同时使用。脚本固定执行 `Release`、`win-x64` 和框架依赖发布，也不提供跳过测试的参数。

## 打包内容

临时 staging 位于 `build/client/package-staging/app`，只用于构建，不应提交。安装器包含：

- `Devkit.exe`、应用依赖和 `appsettings.json`。
- `modules/Devkit.Modules.Demo`。
- `modules/Devkit.Modules.ModuleName`。
- `modules/Devkit.Modules.Barcode2`。

脚本会拒绝缺少主程序、配置或混入测试程序集的 staging，并移除 PDB 和非 Windows 运行时资源。

## GitHub Actions

工作流文件为：

- `.github/workflows/client-package.yml`：持续集成打包。
- `.github/workflows/client-release.yml`：正式版本发布。

如果不熟悉 Workflow、Job、Runner、Artifact、Tag 或 Release，请先阅读 [GitHub Actions Workflows 入门与 Devkit 客户端流程](./github-workflows.md)。

### 自动打包

每次提交或 Pull Request 合并结果推送到 `main` 后自动执行。版本格式为：

```text
<VersionPrefix>-ci.<github.run_number>
```

例如 `0.1.0-ci.42`。同一 ref 的较旧运行会在新运行开始时取消。

### 手动打包

1. 打开仓库的 **Actions** 页面。
2. 选择 **Package Devkit Client**。
3. 点击 **Run workflow** 并选择要构建的 ref。
4. 可选填写完整语义版本；留空时仍使用 CI 运行编号后缀。

手动触发要求工作流文件已存在于默认分支。也可以使用 GitHub CLI：

```powershell
gh workflow run client-package.yml -f version=0.2.0
```

### 正式发布

正式发布由 `client-release.yml` 在推送版本标签时触发。标签必须使用 `v<主版本>.<次版本>.<修订版本>` 格式，标签中的版本必须与 `src/client/DevkitPrism/Directory.Build.props` 中的 `VersionPrefix` 一致，并且标签指向的提交必须已经包含在 `main` 中。

例如发布 `0.2.0`：

```powershell
git switch main
git pull --ff-only
git tag -a v0.2.0 -m "Devkit v0.2.0"
git push origin v0.2.0
```

工作流会执行与持续集成相同的完整打包入口。构建、测试或安装器生成失败时不会创建 Release；全部成功后创建对应的 GitHub Release，自动生成发布说明，并附加 EXE 安装器和 SHA-256 文件。Release 工作流仅为创建 Release 授予 `contents: write`，无需额外配置个人访问令牌。

### 下载产物

`client-package.yml` 成功运行后，Summary 页面包含以安装器文件名命名的 Artifact，保留 14 天，其中包括安装器和 SHA-256 文件。该工作流只授予 `contents: read`，不会创建标签或 GitHub Release。

`client-release.yml` 成功运行后，安装器和 SHA-256 文件位于仓库的 **Releases** 页面，不受 Actions Artifact 的 14 天保留期限制。发布工作流不会自行创建或移动标签，只接受已经推送且通过校验的版本标签。

下载后可校验：

```powershell
$expected = (Get-Content ./Devkit-Setup-0.2.0-win-x64.exe.sha256).Split(' ')[0]
$actual = (Get-FileHash ./Devkit-Setup-0.2.0-win-x64.exe -Algorithm SHA256).Hash.ToLowerInvariant()
$actual -eq $expected
```

## 安装与卸载

安装器按当前用户安装到 `%LOCALAPPDATA%\Programs\Devkit`，不要求管理员权限。默认创建开始菜单快捷方式，桌面快捷方式为可选项。

交互安装：

```powershell
./Devkit-Setup-0.1.0-win-x64.exe
```

静默安装到指定目录：

```powershell
./Devkit-Setup-0.1.0-win-x64.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOICONS /DIR=C:\Temp\Devkit
```

静默卸载：

```powershell
C:\Temp\Devkit\unins000.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

安装器当前未签名，Windows SmartScreen 可能显示未知发布者提示。仓库不得提交代码签名证书、密码或其他凭据；后续接入签名时应使用受保护的 CI Secret。

## 故障排查

- **找不到 `ISCC.exe`**：安装 Inno Setup 6，加入 PATH，或传入 `-InnoCompilerPath`。
- **版本无效**：使用 `主版本.次版本.修订版本`，可附加 SemVer 预发布或构建后缀。
- **正式发布未触发**：确认标签使用 `v1.2.3` 格式，并且是在 Release 工作流已合并到 `main` 后推送。
- **正式发布版本不匹配**：标签去掉 `v` 后必须与 `Directory.Build.props` 中的 `VersionPrefix` 完全一致。
- **正式发布标签不在 `main`**：只对已经包含在 `main` 中的提交创建标签并推送。
- **NuGet/服务器失败**：检查网络与 `NuGet.config` 中的源；还原失败不会生成安装包。
- **测试失败**：修复或确认失败原因后重新运行。打包入口不会重试或跳过测试。
- **缺少模块**：确认三个模块项目均在解决方案内，且模块构建输出仍遵循 `Modules/Module.Build.props`。
- **安装器提示缺少运行时**：安装 .NET 10 Desktop Runtime x64 后重新运行安装器。
- **应用无法访问服务端**：安装包不会写入真实服务连接；按客户端约定配置 `DEVKIT_API_BASE_URL` 和 `DEVKIT_MENU_CONFIG_URL`。
