# Devkit

面向 MES 演进的三端单仓库模板：Vue 3 Web、ASP.NET Core 服务端与 .NET 10 WPF 客户端。

## 目录

- `src/web`：Vue 3 + TypeScript 前端模板。
- `src/server`：.NET 10 分层模块化单体，提供 HTTP API 与 OpenAPI 文档。
- `src/client/DevkitPrism`：现有 WPF/Prism 桌面客户端。
- `docs/agents`：角色协作模板。

## 首个跨端契约

- `GET /health`：服务存活检查。
- `GET /api/v1/system/info`：服务名称、版本、环境和服务端时间。
- `GET /openapi/v1.json`：OpenAPI 描述。

运行服务端：`dotnet run --project src/server/src/Devkit.Server.Api`。Web 端复制 `src/web/.env.example` 为 `.env.local` 后执行 `npm install`、`npm run dev`。桌面端可设置 `DEVKIT_API_BASE_URL` 指向服务端地址。

各端待定技术选型见对应的 `TECH_STACK.md`；贡献规则见根 `AGENTS.md`。
