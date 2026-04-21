## Why

现在一旦房间进入 `Playing`,**对局只能靠连五结束**。若一方静默离开 / 断网 / 故意拖延,另一方会永远卡在"等对手走"的状态 —— `Status` 永远 `Playing`、`Game.Result` 永远 `null`、ELO 永远没结算、`Status=Finished` 永远不触发。`Room.Leave` 在 Playing 状态下仅把玩家视为"离席",spec 里显式写着"判负逻辑留给后续变更"—— **就是这一次**。

补两条对局结束路径:

1. **主动认输**(Resign):玩家点一下按钮,对方立即胜。
2. **超时判负**(Turn Timeout):每一步有时限(默认 60s),超时的玩家自动判负。

两条路径共享"对局结束事务"的尾部 —— `Game.Result` 置位、`Status = Finished`、ELO 结算、广播 `GameEnded` SignalR 事件。把 `MakeMoveCommandHandler` 里的 ELO 结算逻辑抽成共享 helper,三个 handler(`MakeMove` / `Resign` / `TurnTimeout`)共用。

顺便给 Game 加一个 `EndReason` 字段(`Connected5` / `Resigned` / `TurnTimeout`),这样前端 / 回放 / 战绩页能明示"为啥结束的";没有这个字段时,从 Moves 历史推不出"到底是连五赢的还是对手认输的"。

## What Changes

- **Domain**:
  - 新枚举 `GameEndReason { Connected5 = 0, Resigned = 1, TurnTimeout = 2 }`。
  - `Game.EndReason : GameEndReason?`(对局进行中 `null`)。`Game.FinishWith` 签名追加 `GameEndReason reason` 参数;所有现有调用方改为传 `Connected5`。
  - 新异常:
    - `TurnNotTimedOutException`(sealed,继承 Exception,HTTP 409):TurnTimeout 方法被过早调用(worker 竞态:poll 时已超时,但到 handler 执行时对手刚刚落了子)。
  - `Room.Resign(UserId userId, DateTime now) : GameEndOutcome`:
    - `Status != Playing` → `RoomNotInPlayException`
    - `userId` 不是 Black / White 玩家 → `NotAPlayerException`
    - 对手棋色方胜;`Game.FinishWith(result, winnerId, Resigned, now)`;`Status = Finished`
    - 返回 `GameEndOutcome(Result, WinnerId)`
    - **不要求是自己的回合** —— 认输任何时刻都可以
  - `Room.TimeOutCurrentTurn(DateTime now, int turnTimeoutSeconds) : GameEndOutcome`:
    - `Status != Playing` → `RoomNotInPlayException`
    - 计算 `lastActivityAt = Moves.LastOrDefault()?.PlayedAt ?? Game.StartedAt`;
    - `(now - lastActivityAt).TotalSeconds < turnTimeoutSeconds` → `TurnNotTimedOutException`(防御竞态)
    - `CurrentTurn` 的棋色方负,对方胜;`Game.FinishWith(winner result, winnerId, TurnTimeout, now)`;`Status = Finished`
    - 返回 `GameEndOutcome`
  - 新只读 record `GameEndOutcome(GameResult Result, UserId? WinnerUserId)`。
- **Application**:
  - 新 feature `Features/Rooms/Resign/`:`ResignCommand(UserId, RoomId) : IRequest<GameEndedDto>` + handler。
  - 新 feature `Features/Rooms/TurnTimeout/`:`TurnTimeoutCommand(RoomId) : IRequest<Unit>` + handler(**内部命令**,仅 worker 发)。
  - 新共享 helper `Features/Rooms/Common/GameEloApplier.cs`:把现有 `MakeMoveCommandHandler.ApplyEloAsync` 的 logic 抽公静态方法 `ApplyAsync(Room, GameResult, IUserRepository, CancellationToken)`,供三个 handler 复用;**`MakeMoveCommandHandler` 同步重构**为调用这个 helper(行为不变)。
  - `IRoomRepository` 新方法 `Task<IReadOnlyList<RoomId>> GetRoomsWithExpiredTurnsAsync(DateTime now, int turnTimeoutSeconds, CancellationToken ct)`。
  - `GameOptions`(新,绑定 appsettings `"Game"` 段):
    - `TurnTimeoutSeconds: int = 60`(range [10, 3600])
    - `TimeoutPollIntervalMs: int = 5000`(range [1000, 60000])
  - `GameSnapshotDto` **追加字段**:
    - `TurnStartedAt: DateTime`(= `Moves.LastOrDefault()?.PlayedAt ?? Game.StartedAt`)
    - `TurnTimeoutSeconds: int`(从 GameOptions 读)
    - 追加字段**不破坏**既有客户端序列化(JSON 多字段容忍)。
  - `GameSnapshotDto` **追加字段** `EndReason: GameEndReason?`。
  - `GameEndedDto` **追加字段** `EndReason: GameEndReason`(结束时非空)。
- **Infrastructure**:
  - `GameConfiguration`:映射 `EndReason` 列(`INT NULL`)。
  - Migration `AddGameEndReason`:`Games.EndReason INTEGER NULL`;回填:老数据(已 Finished 的对局)**MUST** 回填为 `0`(Connected5),因为老路径只有连五一种结束方式;未 Finished 的 Games 保持 `NULL`。
  - `RoomRepository.GetRoomsWithExpiredTurnsAsync`:`Where(Status=Playing)` + 取 `Games.Moves.Max(PlayedAt)` 或 `Games.StartedAt` 与 `now - timeout` 比较,返回 `RoomId` 列表。
  - 新 `BackgroundServices/TurnTimeoutWorker : BackgroundService`:每 `TimeoutPollIntervalMs` 调用 `GetRoomsWithExpiredTurnsAsync(_clock.UtcNow, TurnTimeoutSeconds, ct)`,逐个发 `TurnTimeoutCommand`。异常处理模式同 `AiMoveWorker`。
- **Api**:
  - `RoomsController` 新 action:`POST /api/rooms/{id}/resign` → `ResignCommand` → 200 + `GameEndedDto`。
  - `ExceptionHandlingMiddleware`:`TurnNotTimedOutException` → 409(合入现有 409 分支)。
  - `RoomMapping.ToState`:`GameSnapshotDto` 增 `TurnStartedAt` / `TurnTimeoutSeconds`(后者从 `IOptions<GameOptions>` 注入)/ `EndReason`。
  - `Program.cs`:注册 `GameOptions`、`TurnTimeoutWorker`、`appsettings.json` 增 `"Game"` 段。
- **Tests**:
  - Domain(~12 tests):
    - `Room.Resign`:成功(Black / White 各一)、RoomNotInPlay、NotAPlayer、TurnNotMine 不检查(可以任意时刻 resign)。
    - `Room.TimeOutCurrentTurn`:超时成功(Black 超时 / White 超时各一)、RoomNotInPlay、未到超时阈值抛 TurnNotTimedOutException、边界值 now - lastActivity = timeout 恰好 → 成功。
    - `Game.EndReason`:`FinishWith` 传入各 reason 后字段一致。
  - Application(~8 tests):
    - `ResignCommandHandler`:成功路径(Domain.Resign 调用一次、ELO 应用、SaveChanges 一次、Notifier 发 RoomStateChanged + GameEnded);RoomNotFound;NotAPlayer 透传。
    - `TurnTimeoutCommandHandler`:成功路径(同上)、`TurnNotTimedOutException` 吞 / 不发事件(worker 防御)、RoomNotFound、RoomNotInPlay。
    - `GameEloApplier` 单测(原 ApplyEloAsync 的测试覆盖仍在 MakeMove handler 测试里,且在这里新增至少 1 个"直接调 applier"用例)。
  - 端到端冒烟:认输 → `GameEnded.EndReason == Resigned`、ELO 正常 +20 / -20;构造超时(或把 `TurnTimeoutSeconds` 调到 3s 快速验证)→ `GameEnded.EndReason == TurnTimeout`。

**显式不做**(留给后续变更):
- **对局重连 / 掉线恢复**:SignalR 连接断开与重连的完整语义(需要心跳 + client 重新 join)归 `add-reconnect`。本次只处理"玩家不走"(无论原因),不区分"下线"与"走神"。
- **自定义 timeout**:每房间不同 timeout、按段位调整、blitz 模式 —— 留给 `add-custom-timecontrol`。
- **前端倒计时 UI**:DTO 提供的 `TurnStartedAt` / `TurnTimeoutSeconds` 足够客户端自算,后端不做 tick 广播。
- **"认输的对手仍可落子"**:Resign 立即 Finished,**没有**"下一步再生效"的延迟。
- **双向超时警告(30s 时推一条 warning)**:加一层 SignalR 事件 `TurnTimeoutWarning`,本次不做 —— 观感优化,不影响正确性。
- **Bot 超时的特殊处理**:AiMoveWorker 正常工作时 bot 1–3s 即答,远低于 60s timeout;Worker 若挂了导致 bot 超时"输"给人,算**系统自愈信号**(人不会被无限卡)。不特殊 case。
- **认输在 AI 房间的 ELO 套利**:Host 认输给 bot → bot 加分、Host 扣分;反向认输为"bot 向 Host 认输"路径不存在(bot 不会调 resign endpoint)。与 add-ai-opponent 的 D7 一致(bot 吃 ELO 但不上榜)。

## Capabilities

### New Capabilities

(无)

### Modified Capabilities

- **`room-and-gameplay`** — `Room` 聚合增加 `Resign` / `TimeOutCurrentTurn` 两个对局结束路径;`Game` 增加 `EndReason` 字段;新 REST 端点 `POST /api/rooms/{id}/resign`;新后台服务 `TurnTimeoutWorker`;新异常 `TurnNotTimedOutException`;`GameSnapshotDto` / `GameEndedDto` 字段扩展。

## Impact

- **代码规模**:~30 新文件(含测试)+ 少量现有文件修改 + 1 migration。比 `add-dissolve-room` 大,比 `add-ai-opponent` 小。
- **NuGet**:零。
- **HTTP 表面**:+1 端点 `POST /api/rooms/{id}/resign`。
- **SignalR 表面**:零新事件(复用现有 `RoomStateChanged` + `GameEnded`)。
- **数据库**:`Games` 表多 1 列 `EndReason`(INT NULL);老 Finished 行回填 0 = Connected5(一次性 UPDATE,在 migration 的 Up 里)。
- **运行时**:新增 1 个 `BackgroundService` 线程(每 5s 查一次超时房间),并发轻度增加。
- **重构**:`MakeMoveCommandHandler.ApplyEloAsync` 抽共享 helper。行为无变化,测试仍绿。
- **后续变更将依赖**:`add-reconnect`(断线重连时触发 timeout 豁免?)、`add-observability`(worker 超时事件入日志)、`add-game-replay`(`EndReason` 已就绪)。
