## Context

`add-rooms-and-gameplay` 交付了完整的对局链路,但 `Room.Leave` 在 Playing 状态下只做"离席"——不判负、不触发 `Game.Result`。Spec 里写着"留给后续变更"。现在这是**所有对局**的唯一重要不变量漏洞:玩家不走棋 → 房间永远卡在 Playing → 永远不 Finished → ELO 永远不结算 → 数据一致性受损。

补两个结束路径:
- **Resign**:调用方主动结束,任何时刻(包括对手回合)。
- **TurnTimeout**:服务端基于"当前回合 start 已过 N 秒"自动判对方胜。

两者共享尾部事务(Game.FinishWith + Status transition + ELO + SignalR);头部不同(身份校验 / 超时计算)。

## Goals / Non-Goals

**Goals**:
- Playing 房间有**至少两条**可靠的结束路径:连五 / 认输 / 超时。
- 客户端知道当前回合何时超时(DTO 里有 `TurnStartedAt` 和 `TurnTimeoutSeconds`),可本地 tick 倒计时 UI。
- 任何时刻,"为啥这局结束了"都有明确答案 —— `Game.EndReason`。
- `MakeMoveCommandHandler` 里重复的 ELO 应用逻辑抽成共享 helper,三条路径共用。

**Non-Goals**:
- 断线与重连(`add-reconnect`)。若玩家掉线但未超时,照常被 timeout judge;重连后仍可继续 —— 但"断线期间时间是否暂停"这类语义本次一概不考虑。
- 每房间 / 每难度不同 timeout(`add-custom-timecontrol`)。
- 双向 5s / 30s pre-timeout 警告事件。
- 服务端推 tick 广播 —— 客户端自算,服务端只在真正超时时推 `GameEnded`。
- Bot "礼貌认输"(e.g. 连 4 必输的局自动认输)。

## Decisions

### D1 — `TurnStartedAt` 从 Moves 历史推算,不新增列

`Game.CurrentTurnStartedAt` 作为**派生字段**:

```
TurnStartedAt = Moves.OrderBy(Ply).LastOrDefault()?.PlayedAt ?? Game.StartedAt
```

理由:
- 避免多一列、多一次 migration;
- Domain / Application / 客户端都能从已有数据(Moves + Game.StartedAt)推出,零歧义;
- 写入路径零冗余(不怕更新漏)。

**代价**:Repository 的 `GetRoomsWithExpiredTurnsAsync` 需在 SQL 里做 `CASE WHEN Moves EXISTS THEN MAX(PlayedAt) ELSE Games.StartedAt END` 比较 —— 能用 EF LINQ 翻译,成本可忽略。

### D2 — Timeout 校验在 Domain 兜底,防 worker 竞态

Worker 的 poll 和 handler 的执行之间有时间窗:
- T0:worker `GetRoomsWithExpiredTurnsAsync` 发现某房间"已超时";
- T0+x:handler 加载 room 去走 `TimeOutCurrentTurn`;
- 这中间若对手 T0+x-ε 时刚好落了一子 → Room.Game 被更新 → `Moves.Last().PlayedAt` 变新 → 不该超时。

`Room.TimeOutCurrentTurn(now, timeoutSeconds)` **重新计算** `now - lastActivity`,若 < threshold 抛 `TurnNotTimedOutException`。Handler 捕获后 log + 吞(worker 下轮重试;实际情况 worker 下轮查出来的就不会包含此房间)。

### D3 — 共享 `GameEloApplier` helper

现 `MakeMoveCommandHandler.ApplyEloAsync` 是 private 方法,含 30 行逻辑(load 双方 User、推导 outcome、调 EloRating.Calculate、各自 RecordGameResult)。

新建 `Features/Rooms/Common/GameEloApplier.cs`:

```csharp
internal static class GameEloApplier
{
    public static async Task ApplyAsync(
        Room room,
        GameResult result,
        IUserRepository users,
        CancellationToken ct);
}
```

三处 handler 都 `await GameEloApplier.ApplyAsync(room, outcome.Result, _users, ct)`。`MakeMoveCommandHandler` 同步重构:把私有方法改为调用静态 helper;行为无变化,保留现有测试。

**考虑过但弃用**:
- 引入 `IGameEndingService` 接口 + DI —— 过度抽象。Static helper 对 handler 的测试 mock 影响为零(测试仍 mock `IUserRepository`)。

### D4 — `GameEndReason` 枚举 + migration

新字段不是 pure addition(要加列 + 回填老数据),所以需要 migration `AddGameEndReason`:

- `ALTER TABLE Games ADD COLUMN EndReason INTEGER NULL;`
- `UPDATE Games SET EndReason = 0 WHERE Result IS NOT NULL;` —— 老 Finished 局一律回填 Connected5(唯一已存在的结束方式)。

枚举底值固定(`Connected5=0 / Resigned=1 / TurnTimeout=2`),为未来 `Surrendered=3` / `Disconnected=4` 留位。

### D5 — Resign 允许任何时刻(包括对手回合)

Gomoku / 围棋 / 国际象棋的业界惯例:**认输不限回合**。玩家可在自己的回合、对手的回合甚至思考阶段认输。技术上现有 `Room.Resign(userId, now)` 不检查 `CurrentTurn` —— 只校验是玩家 + 房间 Playing。

**考虑过但弃用**:"只能在自己回合认输"—— 对 UX 不友好(对手长考时无法认输)。

### D6 — 单一全局 `TurnTimeoutSeconds`

`GameOptions.TurnTimeoutSeconds = 60`,从 `appsettings.json` `"Game"` 段绑定。所有房间共用,**包括 AI 房间**。

- 60s 对人类下 gomoku 是宽松的(典型一手 5–30s),不应过度触发;
- 对 AI 房间更是充裕(bot 1–3s 即答);
- 把"自定义 timecontrol"完全留给后续变更。

### D7 — `TurnTimeoutWorker` 独立,不合并进 `AiMoveWorker`

两者的 poll 频率、查询条件、处理逻辑完全不同:
- `AiMoveWorker`:每 1.5s 查"轮到 bot 走的房间",触发 bot 决策;
- `TurnTimeoutWorker`:每 5s 查"当前回合已超时的房间",触发判负。

把它们合一个 class:代码难读、配置互相耦合、单元测试难写。两个 worker 各司其职。

### D8 — 命令层 / 事件层对称

|路径|Command|Handler 步骤|Notifier 事件|
|---|---|---|---|
|连五|`MakeMoveCommand`|`Room.PlayMove` → ELO → SaveChanges → RoomStateChanged + MoveMade + GameEnded|3 事件|
|认输|`ResignCommand`|`Room.Resign` → ELO → SaveChanges → RoomStateChanged + GameEnded|2 事件(**不发 MoveMade**)|
|超时|`TurnTimeoutCommand`(内部)|`Room.TimeOutCurrentTurn` → ELO → SaveChanges → RoomStateChanged + GameEnded|2 事件|

`GameEnded` 的 payload `GameEndedDto` 现在包含 `EndReason: GameEndReason`,客户端一看即知"为啥结束"。

### D9 — Resign 不需要 body

`POST /api/rooms/{id}/resign` 无 body。调用方(就是当前登录用户)的 UserId 从 JWT `sub` 取出。

**考虑过但弃用**:body 带 reason 字符串("网络不好")—— 非必要,留给聊天频道。

### D10 — TurnTimeoutCommand 是内部命令

`TurnTimeoutCommand` 只被 `TurnTimeoutWorker` 发;**不**暴露 REST 端点、**不**被 SignalR Hub route。这保证"谁的棋到了超时谁就负"不会被客户端随意触发(例如玩家连续发 `TurnTimeoutCommand` 给 SignalR 企图"踢对手 off")。

命令类自身仍是 `public sealed record`(MediatR 需要),但没有任何 Controller / Hub / Authorization 入口。

### D11 — Game.EndReason 在非 Finished 状态下始终 null

- 对局进行中:`EndReason == null`。
- 连五 / 认输 / 超时后:`EndReason` 等于对应枚举值;Nullable 对齐 `Result` / `EndedAt` / `WinnerUserId` 三者。
- 所有现有代码路径(`Room.PlayMove`)显式传入 `Connected5` —— 不让默认值漏进来。

## Risks / Trade-offs

| 风险 | 影响 | 缓解 |
|---|---|---|
| Migration 回填老数据(`EndReason=0 where Result IS NOT NULL`)在 SQL Server 上失败 | 生产迁移中断 | migration 同时在 SQLite(dev)+ SQL Server 上均写原生 SQL 回填(EF migration 的 `migrationBuilder.Sql` 支持);测试 pipeline 含 migration apply |
| Worker 每 5s 扫全 `Games` 表,生产量大时 I/O 上升 | 性能 | 加索引 `Games.RoomId + Ply`(已有)+ `Rooms.Status`(已有);查询写成 "Rooms 表 Where Status=Playing + 子查询取最后 Move" —— SQL 优化器能用索引;量超百房后再优化 |
| 竞态(worker 发 TimeOut、玩家同时 Resign)导致双路径并发 | 一者 DbUpdateConcurrencyException | `Game.RowVersion` 已配 `IsConcurrencyToken` —— 后写者抛;handler 通过全局中间件映射 409;前端视 409 为"已经结束了,重新拉 state" |
| 时区:`_clock.UtcNow` 在 worker 和 handler 里用同一 `IDateTimeProvider` | 跨进程一致 | 本服务单进程,`IDateTimeProvider.UtcNow` 来自 `SystemDateTimeProvider`(wraps `DateTime.UtcNow`),无漂移风险 |
| Resign 与 TurnTimeout 的 EndReason 混乱(用户在超时一刻前点 Resign,worker 恰好触发) | 少见;二者都合法,谁先 SaveChanges 谁赢 | 依赖 RowVersion;后者收 DbUpdateConcurrencyException → 409,丢弃;Game 最终有且仅有一个 EndReason |
| DTO 追加字段 `EndReason` / `TurnStartedAt` / `TurnTimeoutSeconds` 破坏前端 | 向后兼容 | 追加而非重命名,严格向后兼容;未来前端需显式消费才能体现 |
| `TurnTimeoutCommand` 泄漏为 public endpoint(将来有人误加 controller) | 被恶意利用踢人 | design 明示"内部命令 + 不路由 Hub";code review 守住 |

## Migration Plan

- `AddGameEndReason` migration:
  1. `ALTER TABLE Games ADD COLUMN EndReason INTEGER NULL`
  2. `UPDATE Games SET EndReason = 0 WHERE Result IS NOT NULL`(SQLite 和 SQL Server 语法都兼容)
- 启动时 `Database.Migrate()` 自动应用。
- 回滚:标准 EF migration remove 流程;SQLite 因不支持 `DROP COLUMN` 会触发"表重建"—— EF 自动处理,回滚在 dev 可用,生产不走 rollback。

## Open Questions

无 —— 本次的两个"默认值"选择(60s timeout、5s poll)已在 proposal / design 固化,且通过 `GameOptions` 完全可配置。
