## 1. Domain — `BotDifficulty.Hard`

- [x] 1.1 `Gomoku.Domain/Ai/BotDifficulty.cs` 追加 `Hard = 2` + XML 注释("使用 Minimax α-β 两层搜索 + 活三 / 活四 / 冲四等模式识别评估函数")。

## 2. Domain — `HardAi`

- [x] 2.1 `Gomoku.Domain/Ai/HardAi.cs`,实现 `IGomokuAi`:
  - 构造 `HardAi(Random random, int searchDepth = 2)`;`random == null` 抛 `ArgumentNullException`;`searchDepth < 1` 抛 `ArgumentOutOfRangeException`。
  - `SelectMove(Board, Stone myStone)`:
    - 若 `myStone == Empty` 抛 `ArgumentOutOfRangeException`(与 Easy/Medium 一致)。
    - 若棋盘空:返回 `(7, 7)`。
    - 生成候选 = `GenerateCandidates(Board)`(下述)。
    - 对每个候选:Clone 并 PlaceStone(myStone) —— 若 `PlaceStone` 返回 `GameResult` 对应己方胜,**立即**返回该候选(剪枝)。
    - 否则调 `Minimax(clonedBoard, searchDepth - 1, isMax: false, α: int.MinValue, β: int.MaxValue, myStone, opponentStone)` 得到 score。
    - 取 score 最大的候选;并列用 `random` 破平。
  - `GenerateCandidates(Board)`:空盘返回 `[(7,7)]`;否则枚举所有已有子,取其 8 邻域扩展到**距离 2**(共 24 格的 5×5 方块)内的所有空格,去重。
  - `Minimax(Board, depth, isMax, α, β, myStone, oppStone)`:
    - 如果 depth 为 0:返回 `Evaluate(Board, myStone)`。
    - 生成候选。对每候选:Clone + PlaceStone(当前行动方色);若 PlaceStone 返回己方胜 → 按 `isMax` 返回 `+∞`/`-∞`(±100000)。
    - 递归 `Minimax(clone, depth-1, !isMax, α, β, my, opp)`;按 isMax 更新 bestScore + α/β;α ≥ β 时剪枝退出。
    - 返回 bestScore。
  - `Evaluate(Board, myStone)`:遍历 4 方向的所有长度 ≥ 2 的同色连续段,识别"活四 / 冲四 / 活三 / 眠三 / 活二 / 眠二",按 design.D3 表打分;对方模式乘 -1.1;累加;返回 int。
  - 所有 helper 方法 `private static`;纯函数,无 IO。

## 3. Domain — Factory 扩展

- [x] 3.1 `Gomoku.Domain/Ai/GomokuAiFactory.cs` 的 `Create(BotDifficulty, Random)` 加 `Hard => new HardAi(random)` 分支(默认 searchDepth=2)。
- [x] 3.2 现有 `Create(invalid enum)` → `ArgumentOutOfRangeException` 的路径保持,只多一个 case。

## 4. Domain 测试

- [x] 4.1 `Gomoku.Domain.Tests/Ai/HardAiTests.cs`:
  - 空盘首手选 (7,7)
  - 能连五立即选(黑 (7,3)..(7,6) 活四 → 选 (7,2) 或 (7,7))
  - 对手能连五必堵(白 (7,3)..(7,6) 4 连,黑无法自赢 → 黑选 (7,2) 或 (7,7))
  - **识别对手活三并阻止**:对手 `.XXX.`(白在 (7,5)(7,6)(7,7) 活三),Hard 黑方必堵
    两端之一(形成对手"眠三")
  - **识别并形成自己双活三**:己方布局使选中后形成两条活三(例:黑在 (7,7) / (8,8),
    选 (6,6) 后 `(6,6)(7,7)(8,8)` 主对角活三 + `(6,6)` 附近的水平 / 反对角活三)
  - 从不选已有子位置(1000 次采样断言)
  - 入参 Board 不被修改(调用前后逐格对比)
  - `myStone == Empty` 抛 ArgumentOutOfRangeException
  - `random == null` 抛 ArgumentNullException
  - `searchDepth < 1` 抛 ArgumentOutOfRangeException
  - **固定种子复现**:两次 `new HardAi(new Random(42))` 对同盘面返回相同 `Position`
  ~12 tests。
- [x] 4.2 `Gomoku.Domain.Tests/Ai/GomokuAiFactoryTests.cs` 追加:
  - `Create(BotDifficulty.Hard, rng)` 返回 `HardAi` 实例。
  - `Create((BotDifficulty)99, rng)` 仍抛(回归)。
- [x] 4.3 `dotnet test tests/Gomoku.Domain.Tests`:预期 217 → 229(+12 HardAi + 1 Factory)全绿。

## 5. Application — `BotAccountIds.Hard`

- [x] 5.1 `Gomoku.Application/Abstractions/BotAccountIds.cs`:
  - 追加 `public static readonly Guid Hard = Guid.Parse("00000000-0000-0000-0000-0000000000ad");`
  - `For(BotDifficulty)` 的 switch 追加 `Hard => Hard`。
  - `TryGetDifficulty(Guid)` 追加 `if (userId == Hard) return BotDifficulty.Hard;`。

## 6. Application 测试

- [x] 6.1 现有 `CreateAiRoomCommandHandlerTests` 追加一个用例 `CreateAiRoom_Hard_Difficulty_Succeeds`:
  - mock `FindBotByDifficultyAsync(BotDifficulty.Hard, _)` 返回 `NewBot(Hard)`;
  - 断言返回 `RoomStateDto.White.Username == "AI_Hard"`。
- [x] 6.2 `RoomsFixtures.NewBot(BotDifficulty)` 已处理 Hard(代码通用,但需要 Username 模板 `"AI_Hard"` 与 enum ToString 一致,无需改)。
- [x] 6.3 `dotnet test tests/Gomoku.Application.Tests`:预期 89 → 90(+1)全绿。

## 7. Infrastructure — Migration(seed 第 3 bot)

- [x] 7.1 `dotnet ef migrations add AddHardBotAccount --project src/Gomoku.Infrastructure --startup-project src/Gomoku.Api --output-dir Persistence/Migrations`
- [x] 7.2 编辑生成的 migration:
  - Up:`migrationBuilder.InsertData("Users", columns..., values..)` 插入一行 AI_Hard bot。RowVersion 列给一个固定 16 字节(`new byte[] { 0x48, 0x61, 0x72, 0x64, ... 零填 }` 或 `Guid.Parse("...").ToByteArray()` 拿 16 字节)。
  - Down:`migrationBuilder.DeleteData("Users", "Id", <BotAccountIds.Hard 的 Guid 字面量>);`
- [x] 7.3 `dotnet ef database update`;验证 `SELECT Username_Value, IsBot FROM Users WHERE IsBot=1` 返回 3 行(含新的 AI_Hard)。

## 8. 端到端冒烟

- [x] 8.1 启动 Api,注册 Alice;`POST /api/rooms/ai { name: "hard-smoke", difficulty: "Hard" }`:
  - 201 + `RoomStateDto`,`White.Username == "AI_Hard"`,`Status == "Playing"`。
- [x] 8.2 SignalR 连上;Alice 落 (7,7)(黑);等 worker 发 AI 的一手:
  - Hard 的候选必在 (7,7) 周围 2 格内;实际落点可能是 (7,8) 或 (8,7) 之类。
- [x] 8.3 人为摆一个**活三给 Hard 堵**的局面:
  - (用几次 MakeMove 让 Hard 走 1-2 步后,布置白方 `. ? ? ? .` 模式 —— 注意 Alice 是黑,只能下黑子。换角度:Alice 布黑方活三 `(7,3)(7,4)(7,5)` 三连、`(7,2)` `(7,6)` 两端空;此时 Hard (白) 必须下 `(7,2)` 或 `(7,6)` 堵 —— 测它真的做到。
  - 观察:Hard 确实选了封堵点(而不是别处扩张);verification 手工 inspect SignalR `MoveMade` 事件。
- [x] 8.4 打到分胜负(Alice 不认输的话,Hard 深度 2 应能至少阻止人类草率赢棋;若人类认真 3-5 步可胜 Hard,仍可接受)。
- [x] 8.5 `GET /api/leaderboard`:3 个 bot 都**不**在榜(既有过滤逻辑)。

## 9. 归档前置检查

- [x] 9.1 `dotnet build Gomoku.slnx`:0 警告 / 0 错。
- [x] 9.2 `dotnet test Gomoku.slnx`:全绿(Domain 217 → 229;Application 89 → 90)。
- [x] 9.3 Domain csproj 仍 0 PackageReference / 0 ProjectReference。
- [x] 9.4 Application csproj 无 EF / Hosting / Hub 新依赖。
- [x] 9.5 `openspec validate add-ai-opponent-hard --strict`:valid。
- [x] 9.6 分支 `feat/add-ai-opponent-hard`,按层分组 commit(Domain / Application / Infrastructure / docs-openspec 四条)。
