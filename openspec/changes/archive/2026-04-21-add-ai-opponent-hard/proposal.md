## Why

`add-ai-opponent` 交付了 Easy + Medium 两档 AI,但 proposal 里明说"Hard 难度(Minimax / α-β 搜索 / 复杂威胁识别) 下一轮 `add-ai-opponent-hard`"—— 这就是那一轮。Medium 只做"自赢 → 堵五 → 中心启发分"三层,人类稍有经验就能赢,不能称为"多难度"承诺中的高档选手。补上 Hard:

- 真正的博弈树搜索(Minimax + α-β);
- 识别"活三 / 冲四 / 活四"等威胁模式,转为评估分数;
- Seed 第 3 个 bot 账号,前端的难度下拉菜单多一条。

目标不是"顶级 AI"(那要做 VCF/VCT 搜索、transposition table、iterative deepening、killer move 等,工作量翻倍)。目标是"认真下棋的人要专注才能赢"的水准。

## What Changes

- **Domain**:
  - `BotDifficulty` 枚举追加 `Hard = 2`。底层整数值固定(现有 Easy=0 / Medium=1 保留不变)。
  - 新文件 `Gomoku.Domain/Ai/HardAi.cs` 实现 `IGomokuAi`:
    - 构造参数 `(Random random, int searchDepth)`,默认 `searchDepth=2`(两层全展开)。
    - `SelectMove`:
      1. 生成候选集 `GenerateCandidates(board)` —— "当前所有已有子周围 2 格内的空格"(启发式剪枝,空盘特判只返回 (7,7));典型活跃对局 10-30 个候选。
      2. 对每个候选做 `Board.Clone + PlaceStone` 试走 → 若直接连五(`myStone` 胜)立即返回(同 MediumAi 第一层)。
      3. 对每个候选做 `Minimax(clone, searchDepth-1, maximizingPlayer=false, α=-∞, β=+∞, myStone)` 获得 `score`。
      4. 返回分最高的候选;并列用 `Random` 破平。
    - `Minimax(board, depth, isMax, α, β, myStone)`:
      - 终局判断:若任一颜色连五 → 返回 ±∞;否则 depth==0 → 返回 `Evaluate(board, myStone)`。
      - 展开当前候选集;对每个候选递归 `Minimax(depth-1, !isMax, ...)`;α-β 剪枝。
    - `Evaluate(board, myStone)`:沿四方向扫描所有长度 ≥ 2 的同色连续段,按段长和两端"封 / 活"打分。打分表:
      - 长度 5 → +100000(已赢,理论上不到此处)
      - 活四(两端空)→ +10000
      - 冲四(一端空)→ +1000
      - 活三(两端空)→ +500
      - 眠三(一端空)→ +100
      - 活二 → +50;眠二 → +10
      - 对手对等段落同样扫描后乘 -1.1(略偏防守)。
    - 纯函数:不修改 Board,不读外部状态;测试可用固定种子复现。
  - `GomokuAiFactory.Create(BotDifficulty, Random)` 追加 `BotDifficulty.Hard` → `new HardAi(random, searchDepth: 2)` 分支。
- **Application**:
  - `BotAccountIds`:追加 `public static readonly Guid Hard = Guid.Parse("00000000-0000-0000-0000-00000000...");`(design 里定值)+ `For(Hard)` / `TryGetDifficulty(Hard guid)` 覆盖。
- **Infrastructure**:
  - Migration `AddHardBotAccount`:seed 一行 bot `AI_Hard`,固定 Guid,email `hard@bot.gomoku.local`,PasswordHash = `__BOT_NO_LOGIN__`,`IsActive=true`、`IsBot=true`、Rating=1200、其它计数器 0,`CreatedAt=2026-01-01T00:00:00Z`。
  - **不**改 `UserConfiguration`。
- **Api**:
  - 零改动。`POST /api/rooms/ai` 接受 `{"difficulty": "Hard"}` 字符串(`JsonStringEnumConverter` 自动转);`CreateAiRoomCommand` 通过已有 `BotAccountIds.For(Hard)` 路径找到 bot。
- **Tests**:
  - `Gomoku.Domain.Tests/Ai/HardAiTests.cs`:
    - 空盘首手选 (7,7)(与 Medium 对齐,启发式首选中心)
    - 自赢即连五(与 Easy/Medium 一致)
    - 必堵对手 4 连
    - **识别并阻止对手活三**(Medium 不做,Hard 的关键价值):布 `.XXX..` 对手黑子,Hard 白方 MUST 选将其堵死的点(`.XXX.O` 或 `OXXX.`)
    - 识别己方"双活三"优势:两条活三的交点应被选中
    - 固定种子可复现(同一 Random seed + 同盘面 → 同选择)
    - 从不选已有子位置(1000 次采样)
    - 入参 Board 不被修改(调用前后 snapshot 相等)
    - `myStone = Empty` 抛 `ArgumentOutOfRangeException`
  - `Gomoku.Domain.Tests/Ai/GomokuAiFactoryTests.cs`:追加 "Hard 分支返回 HardAi 实例"。
  - `BotAccountIdsTests`:追加 `For(Hard)` / `TryGetDifficulty(Hard)` 覆盖。
  - `Gomoku.Application.Tests/Features/Rooms/RoomsFixtures.cs`:`NewBot(Hard)` 也工作。
  - 现有 `CreateAiRoomCommandHandlerTests`:追加一个用例"创建 Hard AI 房间成功"。

**显式不做**(留给后续变更):
- 更深的搜索(`searchDepth=3`+ 加 killer moves / transposition table)—— 留给 `add-ai-opponent-master`。
- VCF / VCT(四连 / 三连必胜搜索)—— 专门的 gomoku 技术,留给后续。
- 禁手规则(五连以上为长连禁手、黑方活三-活三、双四等):让 Hard 按标准 gomoku 玩,不引入规则差异。
- 并行 / 多线程搜索:单线程同步足够。
- AI 自学(Alpha-Zero 式的 self-play 训练):根本不在 scope。
- 前端难度下拉是前端职责:只要端点接受 `"Hard"` 字符串即可。

## Capabilities

### New Capabilities

(无)

### Modified Capabilities

- **`ai-opponent`** — `BotDifficulty` 枚举追加 `Hard=2`;新 `HardAi` 实现(Minimax/α-β + 威胁识别评估函数);`GomokuAiFactory` 新 case;`BotAccountIds.Hard` 固定 Guid;数据库新增第 3 个 bot seed;HTTP 端点 `POST /api/rooms/ai` 的 `difficulty` 参数值域扩展为 `"Easy" | "Medium" | "Hard"`。

## Impact

- **代码规模**:~7 新文件(HardAi + tests + migration)+ 若干小改。比 `add-timeout-resign` 小,比 `add-concurrency-hardening` 大。
- **NuGet**:零。
- **HTTP 表面**:零新端点;`POST /api/rooms/ai` 的 difficulty 取值扩充一项。
- **SignalR 表面**:零变化。
- **数据库**:`Users` 表多一行 bot seed,无新列;`AddHardBotAccount` migration 仅 `InsertData`。
- **运行时**:AI 决策路径走新 `HardAi`;`AiMoveWorker` 每次处理 Hard 局耗时从 Medium 的 µs 级上升到 ms 级(depth=2,30 候选 → ~900 node,每 node 一次评估 O(225) 扫描 → 20 万操作,现代 CPU < 10 ms)。对 worker 1.5s 轮询间隔毫无压力。
- **后续变更将依赖**:`add-ai-opponent-master`(更强的 Hard+,或新 Master 档)、`add-ai-selftune`(学习式)。
