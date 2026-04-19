## MODIFIED Requirements

### Requirement: `IUserRepository.GetTopByRatingAsync(int limit)` 返回按排行榜顺序的用户列表

Application 层 SHALL 在 `IUserRepository` 上新增(已在 add-elo-system 引入):

```
Task<IReadOnlyList<User>> GetTopByRatingAsync(int limit, CancellationToken cancellationToken);
```

实现 MUST:

1. **过滤 `IsBot == false`**(本次新增约束)—— 机器人 User 虽然跟随 ELO 正常更新,但 **MUST NOT** 出现在排行榜返回结果中,避免压低真人榜或被当作"被刷的靶子"占位。
2. 按 `(Rating DESC, Wins DESC, GamesPlayed ASC)` 排序。
3. 取前 `limit` 条。
4. 返回类型 MUST 是领域类型(不泄漏 `IQueryable` / `IOrderedEnumerable` 等 EF 细节)。

过滤 `IsBot` 的语义由仓储层承担,而不是 handler 层:`GetLeaderboardQueryHandler` 对"底下是否过滤 bot"透明,只负责分配 Rank。

#### Scenario: 排序正确(真人)
- **WHEN** 数据库有三位真人用户:A(Rating=1500, Wins=2), B(Rating=1500, Wins=5), C(Rating=1400, Wins=10),调 `GetTopByRatingAsync(100, ct)`
- **THEN** 返回顺序 `[B, A, C]`

#### Scenario: 按 GamesPlayed ASC 作为三级排序
- **WHEN** 两位真人用户 `(Rating=1500, Wins=3, GamesPlayed=10)` 与 `(Rating=1500, Wins=3, GamesPlayed=5)`
- **THEN** 后者(场次少)排前

#### Scenario: 限流
- **WHEN** 数据库有 200 位真人用户,调 `GetTopByRatingAsync(100, ct)`
- **THEN** 返回 100 条

#### Scenario: Bot 被过滤
- **WHEN** 数据库有 5 位真人(Rating 1200~1400)和 2 位 bot(Rating 1500 / 1600),调 `GetTopByRatingAsync(100, ct)`
- **THEN** 返回 5 位**真人**用户,bot 不出现在结果中;排序只在真人间进行

#### Scenario: 仅 bot 的极端情形
- **WHEN** 数据库仅存在 bot 账号(例如刚跑完 migration 还没有真人注册),调 `GetTopByRatingAsync(100, ct)`
- **THEN** 返回空列表,**不**返回 bot 占位项

---

### Requirement: `MakeMoveCommandHandler` 在对局结束时同事务更新两位玩家的 Rating 与战绩

`MakeMoveCommand` handler 在 `Room.PlayMove` 返回 `MoveOutcome.Result != GameResult.Ongoing` 时 MUST 执行:

1. 用 `IUserRepository.FindByIdAsync` 加载 `BlackPlayerId` 和 `WhitePlayerId` 对应的两位 `User`
2. 按 `Result` 推导双方 `GameOutcome`:
   - `BlackWin`:黑 `Win`、白 `Loss`
   - `WhiteWin`:黑 `Loss`、白 `Win`
   - `Draw`:黑 `Draw`、白 `Draw`
3. 调 `EloRating.Calculate(black.Rating, black.GamesPlayed, white.Rating, white.GamesPlayed, outcomeForBlack)`
4. `black.RecordGameResult(outcomeForBlack, newBlackRating)` 与 `white.RecordGameResult(outcomeForWhite, newWhiteRating)`
5. **同一次** `IUnitOfWork.SaveChangesAsync` 提交 —— Room.Game / Room.Status / 两位 User 的变更在同一事务

`Result == Ongoing` 时 handler MUST NOT 查询 / 修改 `User`。

**bot 参与的对局不做特殊处理**(本变更新增约束):若黑 / 白任一方为 bot(`IsBot == true`),handler MUST 照常调 `RecordGameResult` 更新其 Rating 与战绩。理由:防止"bot Rating 永远 1200 被真人反复刷分"的套利(见 add-ai-opponent design.md D7)。

Handler 调 `IRoomNotifier` 的时序保持原 Requirement(`RoomStateChangedAsync` → `MoveMadeAsync` → `GameEndedAsync`);Rating 变更不单独广播。

#### Scenario: 非结束局不触动 User
- **WHEN** `outcome.Result == Ongoing`
- **THEN** Handler MUST NOT 调 `IUserRepository.FindByIdAsync` / `User.RecordGameResult`

#### Scenario: 真人 vs 真人 黑胜
- **WHEN** `outcome.Result == BlackWin`,对局前黑方 `(Rating=1200, GamesPlayed=0)`,白方 `(Rating=1200, GamesPlayed=0)`,两人都是真人
- **THEN** Handler 调 `EloRating.Calculate(1200, 0, 1200, 0, Win)`;`black.Rating == 1220`,`white.Rating == 1180`;`black.Wins == 1`,`white.Losses == 1`

#### Scenario: 真人打赢 bot
- **WHEN** 黑方是真人(`Rating=1200, GamesPlayed=0`),白方是 bot(`Rating=1200, GamesPlayed=0`, `IsBot=true`),真人黑胜
- **THEN** Handler 照常调 `RecordGameResult`;**bot 也被更新**:`bot.Rating == 1180`、`bot.Losses == 1`、`bot.GamesPlayed == 1`;真人 `Rating == 1220`

#### Scenario: 单 SaveChanges
- **WHEN** 对局结束路径完整跑一遍
- **THEN** `IUnitOfWork.SaveChangesAsync` MUST 被调用恰好 **一次**
