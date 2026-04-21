## Context

两处现有场景会对同一 `User` 并发写:
1. **Timeout worker 一轮扫出多盘**:同一位用户同时是两盘的黑方(两盘都在他回合超时),worker 按顺序发两条 `TurnTimeoutCommand`,它们的 handler 在 `GameEloApplier` 里都会加载同一 User → 两次 `RecordGameResult` → 最后 SaveChanges 一者覆盖另一者的战绩变动。
2. **AI worker + TimeoutWorker 交汇**:AI 房间里,bot 恰好走了一手同时超时 worker 又判某个人胜 —— 虽然不同 user(bot 和真人),但若该人同时在其它房间作为 Black/White 也在 Finished,两条路径都改同一 User → 同样问题。

两种情况都极少见,但频次会随在线量上升;架构纯度要求先把护栏装上。

这一变更只动 `User` 聚合的乐观并发。`Game.RowVersion` 已由 `add-rooms-and-gameplay` 装上,从一开始就保护了 Room 内部。本次对称补齐 User。

## Goals / Non-Goals

**Goals**:
- `RecordGameResult` 写入路径对并发提供硬保护 —— 后写者收到 `DbUpdateConcurrencyException`。
- 与 `Game.RowVersion` 一致的实现方式(`byte[]` + Domain 自管 + SQLite 兼容)。
- 零行为变化 for 非并发路径(单用户单 handler 一次写入仍然成功)。

**Non-Goals**:
- 迁移到 SQL Server 的原生 `rowversion` 列。
- handler / worker 层的自动重试。
- RefreshToken 子实体的并发保护。
- Room 聚合除 Game 外的并发(Spectators / ChatMessages 等)。

## Decisions

### D1 — `byte[]` + `Guid.NewGuid().ToByteArray()`,Domain 自管

与 `Game.RowVersion` 同步。理由:
- SQLite 无原生 `rowversion` / `timestamp` 列,官方建议 Domain 自管(EF doc)。
- `Guid.NewGuid()` 是 cryptographically random 16 字节,碰撞概率 ~ 2^-122,对乐观锁足够;
- 统一两聚合的实现风格,将来若有第三个聚合(`Tournament` / `Match`)也按同模式加。

**考虑过但弃用**:
- 用 `long Version`,每次 +1 —— 简单,但两个事务同时读到 v=5,各自 +1=6,第二个 SaveChanges 的 `WHERE Version=5` 仍成立,**不能检测**并发。要正确,需要 ROWVERSION 式自增写入,SQLite 不支持。
- 改用 `Timestamp` `DateTime` —— 精度与时钟漂移问题,弃。

### D2 — `TouchRowVersion` 仅在 `RecordGameResult` 调

`User` 聚合的其它写入方法(`IssueRefreshToken` / `RevokeRefreshToken` / `RevokeAllRefreshTokens`)都**只操作子集合 `_refreshTokens`**:

- IssueRefreshToken → INSERT 一个 RefreshToken 行
- RevokeRefreshToken → UPDATE 一个 RefreshToken 行的 RevokedAt 字段
- RevokeAllRefreshTokens → UPDATE 多个 RefreshToken 行的 RevokedAt

这些操作**不改 User 父行**,EF 也就不触发 User 的 UPDATE 语句(因此 `IsConcurrencyToken` 的 WHERE 条件也不会被添加),所以不会误抛 `DbUpdateConcurrencyException`。

若我们错误地在 IssueRefreshToken 里调 `TouchRowVersion()`:
- 新增一行并触发 User UPDATE(无意义);
- 把"并发登录 → 两人同时发 token"从零冲突场景变成 N-1 个 409;
- 单元测试看起来没坏,但线上登录体验劣化。

所以严格规则:**凡改 User 业务属性(Rating / GamesPlayed / Wins / Losses / Draws)的路径调 TouchRowVersion;凡只改子集合的不调。**

当前唯一匹配前半句的只有 `RecordGameResult`。未来若加新方法(例如 `ChangePassword`),按同规则决定。

### D3 — Migration 的默认值 `randomblob(16)`

SQLite 支持 `DEFAULT (randomblob(16))`,每行填 16 字节随机。

对老数据:
- 老 Users 表可能已有若干行(从 `InitialIdentity` + `AddBotSupport` seed 起至少 2 行 bot);
- `ALTER TABLE ... ADD COLUMN RowVersion BLOB NOT NULL DEFAULT (randomblob(16))` 会为现有行一次性填值;
- 每行各自独立的随机值(非同值),避免"任何更新都冲突"的灾难。

**考虑过但弃用**:
- 迁移 Up 里写 `UPDATE Users SET RowVersion = randomblob(16)`:多此一举,`ALTER ... DEFAULT` 已生效。
- 用 `Guid.NewGuid()` 在 C# 端为每行显式生成:意味着 migration 要做成 data migration,不能纯 DDL;复杂度不必。

### D4 — HTTP 409 复用现有映射

`DbUpdateConcurrencyException` → 409 的映射是 `add-rooms-and-gameplay` 装的:

```csharp
DbUpdateConcurrencyException => (
    (int)HttpStatusCode.Conflict,
    "Concurrent modification.",
    "The room state changed concurrently; reload and retry."),
```

本次扩展其覆盖面为 "room state OR user state"。`detail` 文案略嫌 room-specific,但语义方向("并发修改,重拉 + 重试")对两者都适用。**不**为 User 并发加独立异常类 —— EF 抛的就是同一个 `DbUpdateConcurrencyException`,分叉处理要在中间件里 `ex.Entries` 判实体类型,不值这点细粒度。

## Risks / Trade-offs

| 风险 | 影响 | 缓解 |
|---|---|---|
| Timeout worker + AI worker 两者都在高频时,同一用户的并发冲突变多 | 用户视角 409 频繁 | 目前观测无负载;真发生再加 handler 层退避重试(`add-handler-retry`) |
| Migration 对已有 dev 库的 bot seed 行冲突 | `RowVersion` 列 DEFAULT 为 randomblob,不与任何既有列冲突 | 无需额外动作 |
| 测试环境 SaveChanges 频繁失败干扰 xUnit | 本次**不加**集成并发测试;Domain 单测只断 `RowVersion` 变化事实 | N/A |
| 未来切 SQL Server 时 `BLOB` 类型不直接等价 `rowversion` | 需要独立变更(重建列为 `rowversion` + drop DEFAULT) | 归档时在 proposal Non-Goals 里已说明,属跨变更协调 |

## Migration Plan

- `AddUserRowVersion` migration:
  1. `migrationBuilder.AddColumn<byte[]>("RowVersion", "Users", nullable: false, defaultValueSql: "randomblob(16)");`
- Down:`migrationBuilder.DropColumn("RowVersion", "Users");`
- 本地 SQLite 直接 `database update` 应用;老行的 bot + 真人用户都拿到各自随机的 16 字节。

## Open Questions

无。
