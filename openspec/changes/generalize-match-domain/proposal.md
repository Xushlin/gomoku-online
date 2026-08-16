# generalize-match-domain

## Why

中国象棋是第一个 `Room` 聚合装不下的对战棋种。今天的 `Room.PlayMove` 直接知道棋盘怎么工作：

```csharp
if (!rules.IsInBounds(position)) throw ...;
var board = Game.ReplayBoard(rules);          // Board = 连 N 子棋盘
var result = board.PlaceStone(new Move(position, playerStone));
```

三处硬假设，象棋一条都不满足：

1. **一步棋只有落点。** `Move` 是 `(Ply, Row, Col, Stone)`。象棋的一步是「从哪儿到哪儿」。
2. **格子只有空/黑/白。** `Stone` 三值。象棋一格上是七种棋子之一 × 两方。
3. **胜负由「刚落的子连成 N 个」判定。** 象棋是将死 / 困毙，和最后一步的位置没有直接关系。

一字棋没暴露这些，因为它就是小一号的五子棋 —— 这一点 `add-tictactoe` 的审计说得很清楚：
规则花了**零行**，贵的是注册表欠债。象棋反过来：注册表已经就位，贵的是聚合根里这三条假设。

## What Changes

**`Room` 不再知道棋盘怎么工作。** 它只做它该做的事：谁的回合、这人是不是玩家、对局结不结束。
盘面语义整个下沉进 `IGameRules`。

- 新增值对象 `MoveIntent(Position? From, Position To)` 与 `PlayedMove(Position? From, Position To, Stone Side)`。
  **`From` 可空** —— 落子类棋种（五子棋 / 一字棋）没有起点，走子类（象棋）有。
- `IGameRules` 新增 `Apply(history, intent, side)`，返回 `MoveApplication(GameResult)`；
  非法走子由**规则**抛 `InvalidMoveException`。
- `IGameRules` 上只留游戏中立的东西。`CreateBoard()` / `WinLength` 下沉到新接口
  `INInARowRules : IGameRules` —— 象棋没有「连几子」，也没有 `Board`；
  让它实现两个对自己无意义的成员，就是把今天的 `WinLength` 之痒变成两处。
- `Move` 实体新增可空的 `FromRow` / `FromCol`。
- `GameEndReason.Connected5` → `Decided`（游戏中立）。

## Impact

- Domain：`Room` / `Game` / `Move` / `IGameRules` / `NInARowRules`；新增三个值对象与 `INInARowRules`
- Application：`MakeMoveCommand` 带上可选 `FromRow` / `FromCol`
- Infrastructure：一条迁移（`Moves` 加两列，可空 → 纯增量）
- Api：Hub 与 controller 的 move 载荷加两个可选字段
- Web：`GameEndReason` 联合类型改一个成员名 + 一个 i18n key

**不做的事**，各有理由：

- **不引入 JSON 走子载荷。** 路线图写着「two seats + JSON move payloads」，但象棋每一步都恰好是
  `from → to`：没有王车易位、没有吃过路兵、没有升变。`from → to` 两个可空列覆盖两类棋种，
  而且**列还是可查询的**、EF 原生映射、replay 仍然是强类型的。
  JSON 是为一个今天不存在、且象棋也不会带来的需求付钱。真出现不规则走子（国际象棋）再说。
- **不动座位。** 路线图把「two seats」和走子载荷绑在一起，但 `BlackPlayerId` / `WhitePlayerId`
  已经就是两个座位。象棋是红黑两方，也是两个座位 —— 需要的是**显示层**把黑/白读成红/黑，
  不是聚合根改结构。
- **不动大厅。** 人人对战入口是另一件事。

## Rollout

`Moves` 的两列可空 → `Down` 只丢列，既有数据不受影响。`GameEndReason` 底层值保持 `Decided = 0`，
数据库里存的是 int，**不需要数据迁移** —— 变的只有 JSON 线上的字符串。
本仓库没有生产数据，且 web 与后端同批发布。
