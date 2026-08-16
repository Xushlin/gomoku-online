# Tasks — add-xiangqi-ai

> 判据：**既有五个落子类 AI 实现一行不改，它们的测试一条不改。**
> 尤其一字棋 Hard 档那套穷举验证 —— 那是一份已经证明过的东西，不该拿去换一次机械改动。

## 1. 接缝

- [x] 1.1 `IBoardGameAi.SelectMove(history, myStone) → MoveIntent`。
- [x] 1.2 `IPlacementAi` 承载原 `Board` 版签名；适配器用 `INInARowRules.ReplayBoard` 造盘、包成 `Place`。
- [x] 1.3 既有五个实现改为实现 `IPlacementAi` —— **只改 `: IBoardGameAi` 这一处**，方法体不动。
- [x] 1.4 两个既有工厂返回适配器。

## 2. 象棋着法枚举

- [x] 2.1 `XiangqiRules.LegalMoves(history, side)` 转公开；`HasAnyLegalMove` 改为用它，别留两份枚举。
- [x] 2.2 测试：开局非空、每条都能被 `Apply` 接受、无着法时与判负一致。

## 3. 象棋 AI

- [x] 3.1 评估函数：子力价值 + 简单位置项。
- [x] 3.2 alpha-beta，限深，**吃子优先排序**。排序不是可选优化：alpha-beta 的剪枝量完全
      取决于顺序，不排时 Hard 一步要约 1.7 秒（自对弈 12 步 21 秒），排完约 750ms（9 秒）。
      750ms 正好落在 `AiMoveWorker` 既有的 800ms 最小思考时间之内 —— 也就是说它在真实对局里
      不会让人等，这才是这个数字的意义所在。
- [x] 3.3 三档：Easy 随机合法着法（偏好吃子）、Medium 深度 2、Hard 深度 4。
- [x] 3.4 `XiangqiAiFactory` + DI。

## 4. Application

- [x] 4.1 `ExecuteBotMoveCommandHandler` 不再自己造盘 —— 交历史给 AI，拿 `MoveIntent` 回来。
- [x] 4.2 它因此不再需要 `INInARowRules` 那次 cast。

## 5. 测试

- [x] 5.1 三档都只走合法着法（对若干局面各跑一遍，逐条送进 `Apply`）。
- [x] 5.2 白送的子会被吃（Medium / Hard）。
- [x] 5.3 被将时走出的仍是合法着法（等价于「会解将」——不解将的着法根本不合法）。
- [x] 5.4 无合法着法时抛 `InvalidOperationException`。
- [x] 5.5 不修改入参历史；同随机源可复现。
- [x] 5.6 既有 AI 经适配器与直接调用结果一致。

## 6. 验收

- [x] 6.1 `dotnet build` 0 warning、`dotnet test` 全绿。
- [x] 6.2 **我写的判据太满了，改正一下。** 「既有 AI 测试零改动」没有做到，也做不到：
      两个文件需要适配，各一处。
      - `GameAiFactoryTests`：断言现在穿过一层 `PlacementAiAdapter`（测什么没变：哪个难度拿到哪个实现）。
      - `TicTacToeMediumAiTests`：一个局部变量的类型 `IBoardGameAi` → `IPlacementAi`。
      **真正要保住的那份保住了**：五个 AI 实现的方法体一行没动，
      `TicTacToeHardAiTests` 那套**穷举**验证与 `HardAiTests` 完全没碰。
      判据该写成「实现与穷举验证零改动」，而不是「所有 AI 测试零改动」。
- [x] 6.3 `openspec validate add-xiangqi-ai --strict`。

## 7. 已知缺口（记录，不在本变更修）

- [ ] 7.1 **不声称任何一档「不可战胜」。** 象棋不可能穷举，那种断言既做不到也验不了 ——
      与一字棋 Hard 档的穷举验证是两回事，不该照着写。
- [ ] 7.2 **无开局库、无置换表、无迭代加深。** 都是提棋力的常规手段，但今天没有 UI，
      没人能感受到差别；先让它能下且合法。
- [ ] 7.3 **象棋仍不计分**（`SupportsHumanVsHuman: false`）。有了 AI 之后「要不要计分」
      才成为活问题，而一字棋那条理由原样适用：只有机器人对手的阶梯排的是刷子次数。

- [ ] 7.4 **Hard 档自对弈那条测试要跑 9 秒**，是整个后端测试里最慢的一条。
      再快下去要么减深度（削弱棋力），要么上置换表 / 迭代加深（本变更范围之外）。
      现在的取舍是：一条 9 秒的测试换「Hard 档真的多看一层」。
- [ ] 7.5 **搜索内部仍然逐点过滤自将**，每个候选着法都克隆一次棋盘。标准做法是搜伪合法着法、
      靠「将帅被吃」的巨大负值兜底 —— 那会再快一截，但会让近将死局面的判断变糙。
      今天没有 UI，没人能感受到那点棋力差，所以先保准确。
