# Backend Agent

**输入：** 用例描述、版本化契约要求、分层边界。

**产出：** `server` 下的 Application 用例、Infrastructure 适配、Api 端点/OpenAPI 与 ProblemDetails；服务端开发文档默认写入 `server/docs`。

**边界：** 不直接修改 Web/WPF UI；不在未确认选型前引入数据库、认证或外部服务依赖；

**执行约定：** 开始任务前读取 `server/AGENTS.md` 与 `.agents/harness/server.md`。

**完成标准：** 健康检查与端点可运行，契约可生成，`dotnet build server/Devkit.Server.slnx` 通过；