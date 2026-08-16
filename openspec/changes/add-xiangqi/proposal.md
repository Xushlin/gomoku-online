# add-xiangqi

## Why

`generalize-match-domain` 把盘面语义整个下沉进了 `IGameRules`，理由就是象棋。现在兑现它。

本变更**只做规则**。AI 与 Web 是各自独立的一步 —— 象棋规则本身就是这个平台上最大的一块领域逻辑
（七种棋子、蹩马腿、塞象眼、炮架、九宫、过河兵、将帅照面、将死与困毙），
把它和 AI、UI 挤进一个 PR 会让这三样都得不到该有的审查。

## What Changes

- `XiangqiRules : IGameRules`，自带棋子表示与走法判定。**不实现 `INInARowRules`** —— 它没有「连几子」。
- 棋子模型 `XiangqiPiece(XiangqiPieceType Type, Stone Side)`，盘面 `XiangqiBoard`（10×9）**内部于规则**，
  聚合根看不到它。
- 注册进 `IGameRulesRegistry`，`SupportsHumanVsHuman: false` / `IsRated: false`
  —— 今天平台还没有它的任何入口，这两个值说的是**结构性事实**，不是判断。

## 红先：`Stone.Black` 读作红方

`Game` 初始化 `CurrentTurn = Stone.Black`，而象棋是**红先**。

所以本棋种里 **`Stone.Black` ≡ 红方，`Stone.White` ≡ 黑方**。Domain 一行都不用改 ——
`Stone` 在这里的含义本就是「先手方 / 后手方」，红黑是**显示层**怎么画它。
这正是 `generalize-match-domain` 说「红/黑是显示层的读法，不是聚合根改结构」时押的那一注。

代价是读代码时容易绊一下，所以它在 `XiangqiRules` 的 doc comment 里写死，并且有测试钉着。

## Impact

- Domain：新增 `Games/Xiangqi/*`；**`Room` / `Game` / `Move` 一行不改**
- Infrastructure：一处 `AddSingleton`
- 无迁移、无 API 改动、无前端改动

## 顺带修一个假的测试

`NInARowRulesTests.AllBuiltInRules()` 的注释写着「遍历注册表而不是只测那两个已知的棋种 ——
将来加中国象棋…它自动被覆盖」。**那句话是假的**：数据源是一份手写清单
`{ Gomoku, TicTacToe }`。象棋会静静地绕过 `IsRated ⇒ SupportsHumanVsHuman` 这个不变量测试。

这正是那条注释自己预言的失效方式，而它预言错了自己的机制。改成从
`BuiltInGameRules.All` 取，同一份清单也供 DI 注册使用 —— 于是「登记一个棋种」只有一个地方。

## 不做的事

- **不做 AI**（`add-xiangqi-ai`）。
- **不做 Web**（`add-web-xiangqi`）。
- **不做长将 / 长捉判负、六十回合和棋**。这些是比赛规则而非基本走法，实现要维护重复局面历史与
  「捉」的定义，体量接近本变更的一半，而在没有任何入口的今天一局都跑不到。
  记为缺口 —— 缺的是和棋判定，**不是**任何一步会被判成合法的错误。
- **不做人人对战入口**。那是大厅泛化，独立一步；`SupportsHumanVsHuman: false` 如实反映今天。
