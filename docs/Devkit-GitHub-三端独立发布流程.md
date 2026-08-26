---
title: Devkit GitHub 三端独立发布流程
tags:
  - GitHub Actions
  - CI/CD
  - WPF
  - Vue 3
  - ASP.NET Core
categories:
  - DevOps
  - GitHub Actions
keywords:
  - GitHub Actions
  - Monorepo
  - WPF 发布
  - Vue 3 发布
  - ASP.NET Core 发布
description: 基于 Devkit 的 WPF、Vue 3 与 ASP.NET Core 三端结构，设计单仓库、独立版本、独立构建和独立部署的 GitHub 发布流程。
abbrlink: f4e463dd
toc: true
aside: true
highlight_shrink: true
disableNunjucks: true
date: 2026-08-26 23:35:00
updated: 2026-08-26 23:35:00
---

Devkit 同时包含 WPF 客户端、Vue 3 Web 前端和 ASP.NET Core 服务端。三端放在一个 GitHub 仓库没有问题，而且以当前项目的跨端契约协作方式来看，继续使用单仓库更合适。需要拆开的不是代码仓库，而是三端的构建、版本、发布产物和部署权限。

本文基于 Devkit 当前工程结构给出一套可逐步落地的 GitHub Actions 方案，并回答两个核心问题：三端是否应该共用仓库，以及如何在共用仓库的前提下独立发布。

<!-- more -->

## 结论先行

推荐采用下面的策略：

- 保留一个仓库和一个受保护的长期分支 `main`。
- 功能开发使用短生命周期分支，例如 `feature/client-*`、`feature/web-*` 和 `feature/server-*`。
- `paths` 负责决定普通 CI 需要构建哪些目录，但不负责确定正式版本。
- `client-v1.2.0`、`web-v1.5.0`、`server-v2.1.0` 这类组件标签负责触发独立正式发布。
- WPF 安装包发布到 GitHub Releases；Web 发布静态文件或 Web 容器；服务端发布 Docker 镜像并部署到目标环境。
- 使用 GitHub Environments 隔离 `staging`、`production-web` 和 `production-server` 的密钥、审批与并发部署。
- API 契约变更必须额外验证 Web 和 WPF，不能只因为文件位于 `src/server` 就仅构建服务端。

一句话概括：**分支表示代码协作过程，目录表示变更范围，标签表示不可变版本，Environment 表示部署目标。**

## Devkit 当前状态

仓库已经具备典型 Monorepo 的目录边界：

```text
Devkit/
├─ .github/workflows/
├─ src/
│  ├─ client/                # .NET 10、WPF、Prism、DryIoc
│  ├─ web/                   # Vue 3、TypeScript、Vite
│  └─ server/                # .NET 10、ASP.NET Core
├─ build/                    # 本地构建输出，不提交
├─ compose.yaml
├─ global.json
└─ NuGet.config
```

三个交付物当前的发布准备程度并不相同：

| 交付物 | 已有能力 | 当前缺口 | 推荐发布物 |
| --- | --- | --- | --- |
| WPF 客户端 | 已有 `Package-Devkit.ps1`、Inno Setup 和 `client-package.yml`；能够生成 EXE 安装包与 SHA-256 | Actions Artifact 只保留 14 天；尚未创建正式 GitHub Release；安装包未签名 | GitHub Release 中的安装器、校验文件和发布说明 |
| Vue 3 Web | 已有 Vite `build` 脚本和 `0.1.0` 版本 | 没有依赖锁文件、CI、发布工作流和明确托管目标 | 版本化静态压缩包，或部署到静态托管/CDN，或 Web 容器 |
| ASP.NET Core 服务端 | 已有 .NET 10 解决方案、测试、Dockerfile 和 Compose 服务 | 没有镜像发布、环境部署和独立版本源 | GHCR 中的不可变 Docker 镜像，再部署到服务器或集群 |

项目已经把版本化 HTTP API 和 `/openapi/v1.json` 作为跨端契约，这正是保留单仓库的重要理由：一次接口变更可以在同一个 Pull Request 中同步修改服务端、Web 调用端、WPF 调用端和契约测试。

## 三端放在同一个仓库是否正确

### 当前阶段：正确

Devkit 的三端属于同一个产品，接口契约紧密，技术团队需要一起评审跨端改动。单仓库在这些方面更有优势：

- 一个 Pull Request 可以原子化完成服务端契约与两个调用端的同步修改。
- Issue、里程碑、代码评审和发布记录集中，不必跨仓库追踪同一需求。
- 可以复用 `global.json`、工程规范、契约测试和 GitHub Actions 模板。
- 三端虽然共仓，但目录已隔离，并没有直接共享跨语言业务源码。

“放在同一个仓库”不等于“必须一起发布”。只要为每个目录建立独立工作流、版本和产物，三端完全可以拥有不同发布节奏。

### 什么时候再考虑拆仓

满足下列多项条件时，再评估拆成三个仓库：

- 三端由不同团队维护，权限、合规或代码可见性必须强隔离。
- 三端已经成为可独立销售或被多个产品复用的产品线。
- 仓库体积和 CI 时间明显影响日常开发，路径过滤与缓存也无法改善。
- 客户端自动更新、Web 发布页和服务端镜像需要完全独立的 Release 门户与权限模型。
- 跨端契约已通过独立的 OpenAPI 包、Schema Registry 或 SDK 仓库稳定治理。

如果只是“想让三端分别上线”，没有必要为此拆仓。

## 为什么不建议用三条长期分支发布三端

不建议把 `dev_client`、`dev_web`、`dev_server` 当成三个长期发布分支。这样会让同一个产品形成三份逐渐漂移的历史：服务端契约可能只存在于一个分支，前端修复可能长期拿不到客户端分支中的公共配置，合并冲突也会持续累积。

推荐的分支职责如下：

| 类型 | 示例 | 生命周期 | 用途 |
| --- | --- | --- | --- |
| 主分支 | `main` | 长期 | 始终保持可构建、可发布，设置保护规则 |
| 功能分支 | `feature/client-auto-update` | 短期 | 完成功能并通过 PR 合入 `main` |
| 修复分支 | `fix/web-login-timeout` | 短期 | 修复后通过 PR 合入 `main` |
| 紧急修复 | `hotfix/server-auth` | 短期 | 从生产版本对应提交创建，修复后回到 `main` |
| 发布稳定分支 | `release/2026.09` | 可选、短期 | 只有确实存在冻结与验收窗口时才使用 |

GitHub Release 本身基于 Git tag，它把某个确定提交标记为可交付版本。因此正式发布应使用组件标签，而不是依靠“某分支当前正好指向哪里”。GitHub 官方也将 Release 定义为基于标签的软件迭代，参见 [About releases](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases)。

## 独立发布的触发规则

建议为三端采用独立版本号：

| 组件 | 标签格式 | 示例 | 触发结果 |
| --- | --- | --- | --- |
| 客户端 | `client-v<SemVer>` | `client-v0.2.0` | 构建、测试、打包并创建客户端 GitHub Release |
| Web | `web-v<SemVer>` | `web-v0.4.1` | 构建静态资源，并部署 Web 或发布 Web 镜像 |
| 服务端 | `server-v<SemVer>` | `server-v1.3.0` | 测试并发布服务端 Docker 镜像 |
| 整套产品 | `suite-v<版本>` | `suite-v2026.09.0` | 可选；需要三端一起形成验收基线时使用 |

普通提交和正式发布使用不同触发条件：

| 场景 | 推荐触发 | 说明 |
| --- | --- | --- |
| Pull Request | `pull_request` + 变更检测 | 构建受影响组件；契约变更额外构建三端 |
| 合并到 `main` | `push` + `paths` | 生成 CI 产物或部署开发环境，不产生正式版本 |
| 正式发布 | `push.tags` | 标签唯一确定版本和源代码提交 |
| 补跑或测试 | `workflow_dispatch` | 允许手动构建，但默认不冒充正式 Release |

GitHub 的 `branches` 与 `paths` 同时存在时，两个条件必须同时满足；标签和路径过滤也有各自的匹配规则，详见 [Workflow syntax for GitHub Actions](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax)。

还要注意：如果把一个带 `paths` 的工作流设为 PR 必需检查，它在路径不匹配时可能保持 `Pending` 并阻塞合并。GitHub 在 [Skipping workflow runs](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/skip-workflow-runs) 中明确说明了这一行为。实践中可以设置一个始终运行的聚合 CI 检查，再由它判断哪些组件任务需要执行；不要简单地把三个可能被路径过滤掉的工作流全部设为必需检查。

## 推荐的发布链路

```text
短期功能分支
  → Pull Request
  → 受影响组件 CI + 跨端契约检查
  → 合入受保护的 main
  → 创建某个组件标签
  → 构建一次不可变产物
  → 发布到 Release / GHCR / 静态托管
  → staging 验证
  → production 审批与部署
  → 冒烟检查与回滚记录
```

关键原则是“构建一次，逐级晋级”。同一个版本不要在测试环境和生产环境各重新构建一遍，否则即使标签相同，实际字节也可能不同。

## WPF 客户端发布

Devkit 已有的 `.github/workflows/client-package.yml` 适合做 `main` 分支的持续集成打包。它生成带 `ci.<run_number>` 后缀的预发布版本，并把安装器保存为短期 Artifact。正式发布可以新增 `client-release.yml`，只监听客户端标签并复用现有 PowerShell 打包入口：

```yaml
name: Release Devkit Client

on:
  push:
    tags:
      - 'client-v*'

permissions:
  contents: write

concurrency:
  group: client-release-${{ github.ref }}
  cancel-in-progress: false

jobs:
  release:
    runs-on: windows-2025
    timeout-minutes: 30

    steps:
      - uses: actions/checkout@v6

      - uses: actions/setup-dotnet@v5
        with:
          global-json-file: global.json

      - name: Build installer
        shell: pwsh
        env:
          RELEASE_TAG: ${{ github.ref_name }}
        run: |
          $version = $env:RELEASE_TAG -replace '^client-v', ''
          if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
            throw "Invalid client release tag: $env:RELEASE_TAG"
          }

          ./src/client/DevkitPrism/packaging/Package-Devkit.ps1 -Version $version

      - name: Create GitHub Release
        shell: pwsh
        env:
          GH_TOKEN: ${{ github.token }}
          RELEASE_TAG: ${{ github.ref_name }}
        run: |
          $assets = Get-ChildItem ./build/client/package -File |
            Where-Object { $_.Name -match '\.(exe|sha256)$' } |
            Select-Object -ExpandProperty FullName

          if ($assets.Count -ne 2) {
            throw "Expected installer and checksum, found $($assets.Count) files."
          }

          gh release create $env:RELEASE_TAG @assets `
            --verify-tag `
            --generate-notes `
            --title "Devkit Client $($env:RELEASE_TAG -replace '^client-v', '')"
```

发布标签的操作示例：

```powershell
git switch main
git pull --ff-only
git tag -a client-v0.2.0 -m "Release Devkit Client 0.2.0"
git push origin client-v0.2.0
```

客户端还需要补齐两项生产能力：

1. 使用受保护的证书或云签名服务签名应用和安装器，并加入可信时间戳。证书和密码只能放在受保护的 Secret 或外部密钥服务，不能提交到仓库。
2. 自动更新不能直接依赖仓库全局的 `/releases/latest`，因为同一个 Monorepo 的 Web 或 Server Release 也可能成为“最新”。应按 `client-v*` 查询，或维护客户端专用更新清单。

## Vue 3 Web 发布

### 发布前先固定依赖

`src/web` 当前没有 `package-lock.json`。CI 不能在每次发布时重新解析一组可能变化的依赖，应先在 Web 目录生成并提交锁文件，然后统一使用 `npm ci`。`actions/setup-node` 官方说明也建议提交包管理器锁文件，以提高安全性和可重复性，参见 [setup-node](https://github.com/actions/setup-node#checking-in-lockfiles)。

建议同时在 `package.json` 或 `.nvmrc` 固定 Node.js 主版本，使本地和 Actions 使用同一运行时。

### 先生成不可变 Web 产物

下面的工作流会在 `web-v*` 标签上构建并保存版本化静态包：

```yaml
name: Release Devkit Web

on:
  push:
    tags:
      - 'web-v*'

permissions:
  contents: read

jobs:
  build:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: src/web

    steps:
      - uses: actions/checkout@v6

      - uses: actions/setup-node@v7
        with:
          node-version: '22'
          cache: npm
          cache-dependency-path: src/web/package-lock.json

      - run: npm ci
      - run: npm run build

      - name: Package static files
        env:
          RELEASE_TAG: ${{ github.ref_name }}
        run: tar -czf "../../devkit-web-${RELEASE_TAG#web-v}.tar.gz" -C dist .

      - uses: actions/upload-artifact@v7
        with:
          name: devkit-web-${{ github.ref_name }}
          path: devkit-web-*.tar.gz
          if-no-files-found: error
```

这段示例只完成可追溯构建，还没有假定生产平台。确定基础设施后，再增加一个引用 `production-web` Environment 的部署 Job，将同一压缩包发布到以下任一目标：

- Nginx 或 IIS 静态目录；
- 对象存储加 CDN；
- 支持静态站点的平台；
- 增加 Web Dockerfile 后发布到 GHCR，再由容器平台部署。

对于需要登录、调用后端并使用前端路由的业务系统，通常优先选择 Nginx/IIS、对象存储/CDN或 Web 容器。若使用 GitHub Pages，需要额外处理 Vite 的 `base`、Vue Router history 回退、API 跨域以及仓库可见性，不应仅因为“代码在 GitHub”就默认选择 Pages。

`VITE_API_BASE_URL` 会在构建时写入前端产物。为了做到真正的“同一产物从 staging 晋级到 production”，更推荐使用同域 `/api` 反向代理或运行时配置文件；否则不同环境需要分别构建，难以证明生产包与已验收包完全一致。

## ASP.NET Core 服务端发布

服务端已经有 Dockerfile，适合把镜像发布到 GitHub Container Registry。下面是核心工作流模板：

```yaml
name: Release Devkit Server

on:
  push:
    tags:
      - 'server-v*'

permissions:
  contents: read
  packages: write

jobs:
  test-and-push:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v6

      - uses: actions/setup-dotnet@v5
        with:
          global-json-file: global.json

      - name: Test server
        run: dotnet test src/server/Devkit.Server.slnx --configuration Release

      - name: Resolve image metadata
        shell: bash
        env:
          RELEASE_TAG: ${{ github.ref_name }}
        run: |
          version="${RELEASE_TAG#server-v}"
          owner="${GITHUB_REPOSITORY_OWNER,,}"
          echo "VERSION=$version" >> "$GITHUB_ENV"
          echo "IMAGE=ghcr.io/$owner/devkit-server" >> "$GITHUB_ENV"

      - name: Log in to GHCR
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build and push image
        uses: docker/build-push-action@v7
        with:
          context: .
          file: src/server/src/Devkit.Server.Api/Dockerfile
          push: true
          tags: |
            ${{ env.IMAGE }}:${{ env.VERSION }}
            ${{ env.IMAGE }}:sha-${{ github.sha }}
```

GitHub 官方的 [Publishing Docker images](https://docs.github.com/en/actions/tutorials/publish-packages/publish-docker-images) 文档说明了使用 `GITHUB_TOKEN`、`packages: write` 和 GHCR 的基本方式。生产环境最好记录并部署镜像 digest，而不只依赖可移动标签；回滚时重新部署上一版本 digest 即可。

为便于阅读，上面的示例使用了 Action 主版本标签。正式工作流应按 GitHub 的安全建议把第三方 Action 固定到完整 commit SHA，并使用 Dependabot 或 Renovate 审核后更新。

镜像发布和服务部署是两个动作。上面的流程把镜像推入 GHCR，但真正部署仍取决于目标环境，例如 Docker Compose 主机、Kubernetes、Azure App Service 或其他容器平台。目标尚未确定时，不应在工作流中虚构 SSH 地址或服务器命令。

## 跨端契约发布顺序

独立发布不代表忽略兼容性。Devkit 当前使用 `/api/v1`，推荐遵循下面的上线顺序：

1. 服务端先以向后兼容方式增加字段或新端点，并保留旧契约。
2. Web 发布并切换到新能力。
3. WPF 客户端发布；考虑到桌面用户不会同时升级，服务端必须继续兼容旧客户端。
4. 通过遥测或版本统计确认旧客户端已低于可接受比例后，再安排废弃旧接口。
5. 破坏性变更使用 `/api/v2`，不要直接改变 `/api/v1` 的含义。

路径过滤也要反映这种依赖关系：

| 变更路径 | 至少应执行的验证 |
| --- | --- |
| `src/client/**` | 客户端构建与测试 |
| `src/web/**` | Web 类型检查与构建 |
| `src/server/**` 的内部实现 | 服务端构建与测试 |
| OpenAPI、DTO、API 路径或认证契约 | 服务端测试 + Web 构建/契约测试 + WPF 构建/契约测试 |
| `global.json`、共享 CI 脚本、根配置 | 根据影响范围构建两端 .NET 或全部三端 |

## GitHub 仓库设置建议

### 保护 `main` 和发布标签

在 GitHub 的 **Settings → Rules → Rulesets** 中建议设置：

- `main` 必须通过 Pull Request 合并；禁止强推和删除。
- 要求聚合 CI 检查通过，至少一人评审；团队规模为一人时可先只要求 CI。
- 对 `client-v*`、`web-v*` 和 `server-v*` 创建标签规则，限制删除、覆盖和非授权创建。
- 自动删除已合并的短期功能分支，减少分支噪声。

Rulesets 可以同时保护分支和标签，并限制更新或删除，参见 [About rulesets](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/about-rulesets)。

### 建立部署环境

建议至少建立：

- `staging-web`
- `production-web`
- `staging-server`
- `production-server`
- `production-client`（接入代码签名后使用）

生产 Environment 应限制允许部署的标签、设置并发组，并在团队和 GitHub 套餐支持时要求审批。环境级 Secret 只有引用该 Environment 的 Job 才能访问；如果设置了审批，Secret 在审批通过前不会提供给 Job。具体能力和套餐限制见 [Deployments and environments](https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments)。

## 当前项目应优先修正的问题

按优先级建议处理以下事项：

1. **恢复 NuGet TLS 校验。** 当前 `NuGet.config` 对 `nuget.org` 配置了 `allowInsecureConnections` 和 `disableTLSCertificateValidation`。正式 CI 不应关闭包源 TLS 校验，应查明本机证书或代理问题后移除这两个设置。
2. **提交 Web 锁文件。** 没有 `package-lock.json` 就无法稳定使用 `npm ci`，同一标签在不同时间可能解析出不同依赖。
3. **收紧 Docker 构建上下文。** 当前服务端 Dockerfile 从仓库根目录 `COPY . .`，`.dockerignore` 又没有排除 `.env`、证书、`build/`、客户端和 Web 目录。本地未跟踪文件也可能被发送给 Docker daemon；应改为按需复制或扩大忽略规则。
4. **给服务端增加唯一版本源。** 可以在 `src/server/Directory.Build.props` 增加 `VersionPrefix`，发布时由 `server-v*` 覆盖，确保程序集、镜像和发布记录版本一致。
5. **把客户端正式版从 Artifact 升级为 Release。** Artifact 适合 CI 验证，GitHub Release 更适合长期下载、发布说明和版本追踪。GitHub 支持在 Release 中附加二进制资产，参见 [Managing releases](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)。
6. **补客户端签名和更新清单。** 当前安装包未签名，正式分发前应解决 Windows 信任与升级通道。
7. **确定 Web 与 Server 的真实生产平台。** 在此之前先完成可重复构建和 GHCR/Artifact 发布，不要把“产物已生成”误认为“生产已部署”。

## 分阶段落地计划

### 第一阶段：先让三个组件都可重复构建

- 保留并优化现有客户端打包工作流。
- 为 Web 提交锁文件，增加 build 工作流。
- 为 Server 增加测试和 Docker build 工作流。
- 建立始终运行的 PR 聚合检查，正确处理路径过滤。

### 第二阶段：建立独立正式版本

- 新增 `client-v*`、`web-v*`、`server-v*` 标签规范。
- 客户端标签创建 GitHub Release。
- Web 标签生成不可变静态包或容器镜像。
- Server 标签推送 GHCR，并记录镜像 digest。

### 第三阶段：接入环境部署与回滚

- 选择 Web 静态托管或容器平台。
- 选择 Server 的 Compose、Kubernetes 或托管平台。
- 使用 GitHub Environments 管理生产审批与密钥。
- 为 Web 保留上一版静态产物，为 Server 保留上一版 digest，为 Client 保留历史安装器。
- 增加部署后健康检查、版本检查和失败自动停止，不自动删除上一版本。

## 最终建议

Devkit 目前不需要拆成三个仓库。它真正需要的是在现有 Monorepo 中建立三条独立交付流水线：客户端以安装器和 GitHub Release 为中心，Web 以不可变静态产物或容器为中心，服务端以 GHCR 镜像和环境部署为中心。

不要用三条长期分支区分三端。让所有经过评审的代码回到 `main`，用目录范围控制 CI，用组件前缀标签控制正式发布，用 GitHub Environments 控制部署目标和权限。这样既保留跨端契约在一个 PR 中同步演进的优势，也能让 WPF、Vue 3 和 ASP.NET Core 按各自节奏安全发布。
