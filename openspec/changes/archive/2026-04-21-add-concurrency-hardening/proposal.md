## Why

`add-elo-system` 和 `add-timeout-resign` 把 `User.RecordGameResult` 变成了**多条路径**都会触碰的写入点:

- 连五结束 → `MakeMoveCommandHandler` 调 `GameEloApplier`
- 认输 → `ResignCommandHandler` 调 `GameEloApplier`
- 超时 → `TurnTimeoutCommandHandler` 调 `GameEloApplier`

同一用户若**同时**是两盘几乎同刻结束的对局的参与者(典型场景:轮询 `TurnTimeoutWorker` 触发若干超时判负,同一位 Alice 同时是其中两盘的黑方),两条事务并发 SaveChanges → 两次加载同一 `User` 聚合 → **后写者覆盖先写者**,丢失一次战绩增量。

这是 `add-elo-system` design.md 明确留给本次的坑:

> **User 乐观并发保护** —— Rating 并发更新在单聚合极少触发(一个用户罕见同时参与两盘结束),本次接受"后写覆盖先写"的极小窗口;`add-concurrency-hardening` 时再加 `IConcurrencyToken`。

现在这个窗口因为 timeout worker 的加入变大了(worker 可能一轮扫出多盘同时超时)。补一条乐观并发令牌,让第二个 SaveChanges 拿到 `DbUpdateConcurrencyException`,通过全局中间件已就位的 409 映射反馈;由上层(worker 捕获 / handler 重试 / HTTP 客户端重试)决定重试。

顺手验收一下 `Game.RowVersion`(在 `add-rooms-and-gameplay` 就已加过)—— 确认两聚合风格一致,**都走 Domain 自管 byte[] + IsConcurrencyToken** 的 SQLite 兼容方案。

## What Changes

- **Domain**:
  - `User` 聚合加 `byte[] RowVersion`(默认 `Guid.NewGuid().ToByteArray()`,与 `Game.RowVersion` 同模式),只读属性。
  - 私有方法 `TouchRowVersion() => RowVersion = Guid.NewGuid().ToByteArray();`
  - **唯一**在 `RecordGameResult` 末尾调用 `TouchRowVersion()` —— 战绩写入是唯一需要并发保护的路径。
    - `IssueRefreshToken` / `RevokeRefreshToken` / `RevokeAllRefreshTokens` **MUST NOT** 调 `TouchRowVersion`。Refresh token 是子集合 INSERT / 子行字段更新,不改 User 父行的业务属性,并发场景(并发登录、并发登出)本身无冲突,加保护反而把无关的登录流程串行化。
- **Application**:
  - 零改动。`MakeMoveCommandHandler` / `ResignCommandHandler` / `TurnTimeoutCommandHandler` / `GameEloApplier` 都已把 `DbUpdateConcurrencyException` 的处理交给全局中间件。
- **Infrastructure**:
  - `UserConfiguration`:`builder.Property(u => u.RowVersion).IsConcurrencyToken().IsRequired();`
  - Migration `AddUserRowVersion`:
    - `ALTER TABLE Users ADD COLUMN RowVersion BLOB NOT NULL DEFAULT (randomblob(16));`(SQLite)
    - 对已存在的行给一次随机 blob,避免全部 NULL 或全部同值导致"任何更新都冲突"。
  - `SQL Server` 将来迁移到生产时,若要换成原生 `rowversion` 列,是**另一个变更**的事(`migrate-to-sqlserver-rowversion`),本次不涉及。
- **Api**:
  - 零改动。全局中间件已把 `DbUpdateConcurrencyException` → 409 + `ProblemDetails` 映射(见 `add-rooms-and-gameplay` 的 spec)。
- **Tests**:
  - `Gomoku.Domain.Tests/Users/UserRowVersionTests.cs`(~4):
    - `User.Register` / `RegisterBot` 生成的 User 有非空 RowVersion 且两次创建不等
    - `RecordGameResult(Win, 1220)` 改变 RowVersion
    - `IssueRefreshToken` 不改 RowVersion(反向不变量)
    - 多次 RecordGameResult 每次都生成新 RowVersion
  - **不**加集成测试模拟并发 SaveChanges 导致 `DbUpdateConcurrencyException`—— EF 行为由 Microsoft 保证,测试它是低价值高脆性。手工 smoke 验证一次 HTTP 响应 409 足够。

**显式不做**(留给后续变更):
- 把 SQLite 的 `byte[] RowVersion` 换成 SQL Server 原生 `rowversion` 列 —— 要在生产迁移时做。
- 在 handler 层做**自动重试**(3 次回退指数退避):并发写罕见,重试反而掩盖真实问题;让上层(客户端 / worker 下轮)自然重试即可。
- 把 `RefreshToken` 子实体也加并发令牌 —— 当前 DDD 设计只通过 User 聚合操作子集合,且并发登录互不冲突,加保护无价值。
- `Room` 聚合级并发(比如并发 JoinAsSpectator / LeaveAsSpectator)—— 现状是"后写赢",行为无感知冲突;Room 聚合的关键不变量(玩家 / Game 状态)已由 `Game.RowVersion` 保护。

## Capabilities

### New Capabilities

(无)

### Modified Capabilities

- **`user-management`** — `User` 聚合新增 `RowVersion` 字段与乐观并发保护语义;并发 `RecordGameResult` 保证"后写者收到 `DbUpdateConcurrencyException`"而非静默覆盖。
- **`room-and-gameplay`** — 现有 `DbUpdateConcurrencyException → HTTP 409` 映射的约束面**扩展**到 User 聚合冲突(除 Room/Game 并发外,也覆盖 User 并发);`ProblemDetails.type` 沿用 `concurrent-modification`。

## Impact

- **代码规模**:~3 新文件(测试 + migration)+ 少量现有文件修改。这次比 `add-dissolve-room` 还小。
- **NuGet**:零。
- **HTTP 表面**:零新端点。现有 409 响应面覆盖面扩大。
- **SignalR 表面**:零变化。
- **数据库**:`Users` 表多 1 列 `RowVersion`(BLOB NOT NULL,dev SQLite 默认 `randomblob(16)`)。
- **运行时**:每次 `RecordGameResult` 后 `User` 的 UPDATE 语句多一个 `WHERE RowVersion = @p` 条件 —— EF 生成,0 代码开销。
- **后续变更将依赖**:`migrate-to-sqlserver-rowversion`(若切 SQL Server)、`add-handler-retry`(若观测到并发频繁,再加重试层)。
