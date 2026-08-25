## MODIFIED Requirements

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

**本方法仍假定恰好两个座位** —— "对手"在两个座位时唯一,三个座位时不唯一。它 MUST 在座位数
不为 2 时以一个具名异常(`SeatCountNotSupportedException`)拒绝,而 MUST NOT 猜一个对手。

**那条拆除条件已经响了,而没有人接。** 它原文写的是「第一个 `SeatCount != 2` 的棋种落地,届时
"认输"对它意味着什么是那个棋种要回答的问题」—— 斗地主与挖坑都已经落地,而那个问题至今没有答案。
中间态的代价是量出来的:**在一局真的三人斗地主里点「认输」,拿到的是 HTTP 500**,而服务端日志里
清清楚楚写着为什么。

在答出来之前,这条要求 SHALL 按下面两条止血,而 MUST NOT 靠「反正没人点」:

1. **客户端在座位数不等于 2 的房间里 MUST NOT 提供认输入口。** 判据是描述符的 `seatCount == 2`,
   而 MUST NOT 是「不大于 2」—— 后者在描述符还没到达时会说「可以认输」。
2. **这个异常 MUST 映射成 4xx**(见下面那条映射要求),而 MUST NOT 落进未处理异常的 500。
   一个领域不变量的拒绝和一次服务端崩溃在客户端看起来一模一样,而它们该被区别对待。

**真正的拆除条件改成:点数阶梯落地那一天。** 三家局里「认输」只能是「弃牌认负、按点数结算」,
而那需要一个点数结算的调用者 —— `DoudizhuScoring.Settle` / `WakengScoring.Settle` 至今没有生产
调用者,这两件事是同一件事的两头。

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

#### Scenario: 三座位房间不提供认输
- **WHEN** 一局 `seatCount == 3` 的对局正在进行,当前用户占着座位
- **THEN** 操作条 MUST NOT 渲染认输按钮;而离开与催促照旧 —— 被拒绝的是认输,不是整组动作

#### Scenario: 描述符还没到达时也不提供
- **WHEN** 房间的棋种不在描述符表里(`seatCount` 读不到)
- **THEN** MUST NOT 渲染认输按钮 —— 未知不等于两个

#### Scenario: 直接调 API 得到 409 而不是 500
- **WHEN** 绕过界面对一局三人对局 `POST /api/rooms/{id}/resign`
- **THEN** HTTP **409**,而 MUST NOT 是 500;响应体说明座位数不支持

### Requirement: 相关领域异常与其 HTTP 映射

系统 SHALL 把 `DbUpdateConcurrencyException`(来自 EF)映射为 HTTP 409 + `ProblemDetails`(`type: "https://gewu/errors/concurrent-move"`)。本次修订 MUST 把该映射的覆盖面从原先"仅 Room/Game 并发"扩展到"Room/Game **与** User 聚合并发冲突";两种情况下 EF 抛出同一异常类型,Api 中间件 MUST NOT 为二者引入不同的 `ProblemDetails.type`。

- 既有(`add-rooms-and-gameplay` 引入):Room / Game 并发冲突(由 `Game.RowVersion` 保护)。
- **新增**(`add-concurrency-hardening`):User 聚合 `RecordGameResult` 写入冲突(由 `User.RowVersion` 保护)。

本次 MUST NOT 新增其它异常与 HTTP 映射条目(所有其它既有条目 `RoomNotFoundException` / `RoomNotWaitingException` / ... / `TurnNotTimedOutException` 保持不变)。

| 异常 | HTTP |
|---|---|
| `DbUpdateConcurrencyException`(来自 EF,覆盖 Game 并发 **与** User 并发) | 409 + `type: "concurrent-move"` |
| `SeatCountNotSupportedException`(领域层拒绝三座位认输) | 409 |

**`SeatCountNotSupportedException` 这一行是 `fix-three-seat-resign` 补的,而它补的是一个 500。**
上面那句「本次 MUST NOT 新增其它异常与 HTTP 映射条目」是 `add-concurrency-hardening` 对**它自己**
的约束,不是永久禁令 —— 而这一条漏在表外的后果,是在浏览器里点一次认输就能看到的未处理异常。
**一个不在映射表里的领域异常,和一次真正的服务端崩溃在客户端长得一样。**

#### Scenario: 并发落子冲突(覆盖既有)
- **WHEN** 两个玩家几乎同时调 `MakeMove`,EF 在 `SaveChangesAsync` 抛 `DbUpdateConcurrencyException`(Game.RowVersion 冲突)
- **THEN** HTTP 409,`ProblemDetails.type == "https://gewu/errors/concurrent-move"`

#### Scenario: 并发战绩更新冲突(本次新增)
- **WHEN** 两个对局结束事务并发更新同一 User 的战绩(Alice 同时是两盘的黑方,两盘都触发 ResignCommand / TurnTimeoutCommand 几乎同刻完成)
- **THEN** 一者成功(第一次 RecordGameResult 的结果持久);另一者 EF 抛 `DbUpdateConcurrencyException`;Api 返回 HTTP 409,客户端重拉 `GET /api/users/me` + 相关 `GET /api/rooms/{id}` 再决定重试

#### Scenario: 座位数不支持的认输是 409
- **WHEN** 对一局三人对局 `POST /api/rooms/{id}/resign`
- **THEN** HTTP 409 + `ProblemDetails`;MUST NOT 是 500,MUST NOT 记为未处理异常
