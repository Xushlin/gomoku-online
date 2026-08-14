## Why

`add-tictactoe` 加了 `IGameRules.IsRated`，并在**三个地方**写明「`add-per-game-rating` 会删掉它」：字段的 XML 注释、`game-rules-registry` 的 spec requirement、`UnratedGameEloTests` 的类注释，外加 `CLAUDE.md` 的 roadmap。

开始做 `add-per-game-rating` 时我去核对那个承诺，发现**它是错的**。

`IsRated` 当初有两个理由：① 一字棋会污染平台唯一的评分池；② 一字棋的评分没有意义。per-game 评分解决 ①，但 ② 没解决，而且更尖锐了 —— **一字棋没有人人对战**，唯一的对手是机器人，而机器人对局是计分的（`ai-opponent` 的反套利约束）。所以一字棋阶梯的榜首会是刷 Easy 档最多的人：Hard 必和分不动，Easy 稳赢分单调涨。那不是噪声，是一个可见且可刷的错误信号。

**但真正的问题不是这个结论，是字段的形状。** `IsRated` 是一个手工维护的布尔，语义是「要不要给这个棋种算分」—— 一个**判断**。判断会过期，而过期的判断不会报错：一字棋将来有了人人对战，得有人**记得**回来翻它。没人记得的那天，代码和现实静默分岔。

我上一轮就是这个坑的受害者：在三处写下待办，然后自己在下一个变更里才发现待办本身是错的。**注释里的待办事项不是机制。**

## What Changes

### `IGameRules` 增加 `SupportsHumanVsHuman`，`IsRated` 受不变量约束

```
SupportsHumanVsHuman : bool      结构性事实 —— 这个棋种有没有人类对手池
IsRated              : bool      判断，但受不变量约束
不变量：IsRated ⇒ SupportsHumanVsHuman
```

不变量由**两处**强制，不只写在文档里：

- `NInARowRules` 构造器违反时抛 `ArgumentException` —— 在构造处失败，而不是等某个 handler 算出一个没人该看的分数。
- 一条遍历注册表的测试，对每一个已注册的 `IGameRules` 断言它成立。

于是：五子棋两者皆 `true`；一字棋 `SupportsHumanVsHuman = false`，**因此** `IsRated` 只能是 `false` —— 不再是谁的判断。

### `SupportsAi` 不加

对称地声明「支不支持人机」是诱人的，但 `IGameAiRegistry.For(gameKey)` 已经知道答案 —— 注册了 AI 工厂就是支持。加个字段等于第二份真源，而这个仓库已经为「两份真源迟早不一致」付过两次学费（建房校验不许内联白名单；前端 manifest 的 `board` 是刻意副本，且只因失配症状肉眼可见、服务端还兜底才被接受）。`SupportsAi` 没有那两个安全网。

### 三处错注记 + roadmap 同步改正

`IsRated` 的拆除条件从「`add-per-game-rating`」改为「**该棋种获得人人对战之后**」，并把理由从「怕污染共享池」改写为「本棋种没有有意义的对手池」—— 前者在 `add-per-game-rating` 之后就不成立了，而留着一个理由已失效的开关，是最容易变成永久设施的东西。

## Scope

**只做能力声明与不变量。** `UserGameStats`、迁移、分棋种排行榜全部留给 `add-per-game-rating`。

这一刀是为了让那个变更能被审：`add-per-game-rating` 要删掉 `User` 上五个战绩字段、写一条含数据搬迁的迁移、改掉全部读者，本身就已经远超 400 行约定。把能力模型混进去，会让「一字棋该不该有排行榜」和「战绩表怎么迁」变成同一次审查里的同一个问题。它们不是。

本变更**没有** `UserGameStats`，所以它对运行时行为的改变**只有一处**：`NInARowRules` 多校验一条不变量。一字棋依旧不计分，五子棋依旧计分，两者的数值一分不差。

## Impact

- **Affected specs:** `game-rules-registry`（MODIFIED ×2）。
- **Affected code:** `Gewu.Domain/Games/Abstractions/IGameRules.cs`、`Gewu.Domain/Games/NInARow/NInARowRules.cs`、`backend/tests/**`（不变量测试 + 既有断言）、`CLAUDE.md`。
- **无迁移、无 DTO 变更、无 wire 变更。** 纯 Domain + 文档。
- **Out of scope:** per-game 评分、`UserGameStats`、分棋种排行榜、大厅泛化。
