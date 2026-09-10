# Server Development Harness

## Paths

| Purpose | Repository-relative path |
| --- | --- |
| Repository root | `.` |
| Server root | `src/server` |
| Server source | `src/server/src` |
| Server solution | `src/server/Devkit.Server.slnx` |
| Server development documentation | `src/server/docs` |
| Existing server tests | `src/server/tests` |

Run commands from the repository root unless a tool requires another working directory. Keep server implementation inside `src/server`; cross into Web or Desktop only when a shared HTTP/OpenAPI contract change requires synchronized callers.

Create new server development documents in `src/server/docs` unless the user gives another destination. This default does not authorize moving existing documents.

## Validation

The default server validation command is:

```powershell
dotnet build src/server/Devkit.Server.slnx
```

Do not create, modify, or run tests unless the user explicitly requests testing. An explicitly invoked packaging, release, or CI workflow may run its mandatory tests and is the only default-policy exception.

If restore or build fails, stop, report the failing command and relevant error, and do not claim validation succeeded. In the final report, state that tests were not run when the default policy applied.
