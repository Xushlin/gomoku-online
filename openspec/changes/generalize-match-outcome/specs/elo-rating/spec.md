# elo-rating Specification Delta

## MODIFIED Requirements

### Requirement: `MakeMoveCommandHandler` 在对局结束时同事务更新两位玩家的 Rating 与战绩

`MakeMoveCommand` handler 在 `Room.PlayMove` 返回 `MoveOutcome.Result != GameResult.Ongoing` **且该房间的棋种 `IsRated == true`** 时 MUST 执行:

1. 用 `IUserRepository.GetOrCreateGameStatsAsync(userId, gameKey, ct)` 取两位玩家在**该棋种**上的
   `UserGameStats` —— 首局时该行还不存在,由仓库以初始值(`Rating = 1200`,战绩全 0)建出来。
   "第一次下这个棋种"是常态而不是异常,所以这里是 get-or-create 而不是 find-or-throw。
2. 按 `Result` 与 `WinnerUserId` 推导双方 `GameOutcome`:
   - `Decided` 且 `WinnerUserId == blackId`:黑 `Win`、白 `Loss`
   - `Decided` 且 `WinnerUserId == whiteId`:黑 `Loss`、白 `Win`
   - `Draw`:黑 `Draw`、白 `Draw`

   **胜负 MUST 从 `WinnerUserId` 读,MUST NOT 从结果值的取值里读。** 此前是
   `GameResult.BlackWin => Win`,那要求结果枚举本身携带颜色 —— 而同一个事实已经在
   `WinnerUserId` 里了。`Decided` 而 `WinnerUserId` 不等于任何一位玩家时 MUST 抛,MUST NOT
   猜一方获胜。
3. 调 `EloRating.Calculate(blackStats.Rating, blackStats.GamesPlayed, whiteStats.Rating, whiteStats.GamesPlayed, outcomeForBlack)`
   —— 两个 `GamesPlayed` 都是**该棋种**的局数,所以 K 因子按该棋种的资历分段(见 K 因子那条)。
4. `blackStats.RecordGameResult(outcomeForBlack, newBlackRating)` 与 `whiteStats.RecordGameResult(outcomeForWhite, newWhiteRating)`
5. **同一次** `IUnitOfWork.SaveChangesAsync` 提交 —— Room.Game / Room.Status / 两行 `UserGameStats`
   的变更(含首局时新建的行)在同一事务

`Result == Ongoing` 时 handler MUST NOT 查询 / 修改 `UserGameStats` —— 尤其 MUST NOT 为一局还没结束的
棋**创建**战绩行,否则"下过这个棋种"的含义会从"下完过"变成"点开过",而排行榜的成员资格正是靠它。

**棋种不计分时同样 MUST NOT 查询 / 修改 `UserGameStats`。** 尤其 MUST NOT 调
`GetOrCreateGameStatsAsync` —— 它会**建行**,而建出来就等于把人登记进了那个棋种的排行榜。判定取自
`IGameRules.IsRated`,规则实例已经由 handler 为落子解析出来,MUST NOT 为此再查一次注册表,
更 MUST NOT 内联一份"哪些棋种算分"的清单。

不计分的棋种依然 MUST 正常结束对局:`Room.Status` 进 `Finished`、`Game.EndReason` 照常写入、
`GameEndedAsync` 照常广播、回放照常可查。**只有 ELO 与战绩不动** —— 一局棋是否算分,不影响
它是否是一局棋。

不计分的理由**不再是"平台只有一个共享评分池"**:`add-per-game-rating` 已经给每个棋种发了独立的
`UserGameStats`。今天的理由是 `IGameRules.IsRated` 那条不变量所说的 —— 一个没有人类对手池的棋种,
它的阶梯排出来的是"谁刷弱档刷得多"。

> 上面这段此前写的是「平台此刻只有一个评分池,它实质上就是五子棋排行榜……这一条是限期约束,
> `add-per-game-rating` 给每个棋种发独立 `UserGameStats` 之后 MUST 被重写」。而那段文字是
> **`add-per-game-rating` 自己写进这条 requirement 的** —— 它把整条 requirement 重写了一遍,
> 却把"本段必须由本次改动重写"这句话留在里面。**一个把自己指定为自己拆除条件的段落,在触发条件
> 与它同处一次改动时最容易活下来。**

**bot 参与的对局不做特殊处理**:若黑 / 白任一方为 bot(`IsBot == true`),handler MUST 照常调 `RecordGameResult` 更新其 Rating 与战绩。理由:防止"bot Rating 永远 1200 被真人反复刷分"的套利(见 add-ai-opponent design.md D7)。该约束只在计分棋种上有意义 —— 不计分棋种上双方都不动,套利无从谈起。

Handler 调 `IRoomNotifier` 的时序保持原 Requirement(`RoomStateChangedAsync` → `MoveMadeAsync` → `GameEndedAsync`);Rating 变更不单独广播。

#### Scenario: 非结束局不触动战绩
- **WHEN** `outcome.Result == Ongoing`
- **THEN** Handler MUST NOT 调 `IUserRepository.GetOrCreateGameStatsAsync` / `UserGameStats.RecordGameResult`

#### Scenario: 真人 vs 真人 先手胜
- **WHEN** `outcome.Result == Decided` 且 `WinnerUserId` 是黑方,对局前黑方 `(Rating=1200, GamesPlayed=0)`,白方 `(Rating=1200, GamesPlayed=0)`,两人都是真人,棋种为 `gomoku`
- **THEN** Handler 调 `EloRating.Calculate(1200, 0, 1200, 0, Win)`;`black.Rating == 1220`,`white.Rating == 1180`;`black.Wins == 1`,`white.Losses == 1`

#### Scenario: 真人打赢 bot
- **WHEN** 黑方是真人(`Rating=1200, GamesPlayed=0`),白方是 bot(`Rating=1200, GamesPlayed=0`, `IsBot=true`),真人黑胜,棋种为 `gomoku`
- **THEN** Handler 照常调 `RecordGameResult`;**bot 也被更新**:`bot.Rating == 1180`、`bot.Losses == 1`、`bot.GamesPlayed == 1`;真人 `Rating == 1220`

#### Scenario: 单 SaveChanges
- **WHEN** 对局结束路径完整跑一遍
- **THEN** `IUnitOfWork.SaveChangesAsync` MUST 被调用恰好 **一次**

#### Scenario: `Decided` 而赢家不是任何一位玩家
- **WHEN** `outcome.Result == Decided` 而 `WinnerUserId` 为 `null` 或不属于两位玩家
- **THEN** Handler MUST 抛,MUST NOT 任选一方判胜

#### Scenario: 不计分棋种结束对局不动评分
- **WHEN** 一局 `tictactoe`(`IsRated == false`)以 `Decided` 结束
- **THEN** `Room.Status == Finished`、`Game.EndReason` 已写入、`GameEndedAsync` 已广播;而 Handler MUST NOT 调 `IUserRepository.GetOrCreateGameStatsAsync` / `UserGameStats.RecordGameResult`,且 MUST NOT 有任何 `UserGameStats` 行被创建或修改

#### Scenario: 不计分棋种的对局仍可回放
- **WHEN** 上述一字棋对局结束后调 `GET /api/rooms/{id}/replay`
- **THEN** 正常返回完整 moves 与元数据 —— 不计分 MUST NOT 削弱对局记录
