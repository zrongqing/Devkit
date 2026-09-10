---
name: server-development
description: Develop, review, or document the Devkit ASP.NET Core server under src/server, including versioned HTTP and OpenAPI contract changes. Use for server code, architecture, endpoints, configuration, and server development documentation; do not use for standalone Web or Desktop work.
---

# Server Development

Before working, read the repository `AGENTS.md`, `src/server/AGENTS.md`, and `.agents/harness/server.md`. Treat those files as the authoritative project constraints and execution environment.

- Keep service implementation under `src/server`; use `src/server/src` for source code.
- Write new server development documentation to `src/server/docs` unless the user specifies another location.
- When an HTTP or OpenAPI contract changes, update the affected Web and Desktop callers in the same change.
- By default, do not create, modify, or run tests. Run tests only when the user explicitly requests them or when an explicitly invoked packaging, release, or CI workflow has mandatory test steps.
- Use the build command and failure handling defined in the server harness.
