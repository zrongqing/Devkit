# MES 模块化服务与可靠消息框架

## 1. 目标与结论

Devkit 服务端以 `Devkit.Server.Api` 作为稳定运行的核心服务，身份、模块控制面、契约入口和可靠命令暂存属于核心能力。生产执行、工艺、质量、设备、物料、仓储、追溯、报工和 ERP/PLC 集成等业务能力应根据边界拆成独立进程或容器。

业务模块不是动态加载到核心进程中的 DLL。模块由 Docker Compose、Kubernetes、Windows Service、systemd 或其他进程编排器独立启动、停止和重启。因此，单个业务模块的上下线不要求重启核心服务，也不会把模块自身的内存泄漏、线程阻塞或依赖故障传播到核心进程。

模块不可用期间的请求不能使用普通内存缓存，也不能只依赖 Redis 缓存。本框架把这类请求建模为“持久化异步命令”：核心服务先写入 SQL Server，立即返回命令 ID；目标模块恢复并重新注册后，按照目标模块内的序号逐条领取、处理和确认。

当前提交实现的是第一阶段可运行基础框架：

- 模块实例注册、心跳、计划下线状态和离线判定。
- SQL Server 持久化命令队列。
- 每个目标模块严格顺序发放。
- 幂等键、处理租约、崩溃后重新投递、重试和死信状态。
- 受独立模块密钥保护的版本化内部 HTTP API。
- EF Core 模型与 SQL Server migration。

业务模块的进程启动/停止动作由部署编排器负责，核心服务只维护控制面状态，不直接执行远程进程终止命令。

## 2. 运行拓扑

```text
                    Web / Desktop / 外部系统
                              |
                              v
                 +---------------------------+
                 | Devkit 核心服务           |
                 | - 身份与版本化 API         |
                 | - 模块注册与心跳           |
                 | - 持久化命令缓冲           |
                 +-------------+-------------+
                               |
                         SQL Server
               模块状态、命令、序号、租约、审计
                               |
            +------------------+------------------+
            |                  |                  |
            v                  v                  v
      生产执行服务        质量管理服务        设备集成服务
      独立进程/容器        独立进程/容器        独立进程/容器
```

Redis 继续用于可丢失、可重建的查询缓存。待执行命令、处理结果、模块注册信息不能只保存在 Redis 中。

## 3. 为什么不采用进程内插件热卸载

.NET 可以通过 `AssemblyLoadContext` 实现有限的程序集卸载，但它不等于可靠的生产服务热替换。静态引用、后台线程、事件订阅、DI 单例、非托管资源和第三方库都可能阻止卸载；插件故障也仍然与主服务共享进程、GC、线程池和崩溃边界。

MES 需要长时间连续运行，并且设备、产线和集成模块的稳定性差异较大，因此使用独立服务边界更符合目标：

- 独立发布和回滚。
- 独立健康检查、资源限制与扩缩容。
- 单模块崩溃不会终止核心服务。
- 可以逐步采用不同的数据存储和通信实现。
- 模块重启期间，核心服务仍能接收异步命令。

不要把所有业务拆成细碎的微服务。首轮建议按照可以独立发布、具有明确数据所有权且确实需要独立生命周期的 MES 领域拆分。例如生产执行、质量、设备集成和外部系统集成可以成为候选边界；同一事务中高度耦合的小功能应保留在一个服务内。

## 4. 核心数据模型

### 4.1 ModuleInstances

记录一个业务模块的运行实例：

- `ModuleKey`：稳定的模块标识，例如 `production-execution`。
- `InstanceId`：单次部署实例标识，例如容器或节点 ID。
- `Version`：模块版本。
- `BaseAddress`：诊断和后续路由所需的地址；当前拉取模式不依赖核心服务主动调用该地址。
- `State`：`Running`、`Draining` 或 `Stopped`。
- `LastHeartbeatAtUtc`：最后心跳时间。

实例处于 `Running` 且心跳未超过 `OfflineAfterSeconds` 时才允许领取命令。心跳超时不会删除注册信息，而是把有效状态计算为 `offline`。

### 4.2 BufferedModuleCommands

记录需要可靠交付给目标模块的命令：

- `SourceModule`、`TargetModule`：来源和目标模块。
- `Sequence`：目标模块范围内严格递增的顺序号。
- `CommandType`、`SchemaVersion`：稳定消息类型和版本。
- `Payload`：JSON 负载。
- `IdempotencyKey`：调用方生成的业务幂等键。
- `Status`：`Pending`、`Leased`、`Succeeded` 或 `DeadLetter`。
- `LeaseId`、`LeasedByInstanceId`、`LeaseExpiresAtUtc`：处理租约。
- `DeliveryAttempts`、`LastError`：重试诊断信息。

`ModuleCommandSequences` 为每个目标模块分配严格递增的 `Sequence`。序号分配和命令写入位于同一串行化数据库事务中。

### 4.3 一致性边界

当前实现提供“至少一次”交付，不宣称分布式“恰好一次”。下面的故障窗口无法仅靠 HTTP 消除：目标模块已经提交本地业务事务，但在确认核心服务之前崩溃。租约过期后，同一命令会再次投递。

因此，每个业务模块必须维护本地 Inbox 表，并在处理命令的同一数据库事务内：

1. 检查 `CommandId` 或 `IdempotencyKey` 是否已处理。
2. 未处理时执行本地业务变更。
3. 写入 Inbox 处理记录。
4. 提交事务后调用核心服务的 complete 接口。

重复投递时，模块根据 Inbox 直接返回成功，再次确认命令。不要把幂等判断只放在进程内存中。

## 5. 命令生命周期

```text
调用方提交
    |
    v
 Pending --领取--> Leased --确认--> Succeeded
    ^                 |
    |                 +--可重试失败/租约超时--+
    |
    +-----------------------------------------+

 Leased --不可重试失败--> DeadLetter
```

完整流程：

1. 来源模块生成不会因 HTTP 重试而变化的 `IdempotencyKey`。
2. 核心服务验证 JSON 和大小限制，在 SQL Server 中分配序号并保存命令。
3. API 返回 HTTP 202 和 `CommandId`。这表示已经可靠接收，不表示目标业务已经完成。
4. 在线目标实例领取下一条命令并获得 `LeaseId`。
5. 目标模块在本地事务中使用 Inbox 去重并处理业务。
6. 成功后携带 `LeaseId` 确认；可恢复失败则指定延迟重试；不可恢复失败进入死信。
7. 模块在处理过程中崩溃时，租约过期后命令会重新投递。

为了满足“恢复后依次执行”，当前实现对每个 `TargetModule` 使用全局顺序：前一条命令处于等待重试或有效租约中时，后一条不会被领取。`DeadLetter` 被视为人工确认的终态，不再阻塞后续命令。

全局顺序会限制单模块吞吐量。以后确有并行需求时，应增加 `PartitionKey`，按产线、设备或工单分区保序；相同分区串行，不同分区并行。切换 Kafka 等消息基础设施时也应沿用相同分区语义。

## 6. 模块上下线行为

### 6.1 启动或重启

1. 编排器启动模块进程。
2. 模块使用固定 `ModuleKey` 和新的或稳定的 `InstanceId` 注册。
3. 模块每 10 秒左右发送心跳；默认 30 秒无心跳视为离线。
4. 模块开始循环领取命令。

重新注册同一个 `(ModuleKey, InstanceId)` 会刷新版本、地址和启动时间，不要求核心服务重启。

### 6.2 计划下线

1. 先把实例状态设置为 `draining`。
2. 核心服务停止向该实例发放新命令。
3. 等待已领取命令完成或租约结束。
4. 把实例状态设置为 `stopped`。
5. 由编排器停止进程。

核心服务在整个过程中继续接受发往该模块的新命令，命令保持 `Pending`，等待后续实例上线。

### 6.3 非计划崩溃

- 心跳超时后实例显示为 `offline`。
- 未领取命令仍保存在 SQL Server。
- 已领取但未确认的命令在租约过期后重新投递。
- 新实例上线并注册后从最早的未完成序号继续处理。

## 7. 内部 HTTP 契约

统一前缀为 `/api/v1/internal/modules`。这些接口面向服务进程，不面向浏览器或桌面客户端。

| 方法与路径 | 用途 |
| --- | --- |
| `GET /instances` | 查询模块实例和有效状态 |
| `PUT /{moduleKey}/instances/{instanceId}` | 注册或重新注册实例 |
| `POST /{moduleKey}/instances/{instanceId}/heartbeat` | 发送心跳 |
| `PUT /{moduleKey}/instances/{instanceId}/state` | 设置 running、draining、stopped |
| `POST /commands` | 持久化提交跨模块命令 |
| `GET /commands/{commandId}` | 查询命令状态 |
| `POST /{moduleKey}/instances/{instanceId}/commands/lease` | 领取下一条命令 |
| `POST /{moduleKey}/instances/{instanceId}/commands/{commandId}/complete` | 确认成功 |
| `POST /{moduleKey}/instances/{instanceId}/commands/{commandId}/fail` | 延迟重试或进入死信 |

每个请求必须携带：

```http
X-Devkit-Module-Key: <secret>
```

提交命令示例：

```json
{
  "sourceModule": "production-execution",
  "targetModule": "quality-management",
  "commandType": "inspection.create",
  "schemaVersion": 1,
  "payload": {
    "workOrderId": "...",
    "sampleId": "..."
  },
  "idempotencyKey": "inspection:work-order-123:sample-7",
  "correlationId": "trace-or-business-flow-id",
  "availableAtUtc": null
}
```

`payload` 在 HTTP 契约中是普通 JSON 值，核心服务将其原样持久化，但不引用或解释各业务模块的类型。业务模块必须按 `CommandType + SchemaVersion` 反序列化。

内部契约不会添加到 Web 或 Desktop 调用模板，因为把模块密钥分发到终端会破坏信任边界。面向用户的模块管理页面应在后续增加单独的管理员 API，由核心服务在内部调用 `IModuleRuntimeService`。

## 8. 配置与启用

默认配置保持关闭，避免未配置服务凭据时意外暴露内部控制面：

```json
{
  "ModuleControl": {
    "Enabled": false,
    "OfflineAfterSeconds": 30,
    "DefaultLeaseSeconds": 60,
    "MaximumLeaseSeconds": 300,
    "MaximumPayloadBytes": 262144
  }
}
```

开发环境通过 User Secrets 启用：

```powershell
dotnet user-secrets set "ModuleControl:Enabled" "true" `
  --project server/src/Devkit.Server.Api/Devkit.Server.Api.csproj

dotnet user-secrets set "ModuleControl:ApiKey" "replace-with-at-least-32-random-bytes" `
  --project server/src/Devkit.Server.Api/Devkit.Server.Api.csproj
```

生产环境使用密钥管理系统或环境变量 `ModuleControl__ApiKey`，不得写入仓库。第一阶段共享密钥适合受控内网接入；正式跨节点生产部署应升级到每模块独立身份的 mTLS 或 OAuth2 client credentials，并按 `ModuleKey` 授权，不能把共享密钥作为最终安全方案。

数据库迁移文件为 `ModuleControlPlane`。部署前按既有流程运行：

```powershell
server/scripts/Update-Database.ps1 `
  -ConnectionString "<production connection string>"
```

API 启动过程不会自动迁移数据库。

## 9. 代码位置与扩展方式

核心框架代码按现有四层维护：

```text
Devkit.Server.Domain/
  Modules/                         模块实例、命令、状态实体
  Abstractions/IModuleRuntimeStore 持久化边界

Devkit.Server.Application/
  Modules/ModuleRuntimeService     注册、校验、状态和命令用例
  Contracts/Modules/               与 HTTP 无关的应用 DTO

Devkit.Server.Infrastructure/
  Modules/SqlModuleRuntimeStore    SQL Server 实现
  Persistence/                     EF 映射、DbSet 和 migration
  Configuration/                   模块控制配置

Devkit.Server.Api/
  Endpoints/ModuleControlEndpoints 内部版本化 API
  Contracts/ModuleControlContracts HTTP 请求契约
```

独立 MES 业务服务建议仍放在 `server/src`，但各自拥有进程入口和四层边界，例如：

```text
server/src/Services/ProductionExecution/
  Devkit.ProductionExecution.Api/
  Devkit.ProductionExecution.Application/
  Devkit.ProductionExecution.Domain/
  Devkit.ProductionExecution.Infrastructure/

server/src/Services/QualityManagement/
  Devkit.QualityManagement.Api/
  Devkit.QualityManagement.Application/
  Devkit.QualityManagement.Domain/
  Devkit.QualityManagement.Infrastructure/
```

不同服务不共享业务实体或 DbContext。跨服务事实来源是版本化 HTTP/OpenAPI 契约和版本化消息契约。可复用的模块接入 SDK 只能包含注册、心跳、租约、确认和 Inbox 基础设施，不能包含生产、质量等领域模型。

## 10. MES 事务设计原则

- 设备控制、停线、放行等实时高风险指令不能依赖无限期离线缓存。命令必须携带有效期、设备状态前置条件和人工补偿规则；过期命令应拒绝或进入死信。
- 查询使用同步 HTTP 时，目标模块离线应快速返回 `503 Service Unavailable`，不能让用户请求长时间占用连接。只有允许延迟执行的写操作才进入持久化命令队列。
- 不跨服务共享数据库事务。来源服务采用 Outbox，目标服务采用 Inbox，业务补偿使用 Saga/流程状态机。
- 消息契约只做向后兼容演进。字段增加使用可选字段；破坏性变化提升 `SchemaVersion`，并在旧消息清空前同时支持新旧版本。
- PLC 或设备指令必须使用设备侧可验证的幂等号。仅在服务器 Inbox 去重不足以覆盖“指令已到设备但确认丢失”的故障窗口。

## 11. 运维指标与后续阶段

上线前应补齐以下指标和告警：

- 每个模块最后心跳距今时间。
- `Pending` 数量和最老命令等待时长。
- 有效/过期租约数量。
- 重试次数分布和 `DeadLetter` 数量。
- 各命令类型的处理耗时和成功率。
- SQL Server 队列事务耗时、锁等待和数据库可用性。

建议迭代顺序：

1. 为第一个真实业务模块建立接入 SDK 和本地 Inbox。
2. 增加管理员只读 API 与模块/死信监控界面。
3. 增加管理员审计下的死信重放、跳过和人工补偿流程。
4. 增加 Outbox，保证来源模块本地业务提交和命令发布的一致性。
5. 根据压测结果决定是否引入 Kafka、RabbitMQ 或云消息服务；不要仅因“微服务”标签提前引入。
6. 从共享模块密钥升级到每模块独立机器身份和细粒度授权。
7. 增加 `PartitionKey`，在保持同产线/设备/工单顺序的前提下横向扩展吞吐。

SQL 队列适合作为第一阶段和中等吞吐场景，能够复用已有 SQL Server、减少运维组件并提供清晰事务语义。如果命令量、保留周期、跨工厂复制或消费组需求显著增长，再将命令队列抽象替换为专业消息系统；模块契约、幂等、租约/确认语义和业务 Inbox 仍应保留。
