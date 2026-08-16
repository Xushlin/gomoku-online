## ADDED Requirements

### Requirement: `IGameRules.Apply` 是走子合法性与胜负判定的唯一入口

`IGameRules` SHALL 提供:

```
MoveApplication Apply(
    IReadOnlyList<PlayedMove> history, MoveIntent intent, Stone side);
```

规则 MUST 自行完成:形状校验(该棋种要不要 `From`)、越界、目标格合法性、走法合法性、
以及走完之后的 `GameResult`。非法走子 MUST 抛 `InvalidMoveException`,且 MUST NOT 产生副作用
—— 规则实例是无状态的,同一个实例被并发的多个房间共享。

`history` 是本局已走的全部步,按 `Ply` 升序。规则从它重建自己需要的表示。

**聚合根 MUST NOT 再自行判断盘面。** `Room.PlayMove` 在调用本方法之前只做三件事:房间在不在
对局中、这人是不是玩家、是不是他的回合。越界、重复落子、走法是否合规,全部 MUST 由本方法回答。

传历史而不是传一个盘面对象:后者会让聚合根重新知道「有一个盘面」,只是换了个名字,而盘面要么
冗余存盘(第二份真源)、要么每次重放(那就是现在的做法)。每步 O(n) 重放在这个量级上是亚毫秒的,
而且**今天的 `Game.ReplayBoard` 已经在这么做**。

#### Scenario: 合法落子返回 Ongoing
- **WHEN** 对空盘调 `Apply([], MoveIntent(null, (7,7)), Black)`(五子棋)
- **THEN** 返回 `Result == Ongoing`

#### Scenario: 越界由规则拒绝
- **WHEN** 对一字棋调 `Apply([], MoveIntent(null, (3,0)), Black)`
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 重复落子由规则拒绝
- **WHEN** 历史里 (0,0) 已有子,再对 (0,0) 落子
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 无状态
- **WHEN** 同一个规则实例被两段不同的历史先后调用
- **THEN** 两次结果只取决于各自的 `history`,MUST NOT 互相影响

### Requirement: `MoveIntent.From` 可空,形状由规则校验

`Gewu.Domain` SHALL 定义:

```
public readonly record struct MoveIntent(Position? From, Position To);
public readonly record struct PlayedMove(Position? From, Position To, Stone Side);
public readonly record struct MoveApplication(GameResult Result);
```

`From` 为 `null` 表示**落子类**棋种的一步(五子棋 / 一字棋:只有落点);非 `null` 表示
**走子类**棋种的一步(中国象棋:从哪儿到哪儿)。

规则 MUST 校验形状:落子类棋种收到非 `null` 的 `From` MUST 抛 `InvalidMoveException`,
走子类棋种收到 `null` 的 `From` 同样 MUST 抛。**这条校验属于规则,不属于聚合根** ——
聚合根不知道哪些棋种走子。

MUST NOT 用一个合法值(例如 `From == To`)表示「没有起点」:那样读代码的人看到 `from == to`
得猜这是原地不动还是落子,而 `null` 说的是实话。

#### Scenario: 落子类拒绝带起点的走子
- **WHEN** 对五子棋调 `Apply([], MoveIntent((0,0), (1,1)), Black)`
- **THEN** 抛 `InvalidMoveException` —— 五子棋没有「从哪儿走」

#### Scenario: 历史保留起点
- **WHEN** 一步走子类的棋被记录
- **THEN** `PlayedMove.From` 非 `null`,重放时能还原

### Requirement: `INInARowRules` 承载连 N 子专有成员

`Gewu.Domain` SHALL 定义 `INInARowRules : IGameRules`,承载 `int WinLength { get; }` 与 `Board CreateBoard()`。

`IGameRules` 本体 MUST NOT 再有这两个成员 —— 中国象棋没有「连几子」,`CreateBoard()` 返回的
`Board` 它也不用。留在基接口上,象棋就得实现两个骗人的成员,而骗人的实现是下一个人删不掉的
(他无从知道有没有调用方)。

这与 `IGameRules` 上那条既有门槛注释是同一条纪律的另一面:**接口只承载对每个实现都成立的东西。**

n-in-a-row 的 AI 工厂 MUST 接 `INInARowRules`;走子类棋种自带表示,不经过 `Board`。

#### Scenario: 五子棋实现窄接口
- **WHEN** 检视 `NInARowRules`
- **THEN** 它实现 `INInARowRules`,`WinLength` / `CreateBoard()` 仍然可用

#### Scenario: 基接口上没有连子概念
- **WHEN** 反射检视 `IGameRules` 的成员
- **THEN** MUST NOT 含 `WinLength` 或 `CreateBoard`
