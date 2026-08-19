# game-rules-registry Specification Delta

## MODIFIED Requirements

### Requirement: `IGameRules.Apply` 是走子合法性与胜负判定的唯一入口

`IGameRules` SHALL 提供:

```
public readonly record struct MatchState(string? Setup, IReadOnlyList<PlayedMove> History);

MoveApplication Apply(MatchState state, MoveIntent intent, int seat);
```

规则 MUST 自行完成:形状校验(该棋种要不要 `From`)、越界、目标格合法性、走法合法性、
以及走完之后的 `GameResult`。非法走子 MUST 抛 `InvalidMoveException`,且 MUST NOT 产生副作用
—— 规则实例是无状态的,同一个实例被并发的多个房间共享。

判出胜负时,规则 MUST 同时给出**赢家的座位号**(`MoveApplication.WinnerSeat`)。它 MUST NOT 被当成
"走这一步的人"的同义词:落子类棋种里赢家恒等于走子方,但那是**那些棋种**的性质,不是接口的性质。

`state.History` 是本局已走的全部步,按 `Ply` 升序。`state.Setup` 是本局的服务端侧对局设置
(见 `room-and-gameplay` 的 `Game.Setup`),不需要设置的棋种恒为 `null`。规则从这两者重建自己
需要的表示。

#### 状态是一个记录,而不是两个平铺的参数

`Apply(history, setup, intent, seat)` 有四个参数,其中两个是**这局到目前为止的状态**、两个是
**这一步**。四个平铺的参数要求读代码的人记住顺序;`Apply(state, intent, seat)` 按它们实际的用法
分组。

**这不是为将来的扩展付钱** —— 本 spec 已经拒绝过那条理由(不加 JSON 载荷列,因为"一个成语是
一个标量")。这里的理由是可读性:`state` 是一个有名字的东西,而 `(history, setup)` 是两个碰巧
相邻的参数。

#### `Setup` 到得了规则,到不了客户端

`state.Setup` 让规则读得到发牌,而那条「任何 DTO 都不得有名字含 `Setup` 的成员」的反射断言
**不变**。这是同一条平台规则的两半:规则在服务端,所以它可以知道;客户端不能。

**聚合根 MUST NOT 再自行判断盘面。** `Room.PlayMove` 在调用本方法之前只做三件事:房间在不在
对局中、这人是不是玩家、是不是他的回合。越界、重复落子、走法是否合规,全部 MUST 由本方法回答。

传状态而不是传一个盘面对象:后者会让聚合根重新知道「有一个盘面」,只是换了个名字,而盘面要么
冗余存盘(第二份真源)、要么每次重放(那就是现在的做法)。每步 O(n) 重放在这个量级上是亚毫秒的。

第三个参数是座位号而不是 `Stone`。

#### Scenario: 合法落子返回 Ongoing
- **WHEN** 对空盘调 `Apply(new MatchState(null, []), MoveIntent.Place((7,7)), 0)`(五子棋)
- **THEN** 返回 `Result == Ongoing`、`WinnerSeat == null`

#### Scenario: 越界由规则拒绝
- **WHEN** 对一字棋调 `Apply(new MatchState(null, []), MoveIntent.Place((3,0)), 0)`
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 重复落子由规则拒绝
- **WHEN** 历史里 (0,0) 已有子,再对 (0,0) 落子
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 判胜时说出赢家座位
- **WHEN** 落子类棋种的一步造成连 N 子,由座位 `s` 走出
- **THEN** 返回 `Result == Decided` 且 `WinnerSeat == s`

#### Scenario: 不需要设置的棋种收到的 Setup 恒为 null
- **WHEN** 一个不实现 `IDealtGameRules` 的棋种被调用
- **THEN** `state.Setup == null`

#### Scenario: 需要设置的棋种收到开局那份设置
- **WHEN** 一个实现 `IDealtGameRules` 的棋种被调用
- **THEN** `state.Setup` 恰好是 `Game.Setup`,一字不改

  这一条是本变更存在的理由:`add-match-setup` 把设置存下来了,而**规则拿不到它** ——
  一个存下来再也没人读的值。

#### Scenario: 无状态
- **WHEN** 同一个规则实例被两个不同的 `MatchState` 先后调用
- **THEN** 两次结果只取决于各自的 `state`,MUST NOT 互相影响
