## Context

`add-ai-opponent` 为 Easy(随机)+ Medium(自赢 → 堵五 → 中心启发分)两档。Medium 在人类 2-3 步内就能被识破(不看多步威胁、不识别活三)。想让 gomoku 的"多难度"承诺真正兑现,要加一档带**博弈树搜索**的 AI。

15×15 棋盘搜索空间是 225^(depth)级别的完全树,所以必须做两件事:
1. **候选剪枝**:只看"已有子附近 2 格内的空格",候选从 ~200 降到 10-30;
2. **α-β 剪枝**:标配,典型剪掉 50-80% 的子树。

配合一个简单的模式识别评估函数(长度-封 / 活组合),深度 2 就能表现出"看一步"的水平,下 gomoku 对多数人类来说是明显挑战。

## Goals / Non-Goals

**Goals**:
- 比 Medium 明显强:必看一步前瞻,能识别对手活三并堵死,能识别自己的潜在活三并利用。
- 单步决策延迟 < 50ms,不给 `AiMoveWorker` 轮询带来压力。
- 算法纯函数、Domain 纯依赖、测试固定种子可复现 —— 与 Easy/Medium 的约定一致。
- 下游改动极小:只动 `GomokuAiFactory` 一个 case + `BotAccountIds` 一个常量 + 一条 seed migration。

**Non-Goals**:
- 顶级水平(VCT / VCF 专用搜索、iterative deepening、transposition table、killer moves)。
- 禁手规则(活三-活三、双四、长连禁)—— 标准 gomoku 玩法,不为 bot 改规则。
- 可配置深度:`HardAi` 构造参数 `searchDepth` 有默认值 2,但**只供测试使用**,生产不暴露配置项。
- 下一档"更 hard"的 AI:留给后续 `add-ai-opponent-master`。
- Self-play 训练 / 神经网络:完全不在规划。

## Decisions

### D1 — Minimax + α-β,深度 2

**深度 2** 的意思:`HardAi` 看一步己方 → 一步对方 → 取对己方最大的分值。具体:

- 顶层(maximizing):对每个己方候选 `c`,`Board.Clone().PlaceStone(c, my)` → `Minimax(clonedBoard, depth=1, isMax=false, α=-∞, β=+∞, my)` → 取最大。
- 第二层(minimizing):对每个对方候选 `c2`,`clone.PlaceStone(c2, opp)` → `Minimax(clone, depth=0, isMax=true, α, β, my)` → `Evaluate(clone, my)`;对第二层的每个对方候选取最小(对手目标:己方分数最小化)。
- 叶子:`depth=0` 或终局(任一方连五)→ 返回 `Evaluate(board, my)` 或 ±∞。

**为什么深度 2 而不是 3**:
- 深度 3 = 己方 → 对方 → 己方第二步,搜索规模 30^3 = 27000 node,每 node O(225) 评估 → 6M 操作,单步 ~ 100ms。虽然仍在 AiMoveWorker 可接受范围,但 p99 延迟不稳定(候选数 / 评估命中率波动)。
- 深度 2 规模 30^2 = 900 node,~200k 操作,单步 < 10ms p99。可预测。
- 深度 2 仍能识别"对手下一步能连五 / 我方下一步能连五",以及"对手活三活四威胁",足以覆盖 80% 关键局面。

**考虑过但弃用**:
- Iterative deepening(1 → 2 → 3 直到超时)—— 实现复杂度显著上升,收益在 depth 2 的单步延迟已经足够低的前提下有限。留给 Master 档。

### D2 — 候选生成:已有子 2 格邻域

`GenerateCandidates(board)`:
- 空盘:只返回 `[(7,7)]`(中心特判;避免 225 个候选全随机分散)。
- 非空盘:枚举所有已有子的位置,取其 Chebyshev 距离 ≤ 2 的空格(一个子周围 5×5 = 25 格,减去自己 = 24 格,多子重叠后去重)。
- 典型 10-30 个候选。

距离 2(而非 1)是业界惯例:距离 1 会漏掉"跳步连三"(如 `X.XX` 的中空点),这类是活三威胁的常见形。

**代价**:空盘之后的第一步可能落在对手 2 格内以"逼迫",而非远处布局 —— 对 Hard 档可接受(远处布局不是 gomoku 的常见策略)。

### D3 — 评估函数(简单模式识别)

`Evaluate(board, myStone)`:
- 沿 4 方向(水平 / 垂直 / 主对角 / 反对角)扫描所有长度 ≥ 2 的同色**连续**段(不含跳);
- 对每段识别两端是否"封"(对方子 / 边界)还是"活"(空格);
- 打分表:

  | 模式 | 己方分 | 对方分(乘 -1.1) |
  |---|---|---|
  | 活四(两端空,4 连) | +10000 | -11000 |
  | 冲四(一端封,4 连) | +1000 | -1100 |
  | 活三(两端空,3 连) | +500 | -550 |
  | 眠三(一端封,3 连) | +100 | -110 |
  | 活二(两端空,2 连) | +50 | -55 |
  | 眠二(一端封,2 连) | +10 | -11 |

- 对手分乘 -1.1:**偏防守**(同等威胁优先堵)。这是 gomoku 实战惯例 —— 进攻失败还有下一手,防守失败直接败局。

**不做**:
- 跳步模式识别(`X.XX`、`XX.XX`等):实现上要扫描"掩码",单步复杂度明显上升。深度 2 + 连续段识别已能覆盖主要威胁;将来 Master 档再加。
- 双活三 / 双冲四奖励:叠加 effect 自然在 score 累加里体现(两条活三 → 两次 +500 = +1000),不特判。

### D4 — 终局短路

Minimax 的叶子之前加终局判断:若当前 board 上任一方刚刚连五,立即返回 ±100000(己方胜 +,对方胜 -),不用算 Evaluate。

实际上"当前是否连五"在上一层的 `PlaceStone` 返回值里已能读到(`GameResult.BlackWin` / `WhiteWin`),Minimax 直接用 PlaceStone 的返回 `result` 判终局,不再重新扫描 Board。这是性能关键。

### D5 — `HardAi` 构造参数

```csharp
public HardAi(Random random, int searchDepth = 2)
```

- `random`:用于并列打破(分相同的多个候选)。同 Easy/Medium 的注入模式。
- `searchDepth`:默认 2,公有,为**单元测试**能构造 `searchDepth=1`(退化到"只看自己一步")做对比测试。生产 `GomokuAiFactory.Create(Hard, _)` **只用默认值 2**。

### D6 — `BotAccountIds.Hard` 固定 Guid

按现有模式(`Easy = ...ea51`,`Medium = ...bed10`),挑一个好记的 hex:

```csharp
public static readonly Guid Hard = Guid.Parse("00000000-0000-0000-0000-000000000ha2d");
```

等下,`h`/`a`/`d`/`2` 不是合法 hex 字符("h"非法)。改用:

```csharp
// "hard" 的近似写法,只用合法 hex {0..9, a..f}
public static readonly Guid Hard = Guid.Parse("00000000-0000-0000-0000-0000000000ad");
```

`..0ad` 是"hard" 去掉 h 后的音形近似(a-r-d → a-d)。可读性稍弱于 Easy/Medium,但作为系统常量足够。

### D7 — Migration 仅 seed 第 3 行 bot

`AddHardBotAccount` migration:`migrationBuilder.InsertData(table: "Users", ...)`,单行。列:
- `Id = BotAccountIds.Hard`
- `Email = "hard@bot.gomoku.local"`
- `Username = "AI_Hard"`
- `PasswordHash = "__BOT_NO_LOGIN__"`
- `Rating = 1200`、`GamesPlayed=0`、`Wins=0`、`Losses=0`、`Draws=0`
- `IsActive = 1`、`IsBot = 1`
- `CreatedAt = '2026-01-01 00:00:00'`(UTC)
- `RowVersion = <16 bytes>`(`add-concurrency-hardening` 引入的列,必须显式填;migration 里给一个固定 16 字节,后续 RecordGameResult 会自然推进)

Down:`DeleteData` 同一行。

## Risks / Trade-offs

| 风险 | 影响 | 缓解 |
|---|---|---|
| 深度 2 + 评估函数偶尔选"看似蠢"的招 | 用户感觉 Hard "没很强" | 接受 —— 目标不是顶级;若批评强烈,后续 `add-ai-opponent-master` 升级 |
| 评估函数的常量权重(10000 / 1000 / 500...)基于直觉 | 可能过度防守或过度进攻 | 测试覆盖典型局(活三堵 / 自赢连五),线上迭代调参;不做预先 tuning |
| Minimax 无 memoization / transposition table,同局面重算 | 深度 2 下同局面重复率低,可忽略 | 深度上升时再考虑 |
| `AiMoveWorker` 的 MinThinkTimeMs=800 对 Hard 仍然 sufficient | Hard 决策 < 10ms,MinThinkTime 是主要耗时 —— 延迟体验与 Easy/Medium 一致 | N/A |
| 搜索偶尔选中一个远离战局的候选(启发式未覆盖)| 评估分相等时 Random 破平可能选出"战略冷门" | 候选已限于已有子 2 格内,"冷门" ≠ "空盘中心",仍属战局附近 |
| Migration seed 的 Guid 与 dev 库已有真人冲突 | 概率 ≈ 2^-120 | 忽略 |

## Migration Plan

- `AddHardBotAccount` migration:`InsertData` 一行。
- 老库运行后:第 3 个 bot 出现,`GET /api/leaderboard` 仍过滤所有 IsBot,前端无感。
- 回滚:`DeleteData` 干净移除。

## Open Questions

无 —— 搜索深度、评估权重、候选剪枝距离都有 design 依据,默认即可实施。
