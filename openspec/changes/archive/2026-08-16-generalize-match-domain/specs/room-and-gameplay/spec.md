## ADDED Requirements

### Requirement: `Move` 子实体记录可空的起点

`Move` SHALL 新增 `int? FromRow` 与 `int? FromCol`,与既有的 `Row` / `Col`(终点)并存。

两者 MUST 同为 `null` 或同为非 `null` —— 半个坐标不是坐标。

数据库层两列 MUST 可空,于是迁移是纯增量:既有的落子类记录不用回填,`Down` 只丢列。

MUST NOT 改用 JSON 载荷列。象棋的每一步都恰好是 `from → to`(没有王车易位、吃过路兵、升变),
两个可空列就覆盖了两类棋种,而且**列仍然可查询**、EF 原生映射、replay 仍是强类型的 ——
写错了是编译错误而不是运行时的 `JsonException`。真出现不规则走子时再加列或那时才上 JSON。

#### Scenario: 落子类的起点为空
- **WHEN** 记录一步五子棋
- **THEN** `FromRow == null && FromCol == null`

#### Scenario: 走子类的起点非空
- **WHEN** 记录一步走子类的棋
- **THEN** `FromRow` / `FromCol` 都非 `null`

#### Scenario: 迁移不动既有数据
- **WHEN** 在含既有 `Moves` 行的库上跑迁移
- **THEN** 每行的 `Ply` / `Row` / `Col` / `Stone` 一字不变,两个新列为 `NULL`

## RENAMED Requirements

一条 requirement 连标题一起改:落子判定不再属于聚合根。
(`GameEndReason` 那条**不改标题** —— 它的主题仍然是「表达对局结束原因」,变的只是一个成员名。)
应用顺序 RENAMED → REMOVED → MODIFIED → ADDED,所以下面 MODIFIED 用的是新标题。

- FROM: ### Requirement: `Room.PlayMove` 以原子事务落子、判胜并推进状态
- TO: ### Requirement: `Room.PlayMove` 校验回合与玩家身份，把盘面判定交给规则

### Requirement: Hub 用两个方法承载两种走子形状

`GomokuHub` SHALL 保留 `MakeMove(roomId, row, col)` 原样不动(落子类棋种),并新增
`MovePiece(roomId, fromRow, fromCol, row, col)`(走子类棋种)。两者 MUST 分派到同一条
`MakeMoveCommand`。

**MUST NOT 改成给 `MakeMove` 加两个可选参数。** SignalR **不套用 C# 的可选参数默认值**:
三参调用打到五参方法上,服务端直接回
`InvalidDataException: Invocation provides 3 argument(s) but target expects 5` ——
每一个已发布的客户端会当场下不了棋。

这一条是 `AiSmoke` 跑出来的。该工具不知道这次重构存在,所以它撞上的正是真实客户端会撞上的东西
—— 而三个层级的单元测试(Domain / Application / Api)**一条都没有发现它**,因为它们都不经过
SignalR 的参数绑定。

这与「不给规则开两个方法」不矛盾:那条约束的理由是**调用方得判断棋种**,而规则的调用方是通用的
聚合根。Hub 的调用方是某个棋种自己的棋盘组件,按定义只服务一个棋种。Domain 一侧仍然只有
`IGameRules.Apply` 一个入口。

#### Scenario: 已发布客户端不受影响
- **WHEN** 客户端以三个参数调 `MakeMove`
- **THEN** 正常落子 —— 签名一个字没改

#### Scenario: 走子类棋种走另一个方法
- **WHEN** 客户端调 `MovePiece(roomId, 0, 1, 2, 2)`
- **THEN** 命令带上起点,`Move` 行的 `FromRow` / `FromCol` 落库

## MODIFIED Requirements

### Requirement: `Room.PlayMove` 校验回合与玩家身份，把盘面判定交给规则

`Room.PlayMove(UserId userId, MoveIntent intent, DateTime now, IGameRules rules)` SHALL 依次执行:

1. `Status != Playing` → 抛 `RoomNotInPlayException`
2. `userId` 不是黑 / 白方 → 抛 `NotAPlayerException`
3. 不是该方回合 → 抛 `NotYourTurnException`
4. 调 `rules.Apply(history, intent, side)` —— **越界、重复落子、走法合法性全部由规则回答**
5. 合法则 append 一条 `Move`(含可空起点)、切换回合
6. `Result != Ongoing` 则 `Game.FinishWith(result, winner, GameEndReason.Decided, now)` 并转 `Finished`

**聚合根 MUST NOT 再调 `rules.IsInBounds` / `rules.CreateBoard` / `Board.PlaceStone`。** 盘面语义
整个属于规则。这是象棋能进这个聚合的前提:它的一格上是七种棋子之一 × 两方,胜负是将死 / 困毙,
与最后一步的位置没有直接关系 —— 没有一条能塞进「连 N 子棋盘」。

签名从 `Position position` 改为 `MoveIntent intent`。落子类棋种的调用方传 `MoveIntent(null, to)`。

#### Scenario: 非玩家落子
- **WHEN** 一个围观者调 `PlayMove`
- **THEN** 抛 `NotAPlayerException`,MUST NOT 调 `rules.Apply`

#### Scenario: 不是自己的回合
- **WHEN** 白方在黑方回合调 `PlayMove`
- **THEN** 抛 `NotYourTurnException`,MUST NOT 调 `rules.Apply`

#### Scenario: 规则拒绝则聚合状态不变
- **WHEN** `rules.Apply` 抛 `InvalidMoveException`
- **THEN** `Game.Moves` 不增加、`CurrentTurn` 不变、`Status` 仍是 `Playing`

#### Scenario: 规则判出胜负则对局结束
- **WHEN** `rules.Apply` 返回 `BlackWin`
- **THEN** `Status == Finished`、`Game.Result == BlackWin`、`EndReason == Decided`、`WinnerUserId` 是黑方

### Requirement: `GameEndReason` 枚举表达对局结束原因

`GameEndReason` SHALL 定义 `Decided = 0` / `Resigned` / `TurnTimeout`。

`Decided` **重命名自 `Connected5`**,底层值不变。原名描述的是五子棋的胜利条件,而这个字段回答的
问题是「这局怎么结束的」,答案只有三类:规则从局面判出了结果 / 有人认输 / 时间到。

它不是陈旧而是**错的** —— 一字棋从上线第一天起就在给三连记录「Connected5」,象棋会给将死记录
同一个词。`Decided` 同时覆盖平局(一字棋满盘和棋也是规则判出来的)。

底层值保持 `0`,数据库存的是 int,**不需要数据迁移**;变的只有 JSON 线上的字符串,
而 web 与后端同批发布。

#### Scenario: 底层值不变
- **WHEN** 检视 `GameEndReason.Decided`
- **THEN** 其值为 `0`,与原 `Connected5` 相同 —— 既有行不需要改写

#### Scenario: 枚举里没有棋种专名
- **WHEN** 反射检视 `GameEndReason` 的成员名
- **THEN** MUST NOT 出现任何以某个棋种胜利条件命名的成员
