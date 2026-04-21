# elo-rating Specification

## Purpose
TBD - created by archiving change add-elo-system. Update Purpose after archive.
## Requirements
### Requirement: `GameOutcome` 枚举表达"一方视角下的一局对局结果"

系统 SHALL 定义 `enum GameOutcome { Loss = 0, Win = 1, Draw = 2 }`,用于 `User.RecordGameResult` 的入参与 `EloRating.Calculate` 的结果计算。底层整数值 MUST 固定,用于未来可能的序列化稳定性。

#### Scenario: 枚举值存在
- **WHEN** 审阅 `Gomoku.Domain/Users/GameOutcome.cs`
- **THEN** 存在三个值 `Loss=0`、`Win=1`、`Draw=2`

---

### Requirement: `User.RecordGameResult(GameOutcome, int newRating)` 原子更新战绩与 Rating

系统 SHALL 在 `User` 聚合根上提供 `RecordGameResult(GameOutcome outcome, int newRating)` 方法。调用后 MUST 原子完成:

- `GamesPlayed = GamesPlayed + 1`
- 根据 `outcome`:若 `Win` 则 `Wins++`,若 `Loss` 则 `Losses++`,若 `Draw` 则 `Draws++`
- `Rating = newRating`
- **`RowVersion` 通过 `TouchRowVersion()` 替换为新 16 字节值**(本次 `add-concurrency-hardening` 新增;保证乐观并发令牌推进,让并发 SaveChanges 能被 EF 捕获)

`outcome` 传入未定义的枚举值时 MUST 抛 `ArgumentOutOfRangeException`,抛出时 User 状态 MUST 保持不变(包括 `RowVersion`)。

调用后 MUST 保持不变量:`Wins + Losses + Draws == GamesPlayed`。

#### Scenario: 胜场更新
- **WHEN** 新用户(`GamesPlayed=0, Wins=0, Rating=1200`)调用 `RecordGameResult(GameOutcome.Win, 1216)`
- **THEN** `GamesPlayed=1`,`Wins=1`,`Losses=0`,`Draws=0`,`Rating=1216`,`RowVersion` 不同于调用前

#### Scenario: 负场更新
- **WHEN** 新用户调用 `RecordGameResult(GameOutcome.Loss, 1184)`
- **THEN** `GamesPlayed=1`,`Losses=1`,`Rating=1184`,`RowVersion` 更新

#### Scenario: 平局更新
- **WHEN** 新用户调用 `RecordGameResult(GameOutcome.Draw, 1200)`
- **THEN** `GamesPlayed=1`,`Draws=1`,`Rating=1200`,`RowVersion` 更新

#### Scenario: 多局累积
- **WHEN** 同一用户连续调用 `RecordGameResult(Win, 1216) → RecordGameResult(Loss, 1200) → RecordGameResult(Draw, 1200)`
- **THEN** `GamesPlayed=3`,`Wins=1`,`Losses=1`,`Draws=1`,`Rating=1200`,且 `Wins+Losses+Draws == GamesPlayed`;三次调用间 RowVersion 两两不等

#### Scenario: 非法枚举值
- **WHEN** 传入 `(GameOutcome)99` 或其他非定义值
- **THEN** 抛 `ArgumentOutOfRangeException`;`User` 状态 MUST 保持不变,包括 `RowVersion`

### Requirement: `EloRating.Calculate` 是纯函数,实现标准 HS ELO 公式

系统 SHALL 在 `Gomoku.Domain/EloRating/EloRating.cs` 定义静态类 `EloRating`,方法签名:

```
public static (int NewRatingA, int NewRatingB) Calculate(
    int ratingA, int gamesA,
    int ratingB, int gamesB,
    GameOutcome outcomeA);
```

MUST 按以下公式(A 方视角)计算:

- `expectedA = 1.0 / (1 + Math.Pow(10, (ratingB - ratingA) / 400.0))`
- `scoreA = outcomeA switch { Win => 1.0, Draw => 0.5, Loss => 0.0 }`
- `kA = KFactor(gamesA)`,`kB = KFactor(gamesB)`
- `newRatingA = ratingA + Math.Round(kA * (scoreA - expectedA), MidpointRounding.AwayFromZero)`
- `newRatingB = ratingB + Math.Round(kB * ((1 - scoreA) - (1 - expectedA)), MidpointRounding.AwayFromZero)`

整数结果。返回 tuple。函数 MUST 不读取任何外部状态(时钟、随机、IO),相同入参产出相同出参。

#### Scenario: 纯函数
- **WHEN** 用相同入参连续调用 `EloRating.Calculate` 三次
- **THEN** 三次返回值完全相等

#### Scenario: 同级对抗黑胜,K=32 等价(双方同为中级 30–99 场)
- **WHEN** `Calculate(1200, 50, 1200, 50, Win)`
- **THEN** 返回 `(1210, 1190)`(K=20 时;用 kA=kB=20 计算:期望 0.5,变动 20*(1-0.5)=10)

#### Scenario: 平局不改变同级 Rating
- **WHEN** `Calculate(1500, 50, 1500, 50, Draw)`
- **THEN** 返回 `(1500, 1500)`(kA=kB=20;20*(0.5-0.5)=0)

#### Scenario: 上手输给下手(积分反转)
- **WHEN** `Calculate(1500, 50, 1400, 50, Loss)`(A=1500 但输了)
- **THEN** `newA` MUST 小于 1500,`newB` MUST 大于 1400

#### Scenario: K 因子按各自 games 分段
- **WHEN** `Calculate(1200, 0, 1200, 200, Win)`(A 新手,B 大师)
- **THEN** `newA - 1200` MUST 等于 `40 * (1 - 0.5)` 的四舍五入值 = 20(`newA = 1220`)
- **AND** `1200 - newB` MUST 等于 `10 * (1 - 0.5)` 的四舍五入值 = 5(`newB = 1195`)
- **AND** `(newA - 1200) != (1200 - newB)`(两方**非对称**,印证 D1)

#### Scenario: K 分段边界 games=29
- **WHEN** `Calculate(1200, 29, 1200, 29, Win)`
- **THEN** 双方 K=40;`newA=1220`,`newB=1180`

#### Scenario: K 分段边界 games=30
- **WHEN** `Calculate(1200, 30, 1200, 30, Win)`
- **THEN** 双方 K=20;`newA=1210`,`newB=1190`

#### Scenario: K 分段边界 games=99
- **WHEN** `Calculate(1200, 99, 1200, 99, Win)`
- **THEN** 双方 K=20;`newA=1210`,`newB=1190`

#### Scenario: K 分段边界 games=100
- **WHEN** `Calculate(1200, 100, 1200, 100, Win)`
- **THEN** 双方 K=10;`newA=1205`,`newB=1195`

#### Scenario: 舍入规则采用 AwayFromZero
- **WHEN** 某组入参让 `kA * (scoreA - expectedA)` 精确等于 `0.5`
- **THEN** 舍入结果 MUST 为 `1`(而非 banker's rounding 下的 `0`)

#### Scenario: 极端积分差(≥ 800)仍可计算
- **WHEN** `Calculate(2000, 50, 1000, 50, Win)`(A 远强且又赢)
- **THEN** 返回的 `newA` MUST 不小于 `ratingA`(几乎无增幅);`newB` MUST 不大于 `ratingB`(小幅扣分);计算不抛异常,不产 NaN

---

### Requirement: K 因子按 `gamesPlayed` 分段 `40 / 20 / 10`

系统 SHALL 在 `EloRating` 内部按下列规则决定 K:

- `gamesPlayed < 30 → K = 40`
- `30 ≤ gamesPlayed < 100 → K = 20`
- `gamesPlayed ≥ 100 → K = 10`

此规则对两方**各自独立**应用(见上 scenarios 中 "非对称")。

#### Scenario: 函数私有不暴露
- **WHEN** 审阅 `EloRating.cs` 的 public API
- **THEN** `KFactor` 可以是 `private static`,不暴露给 Domain 外;Calculate 方法内部使用

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

### Requirement: `IUserRepository.GetTopByRatingAsync(int limit)` 返回按排行榜顺序的用户列表

Application 层 SHALL 在 `IUserRepository` 上将旧的 `GetTopByRatingAsync(int limit, ...)` **替换**为:

```
Task<(IReadOnlyList<User> Users, int Total)> GetLeaderboardPagedAsync(
    int page, int pageSize, CancellationToken cancellationToken);
```

实现 MUST:
1. 过滤 `IsBot == false`(沿用 `add-ai-opponent` 约束);bot 不上榜。
2. 先做一次 `CountAsync` 得 Total(即"真人总数")。
3. 按 `(Rating DESC, Wins DESC, GamesPlayed ASC)` 排序,`Skip((page-1)*pageSize).Take(pageSize)` 物化到 Users。
4. 返回 `(Users, Total)` tuple。

返回类型 MUST 是领域类型(`IReadOnlyList<User>` + `int`),不泄漏 `IQueryable` / `IOrderedEnumerable` 等 EF 细节。

签名 rename 的原因:语义上本函数是"分页拉榜",而不是"取 Top N";避免一份方法既支持 limit 又支持 paged 的二义性。原 `GetTopByRatingAsync` 只有 `GetLeaderboardQueryHandler` 一个调用方,同步迁移即可。

#### Scenario: 排序正确(真人)
- **WHEN** 数据库有三位真人:A(Rating=1500, Wins=2)、B(Rating=1500, Wins=5)、C(Rating=1400, Wins=10),调 `GetLeaderboardPagedAsync(1, 100, ct)`
- **THEN** Users 顺序 `[B, A, C]`;Total = 3

#### Scenario: 按 GamesPlayed ASC 作为三级排序
- **WHEN** 两位真人 `(Rating=1500, Wins=3, GamesPlayed=10)` 与 `(Rating=1500, Wins=3, GamesPlayed=5)`
- **THEN** 后者(场次少)排前

#### Scenario: 分页跳过
- **WHEN** 数据库有 5 位真人,调 `GetLeaderboardPagedAsync(2, 2, ct)`
- **THEN** Users.Count == 2(第 3、4 名);Total == 5

#### Scenario: 过大 page 空结果
- **WHEN** 数据库有 5 位真人,调 `GetLeaderboardPagedAsync(10, 2, ct)`
- **THEN** Users.Count == 0;Total 仍然 == 5(客户端可按 Total 算"无更多"或回第 1 页)

#### Scenario: Bot 被过滤
- **WHEN** 数据库有 5 位真人和 3 位 bot
- **THEN** Users.Count ≤ 5(视分页);Total == 5(仅真人)

#### Scenario: 仅 bot 的极端情形
- **WHEN** 数据库仅存在 bot 账号
- **THEN** 返回空 Users 列表,Total == 0

---

### Requirement: `GetLeaderboardQueryHandler` 分配 Rank 并映射 DTO

Application 层 SHALL 把 `GetLeaderboardQuery` 改为接受 `Page` / `PageSize` 参数,返回 `PagedResult<LeaderboardEntryDto>`:

```
public sealed record GetLeaderboardQuery(int Page, int PageSize)
    : IRequest<PagedResult<LeaderboardEntryDto>>;
```

SHALL 同时新增 `GetLeaderboardQueryValidator`:

- `Page ≥ 1`,否则 `ValidationException` → HTTP 400。
- `PageSize` ∈ [1, 100]。

Handler 流程:

1. 调 `IUserRepository.GetLeaderboardPagedAsync(Page, PageSize, ct)` → `(users, total)`
2. 按顺序映射 Users → `LeaderboardEntryDto`;
3. **Rank 是全局名次**,按公式 `Rank = (Page - 1) * PageSize + i + 1`(`i` 是本页 0-based 下标)计算,使 page=2 pageSize=20 的第一个 entry 的 Rank == 21。
4. 包 `PagedResult<LeaderboardEntryDto>(Items, Total, Page, PageSize)` 返回。

DTO 定义 MUST 精确包含 8 个字段(沿用 `add-elo-system`);MUST NOT 泄漏 `Email` / `PasswordHash` / refresh token 相关字段。

#### Scenario: Rank 全局递增(page=1)
- **WHEN** 仓储返回 3 位用户,调 `GetLeaderboardQuery(1, 20)`
- **THEN** Items Rank 依次为 `1, 2, 3`;Total == 3

#### Scenario: Rank 在 page 2 不重置为 1
- **WHEN** 仓储 返回(Users.Count=2, Total=5),调 `GetLeaderboardQuery(2, 2)`
- **THEN** Items[0].Rank == 3;Items[1].Rank == 4;Total == 5

#### Scenario: 空榜单
- **WHEN** 仓储返回 `(Users=[], Total=0)`
- **THEN** Items 为空,Total == 0,Page / PageSize 回传

#### Scenario: DTO 不含敏感字段
- **WHEN** 审阅 `LeaderboardEntryDto` 定义与 mapping 代码
- **THEN** MUST 不出现 `Email` / `PasswordHash` / `RefreshTokens`

---

### Requirement: `GET /api/leaderboard` 端点返回前 100 条排行榜

Api 层 SHALL 暴露 `GET /api/leaderboard`,要求 `[Authorize]`。成功响应 HTTP 200 + `PagedResult<LeaderboardEntryDto>`(`Items` 最多 `PageSize` 条,Total 是过滤 bot 后的真人总数)。

本 Requirement **修订** `add-elo-system` 原来的 `MUST NOT 接受 query 参数`:

- 端点 SHALL 接受 query `page`(默认 1)和 `pageSize`(默认 20)。
- `pageSize` MUST 限 ≤ 100(与 `add-game-replay` 统一)。
- 非法 `page=0` / `pageSize=0` / `pageSize > 100` MUST 返回 HTTP 400(由 `GetLeaderboardQueryValidator`)。

#### Scenario: 未登录被拒
- **WHEN** 无 Authorization 头的请求
- **THEN** HTTP 401

#### Scenario: 默认参数
- **WHEN** `GET /api/leaderboard`(不带 query)
- **THEN** HTTP 200;`Page == 1`、`PageSize == 20`;Items ≤ 20 条

#### Scenario: 分页拉榜
- **WHEN** `GET /api/leaderboard?page=2&pageSize=10`
- **THEN** HTTP 200;`Page == 2`、`PageSize == 10`;Items 第一条 Rank == 11

#### Scenario: PageSize 超限
- **WHEN** `GET /api/leaderboard?pageSize=101`
- **THEN** HTTP 400 `ValidationException`

#### Scenario: Page 非正
- **WHEN** `GET /api/leaderboard?page=0`
- **THEN** HTTP 400 `ValidationException`

#### Scenario: 排序仍按 elo-rating 约束
- **WHEN** 成功拉榜
- **THEN** Items 按 `Rating DESC, Wins DESC, GamesPlayed ASC` 排序;Rank 递增且无重复

