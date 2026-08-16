# add-xiangqi-ai

## Why

象棋今天没有任何对手 —— 人人对战要等大厅泛化，所以 AI 是它第一次真正能被下的途径。

## 又是同一个问题，高了一层

`IBoardGameAi.SelectMove(Board board, Stone myStone) → Position` 有两条硬假设：

1. **吃的是 `Board`** —— 那是连 N 子专用的表示（它带着 `WinLength` 和 `PlaceStone`）。
2. **返回一个 `Position`** —— 假设一步棋就是「落在某格」。

象棋两条都不满足。这正是 `generalize-match-domain` 在规则那一层解决过的问题，只是它当时
没往上看一层。

**那个接口的注释此前写着「它从来就没用到任何五子棋专属的东西」——那句话是错的。**
它是 `add-tictactoe` 把 `IGomokuAi` 改名成 `IBoardGameAi` 时写的，而一字棋证明不了这件事：
它也是落子类、也用 `Board`。**一字棋是缩小版五子棋，它验证不了泛用性** —— 这与
`add-tictactoe` 自己的审计结论（「规则花了零行」）是同一件事的两面。

## What Changes

外层接缝改成与规则同形：

```
MoveIntent SelectMove(IReadOnlyList<PlayedMove> history, Stone myStone);
```

**既有五个 AI 实现一行不改。** 它们保留 `Board` 版签名，收进一个新的窄接口 `IPlacementAi`，
由一个适配器包成 `IBoardGameAi`：适配器调 `INInARowRules.ReplayBoard(history)` 造盘、
把返回的 `Position` 包成 `MoveIntent.Place(...)`。

这么做是因为那五个实现背后有一批很值钱的测试 —— 尤其一字棋 Hard 档那套**穷举**验证
（对每一个可达局面断言它落在博弈论最优值上）。为了换签名重写它们，是拿一份已经证明过的
东西去换一次纯机械改动的风险。

象棋 AI 自带表示：`XiangqiAi` 走限深 alpha-beta，三档难度。

## Impact

- Domain：`IBoardGameAi` 换签名；新增 `IPlacementAi` + 适配器；新增 `Games/Xiangqi/XiangqiAi*`
- `XiangqiRules` 需要对外给出**合法着法枚举**（AI 要它），此前是私有的
- Application：`ExecuteBotMoveCommandHandler` 不再自己造盘 —— 它把历史交给 AI
- Infrastructure：一处 DI 注册
- 无迁移、无 API、无前端

## 不做的事

- **不做 Web**（`add-web-xiangqi`）。象棋没有 UI，本变更之后它仍然只能被测试和 API 调用。
- **不开人人对战**，因此 `SupportsHumanVsHuman` 仍是 `false`、仍不计分。
  有了 AI 之后「要不要计分」才成为一个活的问题 —— 而不变量已经答了一半：
  只有机器人对手的棋种不该有阶梯（一字棋那条理由原样适用）。
