## ADDED Requirements

### Requirement: `GameEndReason` 枚举表达对局结束原因

系统 SHALL 在 `Gomoku.Domain/Enums/GameEndReason.cs` 定义 `enum GameEndReason { Connected5 = 0, Resigned = 1, TurnTimeout = 2 }`。底层整数值固定,以便序列化稳定性与未来追加(如 `Disconnected = 3`)。

#### Scenario: 枚举值存在
- **WHEN** 审阅 `Gomoku.Domain/Enums/GameEndReason.cs`
- **THEN** 存在三个值 `Connected5=0`、`Resigned=1`、`TurnTimeout=2`

---

### Requirement: `Game.EndReason` 字段记录对局结束原因

`Game` 子实体 MUST 新增 `EndReason: GameEndReason?` 只读属性(`get; private set;`)。对局进行中 MUST 为 `null`;对局结束(`Result != null`)时 MUST 非 `null`,且取值与触发结束的路径对应:`Room.PlayMove` 的连五路径 → `Connected5`、`Room.Resign` → `Resigned`、`Room.TimeOutCurrentTurn` → `TurnTimeout`。

`Game.FinishWith` 的签名 MUST 扩展为 `FinishWith(GameResult, UserId?, GameEndReason, DateTime)`,reason 为必填。

数据库层 MUST 为 `Games.EndReason` 列设置 `INTEGER NULL`,以便老数据(未结束局)保持 `null`。`AddGameEndReason` migration 的 Up MUST 一次性回填:`UPDATE Games SET EndReason = 0 WHERE Result IS NOT NULL`(所有老 Finished 局都是连五胜,唯一已实现的结束路径)。

#### Scenario: 进行中局 EndReason 为 null
- **WHEN** 查询某 `Status == Playing` 房间的 `Game.EndReason`
- **THEN** 返回 `null`

#### Scenario: 连五结束局 EndReason 为 Connected5
- **WHEN** 对局通过 `Room.PlayMove` 连五结束
- **THEN** `Game.EndReason == GameEndReason.Connected5`,`Game.Result != null`,`Game.EndedAt != null`

#### Scenario: 认输结束局 EndReason 为 Resigned
- **WHEN** 对局通过 `Room.Resign` 结束
- **THEN** `Game.EndReason == GameEndReason.Resigned`

#### Scenario: 超时结束局 EndReason 为 TurnTimeout
- **WHEN** 对局通过 `Room.TimeOutCurrentTurn` 结束
- **THEN** `Game.EndReason == GameEndReason.TurnTimeout`

---

### Requirement: `Room.Resign` 允许玩家任意时刻认输

系统 SHALL 在 `Room` 聚合根上提供 `Resign(UserId userId, DateTime now) : GameEndOutcome` 方法。规则:

- `Status != Playing` 或 `Game == null` → MUST 抛 `RoomNotInPlayException`
- `userId` 不是 `BlackPlayerId` 且不是 `WhitePlayerId` → MUST 抛 `NotAPlayerException`
- **MUST NOT** 检查 `CurrentTurn` —— 认输不限回合,可在对手回合认输
- 推导对手棋色与 UserId;`Game.FinishWith(opponentResult, opponentUserId, GameEndReason.Resigned, now)`;`Status` 转换为 `Finished`
- 返回 `GameEndOutcome(opponentResult, opponentUserId)`

新 record `GameEndOutcome(GameResult Result, UserId? WinnerUserId)` MUST 定义在 `Gomoku.Domain.Rooms` 命名空间,与现有 `MoveOutcome` 同文件,是 `Resign` / `TimeOutCurrentTurn` 的通用返回类型。

#### Scenario: 黑方认输
- **WHEN** Black 玩家(含 Host)在 Playing 状态调 `Resign(hostId, now)`
- **THEN** 返回 `GameEndOutcome(WhiteWin, whitePlayerId)`;`Game.Result == WhiteWin`;`Game.WinnerUserId == whitePlayerId`;`Game.EndReason == Resigned`;`Game.EndedAt == now`;`Room.Status == Finished`

#### Scenario: 白方认输
- **WHEN** White 玩家调 `Resign(whiteId, now)`
- **THEN** 返回 `GameEndOutcome(BlackWin, blackPlayerId)`;其他字段对称

#### Scenario: 非自己回合也可认输
- **WHEN** `CurrentTurn == Black`,White 玩家调 `Resign(whiteId, now)`
- **THEN** 不抛异常;对局按白方认输 / 黑方胜结束

#### Scenario: 非玩家认输被拒
- **WHEN** 非 Black / White 的 `UserId`(围观者或任意其他用户)调 `Resign`
- **THEN** 抛 `NotAPlayerException`

#### Scenario: Waiting / Finished 状态调用
- **WHEN** `Status != Playing`
- **THEN** 抛 `RoomNotInPlayException`

---

### Requirement: `Room.TimeOutCurrentTurn` 按阈值判当前回合玩家超时负

系统 SHALL 在 `Room` 聚合根上提供 `TimeOutCurrentTurn(DateTime now, int turnTimeoutSeconds) : GameEndOutcome`。规则:

- `Status != Playing` 或 `Game == null` → MUST 抛 `RoomNotInPlayException`
- `turnTimeoutSeconds < 1` → MUST 抛 `ArgumentOutOfRangeException`
- 计算 `lastActivity = Game.Moves.OrderBy(m => m.Ply).LastOrDefault()?.PlayedAt ?? Game.StartedAt`
- `(now - lastActivity).TotalSeconds < turnTimeoutSeconds` → MUST 抛 `TurnNotTimedOutException`(防 worker 竞态)
- `>= turnTimeoutSeconds` 时:`CurrentTurn` 的棋色方为 loser,对方为 winner;`Game.FinishWith(winnerResult, winnerUserId, GameEndReason.TurnTimeout, now)`;`Status = Finished`
- 返回 `GameEndOutcome(winnerResult, winnerUserId)`

#### Scenario: 黑方超时
- **WHEN** `CurrentTurn == Black`,`lastActivity = t0`,`now - t0 = 61s`,`timeout = 60`
- **THEN** 返回 `GameEndOutcome(WhiteWin, whitePlayerId)`;`Game.Result == WhiteWin`;`Game.WinnerUserId == whitePlayerId`;`Game.EndReason == TurnTimeout`;`Room.Status == Finished`

#### Scenario: 白方超时
- **WHEN** 黑方已走 1 子(ply=1, playedAt=t1),`CurrentTurn == White`,`now - t1 >= timeout`
- **THEN** 返回 `GameEndOutcome(BlackWin, blackPlayerId)`

#### Scenario: 无 Moves 时以 StartedAt 为基准
- **WHEN** `Game.Moves.Count == 0`,`now - Game.StartedAt >= timeout`
- **THEN** 黑方超时 → 白方胜

#### Scenario: 阈值恰好
- **WHEN** `(now - lastActivity).TotalSeconds == turnTimeoutSeconds`(例如都为 60)
- **THEN** **成功判负**(用 `>=` 比较,不是 `>`)

#### Scenario: 尚未超时
- **WHEN** `(now - lastActivity).TotalSeconds < turnTimeoutSeconds`(例如 59 vs 60)
- **THEN** 抛 `TurnNotTimedOutException`;`Room` / `Game` 状态保持不变

#### Scenario: 非法 timeout 参数
- **WHEN** `turnTimeoutSeconds == 0`
- **THEN** 抛 `ArgumentOutOfRangeException`

#### Scenario: 非 Playing 状态
- **WHEN** `Status != Playing`
- **THEN** 抛 `RoomNotInPlayException`

---

### Requirement: 新增异常 `TurnNotTimedOutException` 与其 HTTP 映射

系统 SHALL 在 `Gomoku.Domain/Exceptions/RoomExceptions.cs` 新增 `TurnNotTimedOutException`(sealed,继承 `Exception`,`(string message)` 构造)。

Api 层全局异常中间件 MUST 映射:

| 异常 | HTTP |
|---|---|
| `TurnNotTimedOutException` | 409 |

与现有 `RoomNotInPlayException` / `NotYourTurnException` 等 409 同组。

#### Scenario: 映射生效
- **WHEN** Worker 发 `TurnTimeoutCommand` 进入 handler 后 Domain 发现并未真超时,抛 `TurnNotTimedOutException`
- **THEN** 若事件冒泡到 HTTP(实际上 worker 会 try/catch 吞),响应 409;**在 worker 场景下,异常不冒泡到 HTTP**,worker 仅记日志并丢弃,下轮查询会自动不命中该房间

---

### Requirement: `IRoomRepository.GetRoomsWithExpiredTurnsAsync` 查询超时房间

Application 层 SHALL 在 `IRoomRepository` 上新增:

```
Task<IReadOnlyList<RoomId>> GetRoomsWithExpiredTurnsAsync(DateTime now, int turnTimeoutSeconds, CancellationToken cancellationToken);
```

实现 MUST 返回满足以下条件的所有房间 Id:
- `Status == Playing`
- `Game != null`
- `max(Moves.PlayedAt, Game.StartedAt) + turnTimeoutSeconds <= now`(即"当前回合已超时")

只返回 `RoomId` 列表,MUST NOT 物化 `Room` 聚合。签名 MUST 不暴露 EF 类型。

#### Scenario: 无超时房间
- **WHEN** 所有 Playing 房间的当前回合都在 `now - turnTimeoutSeconds` 之后
- **THEN** 返回空列表,不抛

#### Scenario: 一房间超时
- **WHEN** 一个 Playing 房间的最后一步 Move PlayedAt 是 `now - 70s`,`turnTimeoutSeconds = 60`
- **THEN** 返回该房间 Id(正好一个元素)

#### Scenario: Finished 房间不包括
- **WHEN** 一个房间已 Finished 但因某些原因 Moves 数据暴露在超时窗口内
- **THEN** MUST NOT 返回其 Id(`Status == Playing` 过滤生效)

---

### Requirement: `GameOptions` 绑定 `"Game"` 配置段

Application 层 SHALL 定义 `GameOptions`,绑定 `appsettings.json` 的 `"Game"` 段。字段:

- `TurnTimeoutSeconds: int`(`[Range(10, 3600)]`,默认 60)
- `TimeoutPollIntervalMs: int`(`[Range(1000, 60000)]`,默认 5000)

Api 层 `Program.cs` MUST 通过 `services.AddOptions<GameOptions>().BindConfiguration("Game").ValidateDataAnnotations().ValidateOnStart()` 注册。

#### Scenario: 启动默认值
- **WHEN** `appsettings.json` 没有 `"Game"` 段
- **THEN** `GameOptions` 采用默认值 `TurnTimeoutSeconds=60`、`TimeoutPollIntervalMs=5000`

#### Scenario: 合法覆盖
- **WHEN** `appsettings.Development.json` 写 `"Game": { "TurnTimeoutSeconds": 30, "TimeoutPollIntervalMs": 2000 }`
- **THEN** 运行时采用覆盖值

#### Scenario: 非法值拒绝
- **WHEN** 配置 `"TurnTimeoutSeconds": 5`(低于 10)
- **THEN** 应用启动失败(options validation 阻断),不进入 `app.Run()`

---

### Requirement: `ResignCommand` + `/api/rooms/{id}/resign` 暴露主动认输

Application 层 SHALL 新增:

```
public sealed record ResignCommand(UserId UserId, RoomId RoomId) : IRequest<GameEndedDto>;
```

Handler 流程:
1. Load room(null → `RoomNotFoundException`)
2. `var outcome = room.Resign(UserId, _clock.UtcNow)`
3. `await GameEloApplier.ApplyAsync(room, outcome.Result, _users, ct)`
4. `await _uow.SaveChangesAsync(ct)`
5. 构造 `GameEndedDto(outcome.Result, outcome.WinnerUserId?.Value, room.Game!.EndedAt!.Value, room.Game.EndReason!.Value)`
6. Notifier 顺序调用:`RoomStateChangedAsync(room.Id, state, ct)` → `GameEndedAsync(room.Id, dto, ct)`(**不**发 MoveMade)
7. 返回 `GameEndedDto`

Api 层 SHALL 暴露 `POST /api/rooms/{id}/resign`(`[Authorize]`),成功 200 + `GameEndedDto`。MUST NOT 接受 body;MUST NOT 接受 query。调用方 `UserId` 从 JWT `sub` 取。

#### Scenario: 玩家成功认输
- **WHEN** Playing 房间的 Black 玩家 Alice 调 `POST /api/rooms/{id}/resign`
- **THEN** HTTP 200,body 是 `GameEndedDto { Result: WhiteWin, WinnerUserId: whiteId, EndedAt, EndReason: Resigned }`;数据库 `Room.Status == Finished`;双方 `User.Rating` / `Wins` / `Losses` 更新一次

#### Scenario: 未登录拒绝
- **WHEN** 无 Bearer token 调 `POST /api/rooms/{id}/resign`
- **THEN** HTTP 401(JWT 中间件)

#### Scenario: 非玩家认输
- **WHEN** 围观者 / 非成员调 `POST /api/rooms/{id}/resign`
- **THEN** HTTP 403 `NotAPlayerException`

#### Scenario: 房间不在 Playing
- **WHEN** 对 Waiting 或 Finished 房间调
- **THEN** HTTP 409 `RoomNotInPlayException`

#### Scenario: 对局结束事件包含 EndReason
- **WHEN** 认输成功后
- **THEN** SignalR `GameEnded` event 的 payload `GameEndedDto` 的 `EndReason == Resigned`;客户端据此显示"对方认输"

---

### Requirement: `TurnTimeoutCommand` 是 worker 内部命令

Application 层 SHALL 新增:

```
public sealed record TurnTimeoutCommand(RoomId RoomId) : IRequest<Unit>;
```

Handler 流程:
1. Load room(null → `RoomNotFoundException`)
2. `var outcome = room.TimeOutCurrentTurn(_clock.UtcNow, _opts.Value.TurnTimeoutSeconds)`
3. `await GameEloApplier.ApplyAsync(room, outcome.Result, _users, ct)`
4. `await _uow.SaveChangesAsync(ct)`
5. Notifier 顺序:`RoomStateChangedAsync` → `GameEndedAsync`
6. 返回 `Unit.Value`

此命令 **不**暴露 REST 端点、**不**路由 SignalR Hub;仅 `TurnTimeoutWorker` 通过 `ISender.Send` 发送。

#### Scenario: 命令不可经 HTTP 触发
- **WHEN** 审阅 `RoomsController` / `GomokuHub`
- **THEN** 无任何 action / method 构造或分发 `TurnTimeoutCommand`

#### Scenario: Worker 成功触发
- **WHEN** `TurnTimeoutWorker` 发 `TurnTimeoutCommand(roomId)`,handler 执行
- **THEN** Room.Status 转为 Finished;ELO 被应用;SignalR `GameEnded { EndReason: TurnTimeout }` 被广播

#### Scenario: 竞态:worker 晚到一步
- **WHEN** Worker 的 `GetRoomsWithExpiredTurnsAsync` 说"超时了",但到 handler 执行时对手刚落了一子
- **THEN** `Room.TimeOutCurrentTurn` 抛 `TurnNotTimedOutException`;worker 的 try/catch 吞下并记日志,**不**广播事件,Room 保持 Playing

---

### Requirement: `TurnTimeoutWorker` 后台轮询超时房间

Infrastructure 层 SHALL 新增 `BackgroundServices/TurnTimeoutWorker : BackgroundService`。循环:

```
while (!stopToken.IsCancellationRequested)
{
    await Task.Delay(options.TimeoutPollIntervalMs, stopToken);
    using var scope = sp.CreateScope();
    var rooms = scope.Resolve<IRoomRepository>();
    var sender = scope.Resolve<ISender>();
    var clock = scope.Resolve<IDateTimeProvider>();
    var ids = await rooms.GetRoomsWithExpiredTurnsAsync(clock.UtcNow, options.TurnTimeoutSeconds, stopToken);
    foreach (var id in ids)
    {
        try
        {
            await sender.Send(new TurnTimeoutCommand(id), stopToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "TurnTimeoutWorker failed on room {RoomId}", id);
        }
    }
}
```

MUST 满足:
- 每循环 `CreateScope` 以获得正确生命周期的 DbContext / handlers;
- 非取消异常不中断 worker;
- 使用同一 `IDateTimeProvider.UtcNow` 作为时间基准,便于测试注入假时钟。

#### Scenario: 空载
- **WHEN** 无 Playing 房间或全部都未超时
- **THEN** 每轮查询返回空;worker 不发命令,不报错

#### Scenario: 异常不中断
- **WHEN** 某次 `ISender.Send` 抛意外异常
- **THEN** worker 记 Error 日志,继续处理下一房间;下一轮正常运行

#### Scenario: 优雅关闭
- **WHEN** `stopToken` 触发
- **THEN** `ExecuteAsync` 退出,不吃 `OperationCanceledException`

---

### Requirement: `GameEloApplier` 共享 helper 把 ELO 应用抽公

Application 层 SHALL 在 `Features/Rooms/Common/GameEloApplier.cs` 定义 `internal static class GameEloApplier`,方法:

```
public static async Task ApplyAsync(
    Room room,
    GameResult result,
    IUserRepository users,
    CancellationToken cancellationToken);
```

实现 MUST 等价于原 `MakeMoveCommandHandler.ApplyEloAsync`:加载 Black / White 双方 `User`;推导 `GameOutcome`;调 `EloRating.Calculate`;两位 `User` 各调 `RecordGameResult`。MUST NOT 调 `SaveChangesAsync`。

三个 handler(`MakeMoveCommandHandler` / `ResignCommandHandler` / `TurnTimeoutCommandHandler`)在对局结束路径上 MUST 调用此 helper。

#### Scenario: 连五路径仍正常结算
- **WHEN** 玩家连五结束对局(`MakeMoveCommand` 触发 `GameEloApplier.ApplyAsync`)
- **THEN** ELO 行为与重构前完全一致(保留现有 `MakeMoveCommandHandlerTests` 的断言仍全绿)

#### Scenario: 认输路径结算
- **WHEN** 认输结束对局(`ResignCommand` 触发 `GameEloApplier.ApplyAsync`)
- **THEN** 同样调双方 `FindByIdAsync` 一次、各调 `RecordGameResult` 一次,事务在外层 handler 合并 SaveChangesAsync

#### Scenario: 超时路径结算
- **WHEN** 超时结束对局(`TurnTimeoutCommand`)
- **THEN** 与认输一致

---

### Requirement: `GameSnapshotDto` 扩展 TurnStartedAt / TurnTimeoutSeconds / EndReason

`GameSnapshotDto` MUST 追加三个字段(纯追加,向后兼容):

- `DateTime TurnStartedAt` —— 当前回合起始时间,等价于 `Moves.OrderBy(Ply).LastOrDefault()?.PlayedAt ?? Game.StartedAt`
- `int TurnTimeoutSeconds` —— 由 `GameOptions.TurnTimeoutSeconds` 传入的阈值(不同房间相同,为前端倒计时 UI 提供)
- `GameEndReason? EndReason` —— 与 `Game.Result` 同时为 null 或同时非 null

`GameEndedDto` MUST 追加字段 `GameEndReason EndReason`(非 nullable,结束事件时必有)。

`RoomMapping.ToState` MUST 在入参里接受 `turnTimeoutSeconds` 参数,并计算 `TurnStartedAt`。

#### Scenario: 进行中 DTO
- **WHEN** 对 Playing 房间构造 `GameSnapshotDto`
- **THEN** `TurnStartedAt` 是最后一步 `PlayedAt`(或 `StartedAt` 如无 Moves);`TurnTimeoutSeconds > 0`;`EndReason == null`

#### Scenario: 结束 DTO
- **WHEN** 对 Finished 房间构造 `GameSnapshotDto`
- **THEN** `EndReason` 取对应值(Connected5 / Resigned / TurnTimeout)

#### Scenario: GameEndedDto 总含 EndReason
- **WHEN** 任一路径触发 `GameEndedAsync` 广播
- **THEN** payload `GameEndedDto.EndReason` 非 null 且匹配实际原因

## MODIFIED Requirements

### Requirement: `Game` 子实体承载对局运行状态

`Game` MUST 包含字段:
- `Id: Guid`
- `RoomId: RoomId`
- `StartedAt: DateTime`(UTC)
- `EndedAt: DateTime?`
- `Result: GameResult?`(对局进行时为 `null`)
- `WinnerUserId: UserId?`
- **`EndReason: GameEndReason?`**(本次新增;结束时非 null,与 `Result` 同时为 null 或同时非 null)
- `CurrentTurn: Stone`
- `Moves: IReadOnlyCollection<Move>`
- `RowVersion: byte[]`(乐观并发令牌,由 Infrastructure 层维护)

`Game` 不独立于 `Room` 存活;构造仅由 `Room.JoinAsPlayer` 内部发生。`Game.FinishWith` 的签名 MUST 为 `FinishWith(GameResult, UserId?, GameEndReason, DateTime)` —— 新增必填 reason 参数,保证结束路径不漏填。

#### Scenario: 初始 Game 状态
- **WHEN** 白方加入触发 `JoinAsPlayer`
- **THEN** `Game.StartedAt == now`;`CurrentTurn == Black`;`Moves` 空;`EndedAt == null`;`Result == null`;**`EndReason == null`**

#### Scenario: Game 结束状态
- **WHEN** 某方连五或平局或认输或超时后
- **THEN** `EndedAt != null`;`Result != null`;若有胜方则 `WinnerUserId != null`;**`EndReason != null`** 且对应路径

---

### Requirement: `Room.PlayMove` 以原子事务落子、判胜并推进状态

系统 SHALL 提供 `Room.PlayMove(UserId userId, Position position, DateTime now)`,按顺序:

1. `Status != Playing` → MUST 抛 `RoomNotInPlayException`
2. `userId != BlackPlayerId && userId != WhitePlayerId` → MUST 抛 `NotAPlayerException`
3. 根据 `userId` 推断棋色 `Stone`,若不等于 `Game.CurrentTurn` → MUST 抛 `NotYourTurnException`
4. 调 `Board.PlaceStone(new Move(position, stone))`(gomoku-domain 负责越界 / 重复落子判定)
5. Append 一条 `Move` 子实体:`Ply = 上一 Ply + 1`、`Position`、`Stone`、`PlayedAt = now`
6. `Game.CurrentTurn = oppositeColor(stone)`
7. 若 Board 返回的 `GameResult != Ongoing`:
   - `Game.FinishWith(result, winnerUserId, **GameEndReason.Connected5**, now)`(本次修订:显式传入 `Connected5`)
   - `Room.Status = Finished`
8. 返回的 `MoveOutcome` MUST 包含新 `Move` 实体引用与 `GameResult`,供调用方决定发哪些事件。

#### Scenario: 最后一子连五
- **WHEN** 黑方已在 `(7,3)..(7,6)` 连四,调 `PlayMove(aliceId, (7,7), now)`
- **THEN** 返回 `GameResult.BlackWin`;`Game.EndedAt == now`;`Game.WinnerUserId == aliceId`;**`Game.EndReason == Connected5`**;`Room.Status == Finished`

#### Scenario: 合法落子且未连五
- **WHEN** `Playing` 状态,轮到 Alice(黑方),调 `PlayMove(aliceId, (7,7), now)`,棋盘此前为空
- **THEN** 返回 `GameResult.Ongoing`;`Game.Moves` 新增一条 `Ply=1` 的 Move;`Game.CurrentTurn == White`;`Room.Status == Playing`;**`Game.EndReason == null`**(未结束)

#### Scenario: 非玩家尝试落子
- **WHEN** 围观者或非成员调 `PlayMove`
- **THEN** 抛 `NotAPlayerException`

#### Scenario: 不是你的回合
- **WHEN** 白方在 `CurrentTurn == Black` 时调 `PlayMove`
- **THEN** 抛 `NotYourTurnException`

#### Scenario: 非 `Playing` 状态
- **WHEN** `Status == Waiting` 或 `Finished`,调 `PlayMove`
- **THEN** 抛 `RoomNotInPlayException`

#### Scenario: 底层棋盘规则违反
- **WHEN** 正确玩家在正确回合,但 `Position` 越界或该位置已有子
- **THEN** `Board` 抛 `InvalidMoveException`,`Room` MUST 让其原样冒泡;**`Move` 未被 append**,状态保持不变

---

### Requirement: 相关领域异常与其 HTTP 映射

本次新增的领域异常 MUST 被全局异常中间件按下表映射:

| 异常 | HTTP |
|---|---|
| `TurnNotTimedOutException` | 409 |

(本次**仅新增**此 1 条映射;现有 16 条映射 `InvalidRoomNameException` / `RoomNotFoundException` / `RoomNotWaitingException` / `RoomFullException` / `AlreadyInRoomException` / `HostCannotLeaveWaitingRoomException` / `PlayerCannotSpectateException` / `NotInRoomException` / `NotSpectatingException` / `NotAPlayerException` / `NotYourTurnException` / `RoomNotInPlayException` / `InvalidRoomStatusTransitionException` / `DbUpdateConcurrencyException` / `NotRoomHostException`(由 `add-dissolve-room` 加入)/ `InvalidMoveException` 全部保留不变。)

#### Scenario: Worker 竞态的 409(罕见出现在 HTTP)
- **WHEN** `TurnTimeoutCommandHandler` 抛 `TurnNotTimedOutException` 并冒泡(实际 worker try/catch 吞之,故极少进 HTTP;若将来有 REST 端点引入则按 409 返回)
- **THEN** HTTP 409 `ProblemDetails` 指向 `TurnNotTimedOutException`
