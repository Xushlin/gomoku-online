# room-and-gameplay Specification Delta

## MODIFIED Requirements

### Requirement: `Room.PlayMove` 校验回合与玩家身份，把盘面判定交给规则

`Room.PlayMove(UserId userId, MoveIntent intent, DateTime now, IGameRules rules)` SHALL 依次执行:

1. `Status != Playing` → 抛 `RoomNotInPlayException`
2. `SeatOf(userId) == null` → 抛 `NotAPlayerException`
3. 不是该座位的回合 → 抛 `NotYourTurnException`
4. 调 `rules.Apply(history, intent, seat)` —— **越界、重复落子、走法合法性全部由规则回答**
5. 合法则 append 一条 `Move`(含可空起点)、按 `(seat + 1) % SeatCount` 切换回合
6. `Result != Ongoing` 则 `Game.FinishWith(result, winner, GameEndReason.Decided, now)` 并转 `Finished`

**赢家 MUST 由 `PlayerAt(application.WinnerSeat)` 得到**,而 MUST NOT 由结果值 `switch` 出黑方 / 白方。
后者此前是 `GameResult.BlackWin => BlackPlayerId`,它把"谁赢了"这一个事实存了两份 —— 一份在
`Result` 的取值里,一份在 `WinnerUserId` 里。`Draw` 与 `Ongoing` 时 `WinnerSeat` 为 `null`,写入的
`WinnerUserId` 也是 `null`。

**聚合根 MUST NOT 再调 `rules.IsInBounds` / `rules.CreateBoard` / `Board.PlaceStone`。** 盘面语义
整个属于规则。

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

  这一条**只能由一个三座位规则验证**,而两座位棋种下它与旧行为逐步等价。它证明的是"赢家从座位查",
  MUST NOT 被当成"这个接缝对牌类够用"的证据 —— 后者只有真游戏能证。

#### Scenario: 平局不写赢家
- **WHEN** `rules.Apply` 返回 `(Draw, null)`
- **THEN** `Game.Result == Draw`、`WinnerUserId == null`

### Requirement: `Room.Resign` 允许玩家任意时刻认输

系统 SHALL 在 `Room` 聚合根上提供 `Resign(UserId userId, DateTime now) : GameEndOutcome` 方法。规则:

- `Status != Playing` 或 `Game == null` → MUST 抛 `RoomNotInPlayException`
- `SeatOf(userId) == null` → MUST 抛 `NotAPlayerException`
- **MUST NOT** 检查 `CurrentTurn` —— 认输不限回合,可在对手回合认输
- 推导对手座位与 UserId;`Game.FinishWith(GameResult.Decided, opponentUserId, GameEndReason.Resigned, now)`;`Status` 转换为 `Finished`
- 返回 `GameEndOutcome(GameResult.Decided, opponentUserId)`

`GameEndOutcome(GameResult Result, UserId? WinnerUserId)` MUST 定义在 `Gewu.Domain.Rooms` 命名空间,
与 `MoveOutcome` 同文件,是 `Resign` / `TimeOutCurrentTurn` 的通用返回类型。

结果值 MUST 是 `Decided`,而 MUST NOT 是带颜色的取值。**谁赢了由 `WinnerUserId` 一处说明。**

**本方法今天仍假定恰好两个座位** —— "对手"在两个座位时唯一,三个座位时不唯一。它 MUST 在座位数
不为 2 时以一个具名异常拒绝,而 MUST NOT 猜一个对手。拆除条件:第一个 `SeatCount != 2` 的棋种落地,
届时"认输"对它意味着什么是那个棋种要回答的问题,不是这里可以默认的。

#### Scenario: 先手座位认输
- **WHEN** 先手座位玩家(含 Host)在 Playing 状态调 `Resign(hostId, now)`
- **THEN** 返回 `GameEndOutcome(Decided, whitePlayerId)`;`Game.Result == Decided`;`Game.WinnerUserId == whitePlayerId`;`Game.EndReason == Resigned`;`Game.EndedAt == now`;`Room.Status == Finished`

#### Scenario: 后手座位认输
- **WHEN** 后手座位玩家调 `Resign(whiteId, now)`
- **THEN** 返回 `GameEndOutcome(Decided, blackPlayerId)`;其他字段对称

#### Scenario: 非自己回合也可认输
- **WHEN** `CurrentTurn == 0`,后手座位玩家调 `Resign(whiteId, now)`
- **THEN** 不抛异常;对局按后手认输 / 先手胜结束

#### Scenario: 非玩家认输被拒
- **WHEN** 一个不在任何座位上的 `UserId`(围观者或任意其他用户)调 `Resign`
- **THEN** 抛 `NotAPlayerException`

#### Scenario: Waiting / Finished 状态调用
- **WHEN** `Status != Playing`
- **THEN** 抛 `RoomNotInPlayException`

#### Scenario: 座位数不为 2 时拒绝
- **WHEN** 一个三座位房间里的玩家调 `Resign`
- **THEN** 抛一个具名异常;MUST NOT 任选一个对手判胜

### Requirement: `Room.TimeOutCurrentTurn` 按阈值判当前回合玩家超时负

系统 SHALL 在 `Room` 聚合根上提供 `TimeOutCurrentTurn(DateTime now, int turnTimeoutSeconds) : GameEndOutcome`。规则:

- `Status != Playing` 或 `Game == null` → MUST 抛 `RoomNotInPlayException`
- `turnTimeoutSeconds < 1` → MUST 抛 `ArgumentOutOfRangeException`
- 计算 `lastActivity = Game.Moves.OrderBy(m => m.Ply).LastOrDefault()?.PlayedAt ?? Game.StartedAt`
- `(now - lastActivity).TotalSeconds < turnTimeoutSeconds` → MUST 抛 `TurnNotTimedOutException`(防 worker 竞态)
- `>= turnTimeoutSeconds` 时:`CurrentTurn` 座位为 loser,另一个座位为 winner;`Game.FinishWith(GameResult.Decided, winnerUserId, GameEndReason.TurnTimeout, now)`;`Status = Finished`
- 返回 `GameEndOutcome(GameResult.Decided, winnerUserId)`

结果值 MUST 是 `Decided`。与 `Resign` 同理,**本方法今天仍假定恰好两个座位**,座位数不为 2 时
MUST 以具名异常拒绝。这条限制**不能就这样留给第一个三座位棋种** —— `TurnTimeoutWorker` 会周期性
调用它,而一个每次都抛的调用点就是 `enforce-ai-availability` 那个缺陷的形状:worker 每 1500 ms
抛进日志的虚空,而房间永远停在那里。所以三座位棋种落地**之前** MUST 先给出它的超时语义。

#### Scenario: 先手座位超时
- **WHEN** `CurrentTurn == 0`,`lastActivity = t0`,`now - t0 = 61s`,`timeout = 60`
- **THEN** 返回 `GameEndOutcome(Decided, whitePlayerId)`;`Game.Result == Decided`;`Game.WinnerUserId == whitePlayerId`;`Game.EndReason == TurnTimeout`;`Room.Status == Finished`

#### Scenario: 后手座位超时
- **WHEN** 先手已走 1 子(ply=1, playedAt=t1),`CurrentTurn == 1`,`now - t1 >= timeout`
- **THEN** 返回 `GameEndOutcome(Decided, blackPlayerId)`

#### Scenario: 无 Moves 时以 StartedAt 为基准
- **WHEN** `Game.Moves.Count == 0`,`now - Game.StartedAt >= timeout`
- **THEN** 先手超时 → 后手胜

#### Scenario: 阈值恰好
- **WHEN** `(now - lastActivity).TotalSeconds == turnTimeoutSeconds`(例如都为 60)
- **THEN** **成功判负**(用 `>=` 比较,不是 `>`)

#### Scenario: 座位数不为 2 时拒绝
- **WHEN** 一个三座位房间超时
- **THEN** 抛一个具名异常;MUST NOT 任选一个赢家
