# room-and-gameplay Specification Delta

## MODIFIED Requirements

### Requirement: `Room.TimeOutCurrentTurn` 按阈值判当前回合玩家超时负

系统 SHALL 在 `Room` 聚合根上提供
`TimeOutCurrentTurn(DateTime now, int turnTimeoutSeconds, IGameRules rules) : TurnTimeoutOutcome`。规则:

- `Status != Playing` 或 `Game == null` → MUST 抛 `RoomNotInPlayException`
- `turnTimeoutSeconds < 1` → MUST 抛 `ArgumentOutOfRangeException`
- 计算 `lastActivity = Game.Moves.OrderBy(m => m.Ply).LastOrDefault()?.PlayedAt ?? Game.StartedAt`
- `(now - lastActivity).TotalSeconds < turnTimeoutSeconds` → MUST 抛 `TurnNotTimedOutException`(防 worker 竞态)
- `>= turnTimeoutSeconds` 时分两条路:
  - `rules is ITimeoutFallbackRules fb` → 用 `fb.MoveOnTimeout(history, CurrentTurn)` **替这个座位走一步**,那一步 MUST 走与真人落子完全相同的路径(即经过 `rules.Apply`),返回 `TurnTimeoutOutcome.Played(...)`
  - 否则 → `CurrentTurn` 座位为 loser,另一个座位为 winner;`Game.FinishWith(GameResult.Decided, winnerUserId, GameEndReason.TurnTimeout, now)`;`Status = Finished`;返回 `TurnTimeoutOutcome.Ended(...)`

判负那一条路要求**恰好两个座位** —— "对手"只在两个座位时唯一 —— 不满足时 MUST 抛
`SeatCountNotSupportedException`。**这条限制没有被放宽,只是有了一个正当的出口**:一个三座位棋种
若不提供兜底,仍然会在超时那一刻大声坏掉。

`TurnTimeoutOutcome` MUST 恰好携带两者之一(走了一步 / 结束了),由构造强制;MUST NOT 两个都有
或两个都无。

#### 兜底那一步 MUST 过 `rules.Apply`

MUST NOT 直接往 `Game` 里塞一条 `Move`。两个理由,第二个更要紧:

1. 规则给出的兜底动作也可能非法(实现出错),而非法的一步不该因为"系统替他走的"就被接受。
2. **它可能结束对局** —— 牌类游戏里替人出掉最后一手牌,那一手就赢了。

因此 `PlayMove` 与超时兜底 MUST 共用同一条内部路径。两条路径各写一遍,会让本 spec 已经立下的
「`Apply` 是走子合法性与胜负判定的**唯一**入口」变成两个入口。

兜底走出的一步在**线上与真人走的一步没有区别**,这是刻意的:客户端不需要区分"他走的"与
"系统替他走的",而房间状态广播本来就带着新的 `CurrentTurn`。

#### Scenario: 先手座位超时(没有兜底的棋种)
- **WHEN** `CurrentTurn == 0`,`lastActivity = t0`,`now - t0 = 61s`,`timeout = 60`,规则不实现 `ITimeoutFallbackRules`
- **THEN** 返回 `Ended`,其 `GameEndOutcome` 为 `(Decided, whitePlayerId)`;`Game.Result == Decided`;`Game.WinnerUserId == whitePlayerId`;`Game.EndReason == TurnTimeout`;`Room.Status == Finished`

#### Scenario: 后手座位超时
- **WHEN** 先手已走 1 子(ply=1, playedAt=t1),`CurrentTurn == 1`,`now - t1 >= timeout`
- **THEN** 返回 `Ended`,`WinnerUserId == blackPlayerId`

#### Scenario: 无 Moves 时以 StartedAt 为基准
- **WHEN** `Game.Moves.Count == 0`,`now - Game.StartedAt >= timeout`
- **THEN** 先手超时 → 后手胜

#### Scenario: 阈值恰好
- **WHEN** `(now - lastActivity).TotalSeconds == turnTimeoutSeconds`(例如都为 60)
- **THEN** **成功判负**(用 `>=` 比较,不是 `>`)

#### Scenario: 尚未超时
- **WHEN** `(now - lastActivity).TotalSeconds < turnTimeoutSeconds`
- **THEN** 抛 `TurnNotTimedOutException`;MUST NOT 调 `MoveOnTimeout`

#### Scenario: 有兜底的棋种超时时走一步而不是结束
- **WHEN** 规则实现 `ITimeoutFallbackRules`,已超时
- **THEN** 返回 `Played`;`Game.Moves` 多一条;`Status` 仍为 `Playing`;`CurrentTurn` 已按 `Apply` 的结果推进

#### Scenario: 兜底那一步照样判胜负
- **WHEN** 兜底动作使规则判出胜负
- **THEN** 对局照常结束(`Status == Finished`、`EndReason == Decided`),而 MUST NOT 是 `TurnTimeout` —— 它是被规则判出来的,不是超时判的

#### Scenario: 非法的兜底动作被拒
- **WHEN** `MoveOnTimeout` 返回该局面下非法的一步
- **THEN** 抛 `InvalidMoveException`;`Game.Moves` 不增加、`CurrentTurn` 不变、`Status` 仍是 `Playing`

#### Scenario: 三座位且没有兜底时拒绝
- **WHEN** 一个三座位房间超时,规则不实现 `ITimeoutFallbackRules`
- **THEN** 抛 `SeatCountNotSupportedException`;MUST NOT 任选一个赢家

### Requirement: `Room.PlayMove` 校验回合与玩家身份，把盘面判定交给规则

`Room.PlayMove(UserId userId, MoveIntent intent, DateTime now, IGameRules rules)` SHALL 依次执行:

1. `Status != Playing` → 抛 `RoomNotInPlayException`
2. `SeatOf(userId) == null` → 抛 `NotAPlayerException`
3. 不是该座位的回合 → 抛 `NotYourTurnException`
4. 调 `rules.Apply(history, intent, seat)` —— **越界、重复落子、走法合法性全部由规则回答**
5. 合法则 append 一条 `Move`,并按 `MoveApplication.NextSeat`(为 `null` 时按 `(seat + 1) % SeatCount`)切换回合
6. `Result != Ongoing` 则 `Game.FinishWith(result, winner, GameEndReason.Decided, now)` 并转 `Finished`

第 4–6 步 MUST 抽成一条**内部共用**路径,由 `PlayMove` 与 `TimeOutCurrentTurn` 的兜底分支共同调用。
前三步是 `PlayMove` 独有的(超时兜底不需要"这人是不是玩家、是不是他的回合" —— 座位由
`CurrentTurn` 给出)。

**赢家 MUST 由 `PlayerAt(application.WinnerSeat)` 得到**,而 MUST NOT 由结果值 `switch` 出黑方 / 白方。

**聚合根 MUST NOT 再调 `rules.IsInBounds` / `rules.CreateBoard` / `Board.PlaceStone`。**

#### Scenario: 非玩家落子
- **WHEN** 一个围观者调 `PlayMove`
- **THEN** 抛 `NotAPlayerException`,MUST NOT 调 `rules.Apply`

#### Scenario: 不是自己的回合
- **WHEN** 后手座位在先手回合调 `PlayMove`
- **THEN** 抛 `NotYourTurnException`,MUST NOT 调 `rules.Apply`

#### Scenario: 规则拒绝则聚合状态不变
- **WHEN** `rules.Apply` 抛 `InvalidMoveException`
- **THEN** `Game.Moves` 不增加、`CurrentTurn` 不变、`Status` 仍是 `Playing`

#### Scenario: 规则判出胜负则对局结束
- **WHEN** `rules.Apply` 返回 `(Decided, WinnerSeat: 0)`
- **THEN** `Status == Finished`、`Game.Result == Decided`、`EndReason == Decided`、`WinnerUserId == PlayerAt(0)`

#### Scenario: 赢家座位不是零号也一样
- **WHEN** `rules.Apply` 返回 `(Decided, WinnerSeat: 2)` 且房间有三个座位
- **THEN** `WinnerUserId == PlayerAt(2)`

#### Scenario: 平局不写赢家
- **WHEN** `rules.Apply` 返回 `(Draw, null)`
- **THEN** `Game.Result == Draw`、`WinnerUserId == null`

#### Scenario: 规则可以指定下一手
- **WHEN** 一个三座位规则在座位 `0` 走完之后返回 `NextSeat == 2`
- **THEN** `Game.CurrentTurn == 2`,而不是 `1`

### Requirement: `Game` 子实体承载对局运行状态

`Game` MUST 包含字段:
- `Id: Guid`
- `RoomId: RoomId`
- `StartedAt: DateTime`(UTC)
- `EndedAt: DateTime?`
- `Result: GameResult?`(对局进行时为 `null`)
- `WinnerUserId: UserId?`
- `EndReason: GameEndReason?`(结束时非 null,与 `Result` 同时为 null 或同时非 null)
- `CurrentTurn: int` —— **座位号**,`0` 到 `SeatCount - 1`
- `Setup: string?` —— 本局的**服务端侧对局设置**;不需要设置的棋种为 `null`
- `Moves: IReadOnlyCollection<Move>`
- `RowVersion: byte[]`(乐观并发令牌,由 Infrastructure 层维护)

`CurrentTurn` MUST 是座位号而 MUST NOT 是 `Stone`。轮转的默认规则 MUST 为
`(CurrentTurn + 1) % SeatCount`,而 MUST NOT 是两值之间的布尔翻转;规则可以用
`MoveApplication.NextSeat` **覆盖**它。

**`Stone` MUST NOT 出现在 `Gewu.Domain/Rooms/` 下的任何文件中。** 这是"内核不知道一个游戏有几个人"的可执行形式,MUST 由一条测试强制而不是靠约定。

`Stone` 本身不废弃,它下沉到棋盘类棋种的规则内部。`add-xiangqi` 立下的「`Stone.Black` 就是红」那条读法**一个字不动**。

#### `Setup` 是一个内核从不解释的字符串

内核 MUST NOT 读它的内容、MUST NOT 校验它的格式、MUST NOT 依赖它的长度。**它 MUST NOT 出现在任何 DTO 上**,由一条反射断言强制(DTO 命名空间下不得有名字含 `Setup` 的成员)。行为测试只能证明**今天**的投影没带上它 —— **一个不存在的成员没有明天。**

`Game` 不独立于 `Room` 存活;构造仅由 `Room.JoinAsPlayer` 内部发生。`Game.FinishWith` 的签名 MUST 为 `FinishWith(GameResult, UserId?, GameEndReason, DateTime)`。

#### Scenario: 初始 Game 状态
- **WHEN** 坐满触发 `JoinAsPlayer`
- **THEN** `Game.StartedAt == now`;`CurrentTurn == 0`;`Moves` 空;`EndedAt == null`;`Result == null`;`EndReason == null`

#### Scenario: 不需要设置的棋种其 Setup 为 null
- **WHEN** 一个不实现 `IDealtGameRules` 的棋种开局
- **THEN** `Game.Setup == null` —— MUST NOT 是 `""`

#### Scenario: 需要设置的棋种其 Setup 被存下来
- **WHEN** 一个实现 `IDealtGameRules` 的棋种开局,`JoinAsPlayer` 收到的 `setup` 是 `"abc"`
- **THEN** `Game.Setup == "abc"`,一字不改

#### Scenario: 任何 DTO 都不暴露 Setup
- **WHEN** 反射遍历 `Gewu.Application.Common.DTOs` 下的全部类型
- **THEN** 没有任何成员的名字含 `Setup`

#### Scenario: 两座位游戏的轮转不变
- **WHEN** 一个 `SeatCount == 2` 的棋种连走 3 步,规则都不指定 `NextSeat`
- **THEN** `CurrentTurn` 依次为 `0 → 1 → 0 → 1`

#### Scenario: 三座位游戏按环轮转
- **WHEN** 一个 `SeatCount == 3` 的规则连走 3 步,都不指定 `NextSeat`
- **THEN** `CurrentTurn` 依次为 `0 → 1 → 2 → 0`

  这一条用一个假的三座位规则验证,而它证明的是**取模算术**,MUST NOT 被当成"这个接缝对牌类够用"的证据 —— 后者只有真游戏能证。

#### Scenario: 规则指定的下一手覆盖轮转
- **WHEN** 规则在座位 `1` 走完之后返回 `NextSeat == 0`,而 `SeatCount == 3`
- **THEN** `CurrentTurn == 0`,而不是 `2`

#### Scenario: Game 结束状态
- **WHEN** 某方获胜或平局或认输或超时后
- **THEN** `EndedAt != null`;`Result != null`;若有胜方则 `WinnerUserId != null`;`EndReason != null` 且对应路径

### Requirement: `TurnTimeoutCommand` 是 worker 内部命令

Application 层 SHALL 提供:

```
public sealed record TurnTimeoutCommand(RoomId RoomId) : IRequest<Unit>;
```

Handler 流程:

1. Load room(null → `RoomNotFoundException`)
2. **解析规则**:`_rules.For(room.GameKey)`,解析不出来时与落子路径一致地处理 —— 那是一条损坏的房间记录
3. `var outcome = room.TimeOutCurrentTurn(_clock.UtcNow, _opts.Value.TurnTimeoutSeconds, rules)`
4. 仅当对局**已结束**时 `await GameEloApplier.ApplyAsync(room, _rules, _users, ct)`
5. `await _uow.SaveChangesAsync(ct)`
6. Notifier 顺序按结果分两条路:
   - 走了一步:`RoomStateChangedAsync` → `MoveMadeAsync`(与真人落子**逐条相同**);若那一步同时判出胜负,再 `GameEndedAsync`
   - 判他负:`RoomStateChangedAsync` → `GameEndedAsync`
7. 返回 `Unit.Value`

第 4 步的「仅当已结束」与 `MakeMoveCommandHandler` 是**同一条**规则(一步棋不结束对局就不动评分),
不是一条新规则。

此命令 **不**暴露 REST 端点、**不**路由 SignalR Hub;仅 `TurnTimeoutWorker` 通过 `ISender.Send` 发送。

本条此前写的是 `GameEloApplier.ApplyAsync(room, outcome.Result, _users, ct)` —— 那是
`generalize-match-outcome` 之前的签名(那次改动让它从聚合读结果与赢家,并少了一个参数),
而本 spec 没有跟上。顺带订正。

#### Scenario: 命令不可经 HTTP 触发
- **WHEN** 审阅 `RoomsController` / `MatchHub`
- **THEN** 无任何 action / method 构造或分发 `TurnTimeoutCommand`

#### Scenario: Worker 触发一个没有兜底的棋种
- **WHEN** `TurnTimeoutWorker` 发 `TurnTimeoutCommand(roomId)`,该棋种不实现 `ITimeoutFallbackRules`
- **THEN** Room.Status 转为 Finished;ELO 被应用;`GameEnded { EndReason: TurnTimeout }` 被广播;`MoveMadeAsync` MUST NOT 被调

#### Scenario: Worker 触发一个有兜底的棋种
- **WHEN** 同上,但该棋种实现 `ITimeoutFallbackRules`,且兜底那一步没有结束对局
- **THEN** `RoomStateChangedAsync` 与 `MoveMadeAsync` 各一次;`GameEndedAsync` MUST NOT 被调;ELO MUST NOT 被触动

#### Scenario: 兜底那一步结束了对局
- **WHEN** 兜底动作使规则判出胜负
- **THEN** `RoomStateChangedAsync`、`MoveMadeAsync`、`GameEndedAsync` 各一次

#### Scenario: 房间指向本构建不认识的棋种
- **WHEN** `room.GameKey` 在注册表里解析不出规则
- **THEN** 抛 `RoomNotFoundException`;MUST NOT 提交、MUST NOT 广播

#### Scenario: 竞态:worker 晚到一步
- **WHEN** Worker 的 `GetRoomsWithExpiredTurnsAsync` 说"超时了",但到 handler 执行时对手刚落了一子
- **THEN** `Room.TimeOutCurrentTurn` 抛 `TurnNotTimedOutException`;worker 的 try/catch 吞下并记日志,**不**广播事件,Room 保持 Playing
