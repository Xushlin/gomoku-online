# Tasks — add-game-capabilities

> 本变更对运行时行为的改动**只有一处**：`NInARowRules` 多校验一条不变量。
> 一字棋依旧不计分、五子棋依旧计分，两者数值一分不差 —— 这是判断本变更是否
> 越界的标准。跑完测试后 653 个既有断言应当一条不改地全绿（新增的除外）。

## 1. 能力声明

- [x] 1.1 `IGameRules` 增加 `bool SupportsHumanVsHuman { get; }`。
- [x] 1.2 重写 `IsRated` 的文档注释：理由改为「本棋种没有有意义的对手池」，拆除条件改为「该棋种获得人人对战之后」。**点名当前的注释是错的**，并说明为什么 —— 下一个读到它的人需要知道那句话被改过。
- [x] 1.3 在 `IGameRules` 文档里写下门槛：这类平台能力声明超过三个时应抽成 `GameCapabilities`。
- [x] 1.4 **不要**加 `SupportsAi`。加一条测试断言 `IGameRules` 上不存在该成员（反射即可）—— 光在文档里写"别加"挡不住下一个人。

## 2. 不变量

- [x] 2.1 `NInARowRules(gameKey, rows, cols, winLength, supportsHumanVsHuman = true, isRated = true)`。
- [x] 2.2 构造器校验 `IsRated ⇒ SupportsHumanVsHuman`，违反抛 `ArgumentException`。
- [x] 2.3 `BuiltInGameRules.TicTacToe` 改为 `supportsHumanVsHuman: false, isRated: false`；`Gomoku` 不动（两者取默认 `true`）。
- [x] 2.4 遍历注册表的不变量测试 —— 对**每一个**注册的 `IGameRules` 断言，而不是只测那两个已知的。将来加象棋时它自动被覆盖。
- [x] 2.5 测试：`(supportsHumanVsHuman: false, isRated: true)` 构造失败；默认值是 `true` / `true`。

## 3. 改正三处错注记

- [x] 3.1 `IGameRules.IsRated` 的 XML 注释（§1.2 已含）。
- [x] 3.2 `game-rules-registry` spec —— 由本变更的 delta 负责。
- [x] 3.3 `UnratedGameEloTests` 的类注释：现在写着「`add-per-game-rating` 让每个棋种各算各的之后，`IsRated` 连同本文件一起删除」。那句话错了两处 —— 文件不会被删（不计分的路径依然要测），拆除条件也不是那个变更。
- [x] 3.4 `CLAUDE.md` roadmap 第 1 项现在写着「**This change must delete `IGameRules.IsRated`**」。改正，并写明为什么原来的判断是错的 —— 不要只改结论，那样下一个人会重新得出同一个错判断。

## 4. Ship

- [x] 4.1 `dotnet build` 干净。
- [x] 4.2 `dotnet test` 全绿。**核对既有断言无一需要修改** —— 若有，说明本变更越界了，停下来看为什么。
- [x] 4.3 `openspec validate add-game-capabilities --strict`。
- [x] 4.4 PR 描述写明：这是 `add-per-game-rating` 的前置一半，为什么要拆，以及那个错承诺的来龙去脉。
