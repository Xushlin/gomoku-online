# game-rules-registry Specification Delta

## MODIFIED Requirements

### Requirement: `IGameRules.Apply` 是走子合法性与胜负判定的唯一入口

`IGameRules` SHALL 提供:

```
MoveApplication Apply(
    IReadOnlyList<PlayedMove> history, MoveIntent intent, int seat);
```

规则 MUST 自行完成:形状校验(该棋种要不要 `From`)、越界、目标格合法性、走法合法性、
以及走完之后的 `GameResult`。非法走子 MUST 抛 `InvalidMoveException`,且 MUST NOT 产生副作用
—— 规则实例是无状态的,同一个实例被并发的多个房间共享。

判出胜负时,规则 MUST 同时给出**赢家的座位号**(`MoveApplication.WinnerSeat`)。它 MUST NOT 被当成
"走这一步的人"的同义词:落子类棋种里赢家恒等于走子方,但那是**那些棋种**的性质,不是接口的性质。

`history` 是本局已走的全部步,按 `Ply` 升序。规则从它重建自己需要的表示。

**聚合根 MUST NOT 再自行判断盘面。** `Room.PlayMove` 在调用本方法之前只做三件事:房间在不在
对局中、这人是不是玩家、是不是他的回合。越界、重复落子、走法是否合规,全部 MUST 由本方法回答。

传历史而不是传一个盘面对象:后者会让聚合根重新知道「有一个盘面」,只是换了个名字,而盘面要么
冗余存盘(第二份真源)、要么每次重放(那就是现在的做法)。每步 O(n) 重放在这个量级上是亚毫秒的。

**第三个参数是座位号而不是 `Stone`。** 这一条自 `generalize-match-seats` 起为真,而本 spec 在那次
改动之后仍然写着 `Stone side` —— 那次的 delta 只改了 `IGameRules` 那条 requirement,没有回头看
同一个文件里第二处描述同一个方法签名的地方。`openspec validate --specs --strict` 当时 38/38 全绿,
**因为它校验的是 spec 的形状,不是 spec 的真假。**

#### Scenario: 合法落子返回 Ongoing
- **WHEN** 对空盘调 `Apply([], MoveIntent.Place((7,7)), 0)`(五子棋)
- **THEN** 返回 `Result == Ongoing`、`WinnerSeat == null`

#### Scenario: 越界由规则拒绝
- **WHEN** 对一字棋调 `Apply([], MoveIntent.Place((3,0)), 0)`
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 重复落子由规则拒绝
- **WHEN** 历史里 (0,0) 已有子,再对 (0,0) 落子
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 判胜时说出赢家座位
- **WHEN** 落子类棋种的一步造成连 N 子,由座位 `s` 走出
- **THEN** 返回 `Result == Decided` 且 `WinnerSeat == s`

#### Scenario: 无状态
- **WHEN** 同一个规则实例被两段不同的历史先后调用
- **THEN** 两次结果只取决于各自的 `history`,MUST NOT 互相影响

### Requirement: `MoveIntent.From` 可空,形状由规则校验

`Gewu.Domain` SHALL 定义:

```
public readonly record struct MoveIntent(Position? From, Position? To, string? Text);
public readonly record struct PlayedMove(Position? From, Position? To, string? Text, int Seat);
public readonly record struct MoveApplication(GameResult Result, int? WinnerSeat);
```

`From` 为 `null` 表示**落子类**棋种的一步(五子棋 / 一字棋:只有落点);非 `null` 表示
**走子类**棋种的一步(中国象棋:从哪儿到哪儿)。两种载荷(位置 / 文本)的互斥不变量由
`room-and-gameplay` 的「一步棋要么是位置,要么是文本」那条 requirement 定义,本条 MUST NOT 复述它
—— 本 spec 此前写的是 `MoveIntent(Position? From, Position To)`,即 `generalize-match-payload`
之前的签名,而那次改动新增了一条正确的 requirement 却把这条错的留在原地。**同一个事实被两条
requirement 描述、其中一条是旧的,是这个仓库反复付账的那个形状。**

规则 MUST 校验形状:落子类棋种收到非 `null` 的 `From` MUST 抛 `InvalidMoveException`,
走子类棋种收到 `null` 的 `From` 同样 MUST 抛。**这条校验属于规则,不属于聚合根** ——
聚合根不知道哪些棋种走子。

`MoveApplication.WinnerSeat` MUST 非 `null` 当且仅当 `Result == Decided`,**由构造器强制**。
`Ongoing` / `Draw` 带一个赢家、或 `Decided` 不带赢家,都 MUST 在构造时抛异常,而 MUST NOT 只写在
文档里 —— 与上面那条互斥载荷同一种机制,同一个理由。

#### Scenario: 落子类拒绝带起点的走子
- **WHEN** 对五子棋调 `Apply([], MoveIntent(from: (0,0), to: (1,1)), 0)`
- **THEN** 抛 `InvalidMoveException` —— 五子棋没有「从哪儿走」

#### Scenario: 历史保留起点
- **WHEN** 一步走子类的棋被记录
- **THEN** `PlayedMove.From` 非 `null`,重放时能还原

#### Scenario: 结果与赢家必须一致
- **WHEN** 构造 `MoveApplication(GameResult.Ongoing, 0)` 或 `MoveApplication(GameResult.Decided, null)`
- **THEN** 构造 MUST 失败并抛异常
