# room-and-gameplay 的规格变化

## MODIFIED Requirements

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

**首手座位同样有默认、也同样可被规则覆盖。** 默认 MUST 是 `0`;实现 `IFirstSeatRules` 的棋种
在开局那一刻决定它,而内核 MUST 校验返回值落在 `[0, SeatCount)` 内。挖坑是第一个需要它的:
**持最小 ♣ 的人首叫且首出**,而那是发牌决定的,不是「谁坐 0 号」这条约定。
**把发牌旋转成「最小 ♣ 总在 0 号」MUST NOT 被用来绕开它** —— 统计上等价,体验上不等价:
那样同一个人每一局都先叫。

**`Stone` MUST NOT 出现在 `Gewu.Domain/Rooms/` 下的任何文件中。** 这是"内核不知道一个游戏有几个人"的可执行形式,MUST 由一条测试强制而不是靠约定。

`Stone` 本身不废弃,它下沉到棋盘类棋种的规则内部。`add-xiangqi` 立下的「`Stone.Black` 就是红」那条读法**一个字不动**。

#### `Setup` 是一个内核从不解释的字符串

内核 MUST NOT 读它的内容、MUST NOT 校验它的格式、MUST NOT 依赖它的长度。**它 MUST NOT 出现在任何 DTO 上**,由一条反射断言强制(DTO 命名空间下不得有名字含 `Setup` 的成员)。行为测试只能证明**今天**的投影没带上它 —— **一个不存在的成员没有明天。**

`Game` 不独立于 `Room` 存活;构造仅由 `Room.JoinAsPlayer` 内部发生。`Game.FinishWith` 的签名 MUST 为 `FinishWith(GameResult, UserId?, GameEndReason, DateTime)`。

#### Scenario: 初始 Game 状态
- **WHEN** 坐满触发 `JoinAsPlayer`,且棋种不实现 `IFirstSeatRules`
- **THEN** `Game.StartedAt == now`;`CurrentTurn == 0`;`Moves` 空;`EndedAt == null`;`Result == null`;`EndReason == null`

#### Scenario: 规则可以指名首手座位
- **WHEN** 棋种实现 `IFirstSeatRules` 并返回 `2`
- **THEN** `Game.CurrentTurn == 2`;而**不实现它的棋种一行不动**,仍从 `0` 开始

#### Scenario: 越界的首手座位在开局那一刻被拒
- **WHEN** `IFirstSeatRules.FirstSeat` 返回 `[0, SeatCount)` 之外的值
- **THEN** MUST 抛 `InvalidFirstSeatException`(code `invalid-first-seat`),房间 MUST 留在
  `Waiting`、`Game` MUST 仍是 `null` —— 存下来会造出一局**谁都动不了**的棋,而它要到几十秒后
  由超时兜底才暴露出来

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
