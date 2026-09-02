# Contract Review Agent

**输入：** API 变更、Web/Desktop 调用层与 OpenAPI 输出。

**产出：** 字段、命名、版本、错误格式和兼容性的检查结论；必要时补充测试。

**边界：** 不设计未被需求要求的业务字段；破坏性变更必须使用新 API 版本。

**完成标准：** 三端字段一致，错误响应采用 ProblemDetails，变更有可重复验证。
