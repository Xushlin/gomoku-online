## MODIFIED Requirements

### Requirement: `MakeMoveCommandHandler` 在对局结束时同事务更新两位玩家的 Rating 与战绩

`MakeMoveCommand` handler 在 `Room.PlayMove` 返回 `MoveOutcome.Result != GameResult.Ongoing` **且该房间的棋种 `IsRated == true`** 时 MUST 执行:

1. 用 `IUserRepository.FindByIdAsync` 加载 `BlackPlayerId` 和 `WhitePlayerId` 对应的两位 `User`
2. 按 `Result` 推导双方 `GameOutcome`:
   - `BlackWin`:黑 `Win`、白 `Loss`
   - `WhiteWin`:黑 `Loss`、白 `Win`
   - `Draw`:黑 `Draw`、白 `Draw`
3. 调 `EloRating.Calculate(black.Rating, black.GamesPlayed, white.Rating, white.GamesPlayed, outcomeForBlack)`
4. `black.RecordGameResult(outcomeForBlack, newBlackRating)` 与 `white.RecordGameResult(outcomeForWhite, newWhiteRating)`
5. **同一次** `IUnitOfWork.SaveChangesAsync` 提交 —— Room.Game / Room.Status / 两位 User 的变更在同一事务

`Result == Ongoing` 时 handler MUST NOT 查询 / 修改 `User`。

**棋种不计分时同样 MUST NOT 查询 / 修改 `User`**(本变更新增约束)。判定取自
`IGameRules.IsRated`,规则实例已经由 handler 为落子解析出来,MUST NOT 为此再查一次注册表,
更 MUST NOT 内联一份"哪些棋种算分"的清单。

不计分的棋种依然 MUST 正常结束对局:`Room.Status` 进 `Finished`、`Game.EndReason` 照常写入、
`GameEndedAsync` 照常广播、回放照常可查。**只有 ELO 与战绩不动** —— 一局棋是否算分,不影响
它是否是一局棋。

理由见 `add-tictactoe` design D2:平台此刻只有一个评分池,它实质上就是五子棋排行榜;
让一字棋结果推动它会无声地污染平台唯一的排行榜。且一字棋是已解游戏,完美对弈必和、
Hard 档不可战胜,评分在其上收敛为噪声。这一条是限期约束,`add-per-game-rating` 给每个棋种
发独立 `UserGameStats` 之后 MUST 被重写。

**bot 参与的对局不做特殊处理**:若黑 / 白任一方为 bot(`IsBot == true`),handler MUST 照常调 `RecordGameResult` 更新其 Rating 与战绩。理由:防止"bot Rating 永远 1200 被真人反复刷分"的套利(见 add-ai-opponent design.md D7)。该约束只在计分棋种上有意义 —— 不计分棋种上双方都不动,套利无从谈起。

Handler 调 `IRoomNotifier` 的时序保持原 Requirement(`RoomStateChangedAsync` → `MoveMadeAsync` → `GameEndedAsync`);Rating 变更不单独广播。

#### Scenario: 非结束局不触动 User
- **WHEN** `outcome.Result == Ongoing`
- **THEN** Handler MUST NOT 调 `IUserRepository.FindByIdAsync` / `User.RecordGameResult`

#### Scenario: 真人 vs 真人 黑胜
- **WHEN** `outcome.Result == BlackWin`,对局前黑方 `(Rating=1200, GamesPlayed=0)`,白方 `(Rating=1200, GamesPlayed=0)`,两人都是真人,棋种为 `gomoku`
- **THEN** Handler 调 `EloRating.Calculate(1200, 0, 1200, 0, Win)`;`black.Rating == 1220`,`white.Rating == 1180`;`black.Wins == 1`,`white.Losses == 1`

#### Scenario: 真人打赢 bot
- **WHEN** 黑方是真人(`Rating=1200, GamesPlayed=0`),白方是 bot(`Rating=1200, GamesPlayed=0`, `IsBot=true`),真人黑胜,棋种为 `gomoku`
- **THEN** Handler 照常调 `RecordGameResult`;**bot 也被更新**:`bot.Rating == 1180`、`bot.Losses == 1`、`bot.GamesPlayed == 1`;真人 `Rating == 1220`

#### Scenario: 单 SaveChanges
- **WHEN** 对局结束路径完整跑一遍
- **THEN** `IUnitOfWork.SaveChangesAsync` MUST 被调用恰好 **一次**

#### Scenario: 不计分棋种结束对局不动评分
- **WHEN** 一局 `tictactoe`(`IsRated == false`)以 `BlackWin` 结束
- **THEN** `Room.Status == Finished`、`Game.EndReason` 已写入、`GameEndedAsync` 已广播;而 Handler MUST NOT 调 `IUserRepository.FindByIdAsync` / `User.RecordGameResult`,双方 `Rating`、`Wins`、`Losses`、`GamesPlayed` 全部不变

#### Scenario: 不计分棋种的对局仍可回放
- **WHEN** 上述一字棋对局结束后调 `GET /api/rooms/{id}/replay`
- **THEN** 正常返回完整 moves 与元数据 —— 不计分 MUST NOT 削弱对局记录

#### Scenario: 排行榜不受一字棋影响
- **WHEN** 若干局一字棋结束后调 `GET /api/leaderboard`
- **THEN** 名次与 Rating 与这些对局发生之前完全一致
