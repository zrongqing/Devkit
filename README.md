# Devkit

## 客户端打包

执行 `pwsh ./client/DevkitPrism/packaging/Package-Devkit.ps1` 可完成 Release 构建、完整测试，并在 `build/client/package` 生成 Windows x64 EXE 安装包和 SHA-256。详细参数和安装方式见 [客户端打包文档](docs/client/packaging.md)；工作流基础、CI 打包和正式发布的区别见 [GitHub Actions Workflows 入门](docs/client/github-workflows.md)。

面向 MES 演进的三端单仓库模板：Vue 3 Web、ASP.NET Core 服务端与 .NET 10 WPF 客户端。

## 目录

- `web`：Vue 3 + TypeScript 前端模板。
- `server`：.NET 10 分层模块化单体，提供 HTTP API 与 OpenAPI 文档。
- `client/DevkitPrism`：现有 WPF/Prism 桌面客户端。
- `.agents`：角色协作模板、仓库级 Skills 与 Harness 定义。
- `.agents/skills/server-development`：服务端开发 Skill，可通过 `$server-development` 显式调用，也可按任务描述自动触发。
- `.agents/harness/server.md`：服务端目录、文档位置与默认验证方式。

## 首个跨端契约

- `GET /health`：服务存活检查。
- `GET /api/v1/system/info`：服务名称、版本、环境和服务端时间。
- `GET /openapi/v1.json`：OpenAPI 描述。

运行服务端：`dotnet run --project server/src/Devkit.Server.Api`。Web 端复制 `web/.env.example` 为 `.env.local` 后执行 `npm install`、`npm run dev`。桌面端可设置 `DEVKIT_API_BASE_URL` 指向服务端地址。

各端待定技术选型见对应的 `TECH_STACK.md`；贡献规则见根 `AGENTS.md`。

## 导航框架文档

- [分端导航总体架构](docs/navigation-framework.md)
- [Web 前端框架](web/docs/frontend-framework.md)
- [服务端菜单接口与配置](server/docs/navigation-menu-api.md)
- [Desktop 菜单接入](client/docs/navigation-client.md)
