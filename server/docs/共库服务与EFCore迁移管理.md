# 共库服务与 EF Core 迁移管理

## 当前决策

现阶段保留 Identity、FileStorage、Business 三个独立服务，共用 SQL Server 中的 `Devkit` 数据库；各自拥有实体、DbContext、表和迁移。该方案降低初期部署成本，保留服务边界，但数据库资源、故障和整库恢复仍会相互影响。

保留 EF Core Migrations。开发期在所有相关数据库均可丢弃重建的前提下，定期将未发布迁移整理为每个 DbContext 一份初始基线，控制文件数量。进入共享测试环境或正式发布后，保留已经应用的迁移，通过增量迁移演进。

不将迁移目录加入 `.gitignore`，不以 API 启动时自动删库或自动迁移代替部署流程，不同时用 `EnsureCreated` 和 Migrations 管理同一个库。

## 服务及数据边界

| 服务 | DbContext | 拥有的表前缀 |
| --- | --- | --- |
| Identity | `IdentityDbContext` | `sys_` |
| FileStorage | `FileStorageDbContext` | `file_` |
| Business | `BusinessDbContext` | `module_`、`study_`、`workspace_` |

每张表只由一个服务负责读写和结构变更。一个 DbContext 只映射自己拥有的表；不复用其他服务的实体，不直接查询或 JOIN 其他服务的表，不建立跨服务数据库外键。服务内部可正常使用关联、外键和本地事务。

跨服务访问通过版本化 HTTP 契约完成。跨服务流程不能假设一个本地 EF 事务能同时回滚远程 HTTP 操作；使用幂等操作、补偿和 outbox 等机制。现有 `workspace_file_release_outbox` 属于 Business，由 Business 记录文件引用释放意图，再调用 FileStorage 完成操作。

表名采用模块前缀、单数、小写下划线，列、索引及约束也使用小写下划线，完整清单见 [数据库与认证](数据库与认证.md)。表前缀仅表达归属，不提供数据库权限隔离。

## 迁移与发布约定

迁移位于各服务 Infrastructure 的 `Persistence/Migrations/SqlServer`。当前继续共用 `__EFMigrationsHistory`，三个上下文的迁移 ID 必须全局唯一；迁移工具按上下文顺序执行，不并发运行。API 启动不执行迁移。

每个上下文的一份基线包含三个文件：主迁移 `.cs`、元数据 `.Designer.cs` 和 `ModelSnapshot.cs`；三个上下文共九个文件。保留 Designer 和快照，它们是 EF 迁移模型的一部分。后续每份增量迁移通常增加两个文件并更新已有快照，不是每次复制一套快照。

在仓库根目录执行建库或应用未执行迁移：

```powershell
./server/scripts/Update-Database.ps1 -Context Identity
./server/scripts/Update-Database.ps1 -Context Business
./server/scripts/Update-Database.ps1 -Context FileStorage
```

连接配置使用统一的 `ConnectionStrings:Devkit`，默认值来自 `server/database.json`；本机凭据放在仓库外的配置文件或环境变量中。脚本支持 `-DatabaseConfigFile` 指定外部配置。普通建库不创建管理员；确需初始化账号时使用 Identity 脚本的 `-SeedBootstrapAccount`，凭据由外部环境变量提供。

## 开发期整理基线

整理基线只适用于尚未发布、所有使用旧迁移的数据库均允许重新建库的阶段。团队中有人依赖现有数据，或迁移已应用到需要保留的共享测试库、生产库时，应保留迁移并增加纠正迁移，不能直接覆盖基线。

1. 停止使用待重建开发库的服务，确认实际服务器、数据库名、配置来源及数据是否可丢弃。只清理文件不会重置数据库中的迁移记录。
2. 确认各 DbContext 只包含所属表。将三个活动迁移目录中现有的迁移、Designer 和快照备份到仓库外，再一起移出活动项目；不要删除 `legacy` 历史实现或其他未提交工作。
3. 根据当前模型为每个上下文重新生成初始迁移，不手写快照。以下命令从仓库根目录运行：

```powershell
foreach ($context in @('Identity', 'Business', 'FileStorage')) {
    dotnet ef migrations add "Initial${context}Context" `
        --context "${context}DbContext" `
        --project "server/src/$context/Devkit.$context.Infrastructure/Devkit.$context.Infrastructure.csproj" `
        --startup-project "server/src/$context/Devkit.$context.Api/Devkit.$context.Api.csproj" `
        --output-dir Persistence/Migrations/SqlServer
    if ($LASTEXITCODE -ne 0) { throw "$context baseline generation failed." }
}
```

4. 审查生成的迁移：三个上下文不得重复创建表，外键不得越过服务边界，种子数据、字段长度、过滤索引及约束应与当前模型一致。初始迁移直接表达新库结构，不保留历史拆表、旧表复用或旧数据修补逻辑。
5. 执行 `dotnet build server/Devkit.Server.slnx` 和 `dotnet test server/Devkit.Server.slnx`。模型命名及迁移测试需确认模型与快照一致、基线表归属正确、迁移 SQL 可生成。
6. 在独立临时 SQL Server 数据库中依次应用三个基线，再重复执行验证无待执行迁移；验证 29 张业务表、外键、索引和菜单状态种子数据。回退迁移时按 FileStorage、Business、Identity 的顺序检查各上下文仅删除自己的表；测试结束删除临时库。
7. 经明确授权后重建需要切换的开发库，使用新迁移建立结构，再由开发者重新初始化所需账号及测试数据。将旧迁移删除、新基线、相关测试和文档一并提交。

开发中的零碎模型调整可以在功能完成时一起生成迁移；每次提交或交付时，模型、快照和迁移必须一致。基线清理不是自动完成的，修改实体不会自动更新现有基线。

不要只清空 `__EFMigrationsHistory` 再运行新基线：已有表仍然存在，新迁移会尝试重复建表。也不要把新基线 ID 直接写入历史表冒充已执行。需要保留数据时另行设计升级方案。

## 本次基线整理（2026-11-02）

| 上下文 | 新迁移 ID |
| --- | --- |
| Identity | `20261102154500_InitialIdentityContext` |
| Business | `20261102154506_InitialBusinessContext` |
| FileStorage | `20261102154512_InitialFileStorageContext` |

活动迁移由原来的四份整理为三份，文件数从十一份减至九份。Business 基线直接创建 `workspace_file_release_outbox`，移除独立的 `ExtractFileStorageContext` 迁移；FileStorage 基线直接创建最终表结构及 `module` 的 450 字符限制，移除旧表接管 SQL 和仅为它服务的公共辅助代码。`legacy` 历史实现不在本次清理范围内。

本次整理不会改写本机 `Devkit` 的数据或迁移记录。此前重建的开发库仍记录旧迁移 ID，不能直接应用这三份新基线；需要在允许清空该库时重新建库。当前 API 可以继续读取已有相同结构的数据，但迁移工具必须等开发库与新基线对齐后再使用。

## 生产前的演进方向

当前共用连接信息尚未体现服务级数据库权限隔离。生产部署前优先为三个服务配置独立运行账号，仅授予所属表必要的读写权限；迁移使用独立的结构变更账号。届时需相应调整配置入口，不能仅凭表前缀认为权限已经隔离。

如果服务需要独立迁移记账，可配置各自的迁移历史表；这是后续设计选项，本次未实施。运行时与设计时工厂必须使用同样的配置，已有历史记录需另行迁移。分开历史表不能消除共享数据库的 DDL 竞争，仍需编排迁移。

当出现独立备份恢复、显著资源竞争、权限合规或不同团队独立发布需求时，再评估独立数据库；可以先共用同一个 SQL Server 实例，之后再按负载拆分实例。

## 参考资料

- [微服务的数据所有权](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/architect-microservice-container-applications/data-sovereignty-per-microservice)
- [EF Core 管理迁移](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing)
- [EF Core 自定义迁移历史表](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/history-table)
- [EnsureCreated 与多个 DbContext 的限制](https://learn.microsoft.com/en-us/ef/core/managing-schemas/ensure-created)
