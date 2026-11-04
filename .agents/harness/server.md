# Server Development Harness

## Paths

| Purpose | Repository-relative path |
| --- | --- |
| Repository root | `.` |
| Server root | `server` |
| Server source | `server/src` |
| Server solution | `server/Devkit.Server.slnx` |
| Server development documentation | `server/docs` |
| Existing server tests | `server/tests` |

Run commands from the repository root unless a tool requires another working directory. Keep server implementation inside `server`; cross into Web or Desktop only when a shared HTTP/OpenAPI contract change requires synchronized callers.

Create new server development documents in `server/docs` unless the user gives another destination. This default does not authorize moving existing documents.

## Validation

The server validation commands are:

```powershell
dotnet build server/Devkit.Server.slnx
dotnet test server/Devkit.Server.slnx
```

When adding, changing, or fixing a server endpoint, add or update meaningful API / contract tests and run them. Cover the normal flow, validation, authentication / authorization, and relevant failure paths. Targeted runs are useful during development; run the server solution tests before completing an endpoint change.

Keep tests isolated from real databases, user files, external model credentials, and shared Docker resources. Use dedicated test storage under the configured data root and controlled external-service replacements.

## Default query contract

Every mapped table inherits scoped paging through `IPagedQueryStore` / `PagedDbContext`, including composite keys. Public queries use versioned `.../query` routes with `PageQuery` and `PageResult<T>`; authorize and scope before filtering, count, ordered multi-column sorting and paging. Expose DTO allowlists rather than arbitrary table/type access or credential fields. Web tables must submit filters and ordered sorts to the server. See `server/docs/统一分页查询.md` and the repository / delivery AGENTS.md rules.

If restore, build, or tests fail, report the relevant failure, fix the cause within scope, and rerun the affected checks. Never weaken assertions or skip required checks to hide failures. Report the actual build and test commands and results, and state any unresolved blocker without claiming validation succeeded.
