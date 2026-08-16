# Tasks — add-klotski

## 1. Domain：盘面与棋子

- [x] 1.1 `Games/Klotski/KlotskiPiece.cs`：`(Id, Row, Col, Height, Width, IsTarget)` + `KlotskiMove`。
- [x] 1.2 `KlotskiBoard.cs`（`internal`）：占用图、`TryMove`、`Signature()`、`Target`。盘面**不可变** —— 走一步返回新盘，搜索因此可以放心地把局面塞进集合里。
- [x] 1.3 布局校验：越界 / 重叠 / 重复 id → `TryCreate` 返回 `null`，调用方当作「不通关」而不是抛。

**没有「人物」字段。** 原本设计过一个 `KlotskiPieceKind` 枚举（曹操/关羽/将/卒），写到一半发现它与几何是两份可以互相矛盾的真相：一枚声明为 `CaoCao` 的 1×2 会让签名和判定各说各话。改成「形状由 `Height`/`Width` 说了算，要送出去的那一枚由 `IsTarget` 说了算」，人物名退回关卡 JSON 里的显示字段，领域层原样忽略。

## 2. Domain：求解器

- [x] 2.1 `KlotskiSolver.cs`：A\*，启发函数为目标子到出口的曼哈顿距离。
- [x] 2.2 签名按**形状**归一化：两枚 1×1 卒交换位置是同一个局面。目标子记一个独有符号，不与同形状的子归并 —— 否则一个含两枚 2×2 的自造关卡会把「送错一枚出去」当成通关。
- [x] 2.3 `Solve` / `DistanceToGoal`；无解返回 `null`；`MaxExpansions` 兜底。
- [x] 2.4 `KlotskiLevels`（public 窄入口）：`Solve` / `MinMoves` / `Replay`，供生成器与测试使用而不必公开整个盘面模型。

**启发函数可采纳**（一步最多让目标子靠近一格 → 永不高估），所以 A\* 求出的是真正的最优步数，而不是「一个还不错的解」。这一点是必需的：`minMoves` 是计分的分母。

## 3. Domain：规则

- [x] 3.1 `KlotskiRules : IPuzzleRules`，`GameKey => "klotski"`。
- [x] 3.2 `Validate`：从 `layoutJson` 重放，全合法且目标子到出口才通关。
- [x] 3.3 `CheckPartial`：前缀是否全合法；为真时附 `{"caoCaoOut": bool}`。
- [x] 3.4 `Hint`：对**上报局面**跑求解器取下一步；缺失/畸形/不合法 → 退回初始布局。
- [x] 3.5 `Score`：步数 / `minMoves` + 提示数，**不看** `Mistakes`、不看用时；坏关卡给 1 星而不抛。

## 4. 关卡

- [x] 4.1 手写 5 份布局，全部以横刀立马为基准派生。
- [x] 4.2 `tools/KlotskiGenerator`：读布局 → 跑 A\* → 写 `backend/data/levels/klotski.json`。
- [x] 4.3 产物提交进仓库。

**算出来的难度梯度**（`minMoves`，一格一步）：

| 关 | 名称 | 派生 | minMoves | 求解耗时 |
| --- | --- | --- | --- | --- |
| 0 | 初识华容 | 去掉全部四卒 | **16** | 38 ms |
| 1 | 四卒当关 | 去掉中间两卒 | **23** | 125 ms |
| 2 | 兵临城下 | 去掉底部两卒 | **27** | 214 ms |
| 3 | 一卒之差 | 去掉一卒 | **53** | 352 ms |
| 4 | 横刀立马 | 经典局面 | **116** | 233 ms |

派生方向保证难度单调（少一枚子 = 更多空格、更少约束），而实测数字确认了这一点 —— 这条推理还被写成了一条测试（`Removing_a_blocker_never_makes_a_layout_harder`），不是设计时的一句话。

**关于 116：** 它是算出来的，不是抄来的。事后对照发现它与公开资料里横刀立马的「单格步数」一致，而广为流传的「81 步」用的是另一种数法（同一枚子连滑算一步）。这个对照是**事后的旁证**，不是数据来源 —— 顺序很重要。

## 5. Infrastructure

- [x] 5.1 `CrosswordLevelSeeder` → `PuzzleLevelSeeder`，游戏键与产物路径变构造参数。
- [x] 5.2 产物文件头的 `seed` / `dictionaryCommit` 改为**可空**：成语纵横的关卡由随机生成器产出，需要种子才能复现；华容道的布局是手写的，没有种子可记。让第二个游戏编造两个值出来才是错的。
- [x] 5.3 DI：`AddSingleton<IPuzzleRules, KlotskiRules>()` + 两个 keyed seeder 实例；`.csproj` 复制产物到输出目录。

## 6. 测试

- [x] 6.1–6.6 共 **37** 条：盘面合法性、重放、求解器、提示、计分、边界。
- [x] 6.7 提示耗时有测试盯着（阈值 3s，实测远低于此）—— 慢了会红，红了就得写进 tasks 而不是悄悄放宽。
- [x] 6.8 seeder 新增两条：**同一个 seeder 能装第二个游戏**（用真的华容道产物），以及**两个游戏的 seeder 互不阻塞**。这两条是「把它改成通用的」那句话的执行形式。

一条被改掉的弱测试:最初写的「封死局面无解」靠我手摆几枚子堵住曹操,能不能真堵住取决于我猜得对不对 —— 一条只在我猜对时才成立的断言不是断言。改成**把盘面塞满**(曹操 4 格 + 16 枚卒 = 20 格,零空格),无解在结构上就是确定的。

## 7. 验证

- [x] 7.1 `dotnet build Gewu.slnx` —— **0 error, 0 warning**。
- [x] 7.2 `dotnet test Gewu.slnx` 全绿：Domain 551 / Application 210 / Infrastructure 80 = **841**（本变更前 802）。
- [x] 7.3 `openspec validate add-klotski --strict` 通过。
- [x] 7.4 HTTP 冒烟走通全程(独立进程 + 全新 scratch 数据库)。

### 7.5 冒烟里最有信息量的一条

脚本**不自己算解**,而是一路跟着服务端的提示走。结果:第 0 关跟着提示走了 **16 步到出口,而这一关的 minMoves 恰好是 16**。

也就是说每一次提示都真的落在一条最短路径上 —— 这比「提示返回了一个合法着法」强得多,而且它是端到端验的:经过真的 HTTP、真的数据库、真的尝试计数。提交回来 `stars: 1, hintsUsed: 16`,进度 `unlockedLevelIndex: 1, totalStars: 1`。

顺带纠正一处**我自己的预期错误**:重复提交同一个已完成的尝试返回 **409 Conflict**,我在脚本里写的是 400。服务端是对的 —— 「这个尝试已经结束了」是冲突,不是请求格式错误。改的是脚本。

### 7.6 顺带修掉的一个平台依赖

生成器用 `WriteIndented = true`,而 **.NET 9 起它默认取 `Environment.NewLine`** —— 同一份布局在 Windows 上生成 CRLF、在 Linux 上生成 LF。这份产物是提交进仓库的,而且它的内容会**原样进数据库**(`GetRawText()`)。显式设 `NewLine = "\n"`。

## 8. 归档前必答

- [x] **8.1 `git diff --name-only` 里有没有 puzzle-core 的文件?**

  **没有。** 完整清单里 `Gewu.Domain/Puzzles/`、`Gewu.Application/Features/Puzzles/`、`PuzzlesController` 一个都没有出现。改动集中在:一个新的 `Games/Klotski/` 目录、两行 DI、一个通用化的 seeder、一个工具、一份关卡产物。

  `generalize-puzzle-rules` 抽在了对的地方,而这一次检验它的是**真实的第二个游戏**,不是一个照着第一个游戏形状写的 fake。

- [x] **8.2 求解器实测。** 见 §4 的表:最慢一关 352 ms。提示走的是同一条路径,所以一次提示的上界就是这个数。它落在「玩家主动发起、还要扣一颗星」的操作可以接受的范围内,但**不是快**,如实记下来。

## 9. 遗留

- **关卡产物以缩进 JSON 存进数据库**(`layoutJson` 带换行与空格),第 0 关的布局因此是 1.7 kB 而不是约 0.5 kB。这是既有行为 —— 成语纵横的关卡完全一样 —— 不是本变更引入的,但两个游戏都在为可读的产物付传输的钱。要修就在 seeder 里统一压缩,属于 puzzle-core 的活。
- **没有 UI。** 华容道在目录页仍是「即将上线」,这是诚实的状态。`add-web-klotski` 补它。
