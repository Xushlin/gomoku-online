# elo-rating Specification

## Purpose
TBD - created by archiving change add-elo-system. Update Purpose after archive.
## Requirements
### Requirement: `GameOutcome` 枚举表达"一方视角下的一局对局结果"

系统 SHALL 定义 `enum GameOutcome { Loss = 0, Win = 1, Draw = 2 }`,用于 `User.RecordGameResult` 的入参与 `EloRating.Calculate` 的结果计算。底层整数值 MUST 固定,用于未来可能的序列化稳定性。

#### Scenario: 枚举值存在
- **WHEN** 审阅 `Gewu.Domain/Users/GameOutcome.cs`
- **THEN** 存在三个值 `Loss=0`、`Win=1`、`Draw=2`

---

### Requirement: `EloRating.Calculate` 是纯函数,实现标准 HS ELO 公式

系统 SHALL 在 `Gewu.Domain/EloRating/EloRating.cs` 定义静态类 `EloRating`,方法签名:

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

`gamesPlayed` MUST 取自该玩家在**该棋种**上的 `UserGameStats.GamesPlayed`,而不是跨棋种总局数。
一个五子棋老手第一次下中国象棋时,在象棋阶梯上 MUST 按 `K = 40` 起步 —— 他在象棋上确实是新手。
用总局数会让他的象棋分几乎不动,而那正是分棋种评分要解决的问题。

#### Scenario: 函数私有不暴露
- **WHEN** 审阅 `EloRating.cs` 的 public API
- **THEN** `KFactor` 可以是 `private static`,不暴露给 Domain 外;Calculate 方法内部使用

### Requirement: `MakeMoveCommandHandler` 在对局结束时同事务更新两位玩家的 Rating 与战绩

`MakeMoveCommand` handler 在 `Room.PlayMove` 返回 `MoveOutcome.Result != GameResult.Ongoing` **且该房间的棋种 `IsRated == true`** 时 MUST 执行:

1. 用 `IUserRepository.GetOrCreateGameStatsAsync(userId, gameKey, ct)` 取两位玩家在**该棋种**上的
   `UserGameStats` —— 首局时该行还不存在,由仓库以初始值(`Rating = 1200`,战绩全 0)建出来。
   "第一次下这个棋种"是常态而不是异常,所以这里是 get-or-create 而不是 find-or-throw。
2. 按 `Result` 推导双方 `GameOutcome`:
   - `BlackWin`:黑 `Win`、白 `Loss`
   - `WhiteWin`:黑 `Loss`、白 `Win`
   - `Draw`:黑 `Draw`、白 `Draw`
3. 调 `EloRating.Calculate(blackStats.Rating, blackStats.GamesPlayed, whiteStats.Rating, whiteStats.GamesPlayed, outcomeForBlack)`
   —— 两个 `GamesPlayed` 都是**该棋种**的局数,所以 K 因子按该棋种的资历分段(见 K 因子那条)。
4. `blackStats.RecordGameResult(outcomeForBlack, newBlackRating)` 与 `whiteStats.RecordGameResult(outcomeForWhite, newWhiteRating)`
5. **同一次** `IUnitOfWork.SaveChangesAsync` 提交 —— Room.Game / Room.Status / 两行 `UserGameStats`
   的变更(含首局时新建的行)在同一事务

`Result == Ongoing` 时 handler MUST NOT 查询 / 修改 `UserGameStats` —— 尤其 MUST NOT 为一局还没结束的
棋**创建**战绩行,否则"下过这个棋种"的含义会从"下完过"变成"点开过",而排行榜的成员资格正是靠它。

**棋种不计分时同样 MUST NOT 查询 / 修改 `UserGameStats`**(`add-tictactoe` 起的约束)。尤其
MUST NOT 调 `GetOrCreateGameStatsAsync` —— 它会**建行**,而建出来就等于把人登记进了那个棋种的
排行榜。判定取自
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

#### Scenario: 非结束局不触动战绩
- **WHEN** `outcome.Result == Ongoing`
- **THEN** Handler MUST NOT 调 `IUserRepository.GetOrCreateGameStatsAsync` / `UserGameStats.RecordGameResult`

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
- **THEN** `Room.Status == Finished`、`Game.EndReason` 已写入、`GameEndedAsync` 已广播;而 Handler MUST NOT 调 `IUserRepository.GetOrCreateGameStatsAsync` / `UserGameStats.RecordGameResult`,且 MUST NOT 有任何 `UserGameStats` 行被创建或修改

#### Scenario: 不计分棋种的对局仍可回放
- **WHEN** 上述一字棋对局结束后调 `GET /api/rooms/{id}/replay`
- **THEN** 正常返回完整 moves 与元数据 —— 不计分 MUST NOT 削弱对局记录

#### Scenario: 排行榜不受一字棋影响
- **WHEN** 若干局一字棋结束后调 `GET /api/leaderboard`
- **THEN** 名次与 Rating 与这些对局发生之前完全一致

#### Scenario: 首次下某棋种时建出战绩行
- **WHEN** 两位从未下过 `xiangqi` 的玩家下完第一局 `xiangqi`
- **THEN** 各新建一行 `UserGameStats(userId, "xiangqi")`,`GamesPlayed == 1`;两人的 `gomoku` 那行 MUST NOT 被触碰

#### Scenario: 棋种之间互不影响
- **WHEN** Alice 的 `gomoku` 是 `(1500, 30 局)`,她下完一局 `xiangqi` 并取胜
- **THEN** `xiangqi` 那行按 `K = 40` 从 1200 起算(她在象棋上是新手);`gomoku` 那行 MUST 一个字段都不变

#### Scenario: 未结束局不建行
- **WHEN** `outcome.Result == Ongoing`
- **THEN** MUST NOT 有任何 `UserGameStats` 行被创建或修改

### Requirement: `GetLeaderboardQueryHandler` 分配 Rank 并映射 DTO

Application 层 SHALL 把 `GetLeaderboardQuery` 改为接受 `GameKey` / `Page` / `PageSize` 参数,返回 `PagedResult<LeaderboardEntryDto>`:

```
public sealed record GetLeaderboardQuery(string GameKey, int Page, int PageSize)
    : IRequest<PagedResult<LeaderboardEntryDto>>;
```

SHALL 同时新增 `GetLeaderboardQueryValidator`:

- `Page ≥ 1`,否则 `ValidationException` → HTTP 400。
- `PageSize` ∈ [1, 100]。
- `GameKey` 非空。**MUST NOT 校验它是否已登记** —— 未登记的棋种返回空榜而不是 400,与房间列表同一处理:集合端点上"这个棋种没有榜"与"榜是空的"对调用方无从分辨。

Handler 流程:

1. 调 `IUserRepository.GetLeaderboardPagedAsync(GameKey, Page, PageSize, ct)` → `(entries, total)`
2. 按顺序映射 `UserGameStats` → `LeaderboardEntryDto`,用户名经 `LookupUsernamesAsync` 另取;
3. **Rank 是全局名次**,按公式 `Rank = (Page - 1) * PageSize + i + 1`(`i` 是本页 0-based 下标)计算,使 page=2 pageSize=20 的第一个 entry 的 Rank == 21。
4. 包 `PagedResult<LeaderboardEntryDto>(Items, Total, Page, PageSize)` 返回。

DTO 定义 MUST 精确包含 8 个字段(沿用 `add-elo-system`);MUST NOT 泄漏 `Email` / `PasswordHash` / refresh token 相关字段。

**`LeaderboardEntryDto` 的形状 MUST 一个字节不变。** 变的只是数据来源(`User` → `UserGameStats`)。
这样已发布的 Web 客户端零改动、看到的数字与现在完全一致 —— "排行榜加棋种切换"是纯前端工作,
留给 `add-web-per-game-rating`。

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

#### Scenario: 未登记的棋种返回空榜而不是 400
- **WHEN** `GetLeaderboardQuery("a-game-nobody-registered", 1, 20)`
- **THEN** Items 为空、Total == 0、HTTP 200

  举例 MUST NOT 用 `xiangqi` —— 它自 `add-xiangqi` 起就已登记,拿它举例"未登记"会让这条场景为了错误的理由通过(一个新棋种的空榜同样是空的)。对应的测试用的一直是真正没登记的键。

### Requirement: `GET /api/leaderboard` 端点返回前 100 条排行榜

Api 层 SHALL 暴露 `GET /api/leaderboard`,要求 `[Authorize]`。成功响应 HTTP 200 + `PagedResult<LeaderboardEntryDto>`(`Items` 最多 `PageSize` 条,Total 是过滤 bot 后的真人总数)。

本 Requirement **修订** `add-elo-system` 原来的 `MUST NOT 接受 query 参数`:

- 端点 SHALL 接受 query `page`(默认 1)、`pageSize`(默认 20)和 `gameKey`(默认 `gomoku`)。
- `gameKey` 缺省为 `gomoku` MUST 只发生在 Api 层;`GetLeaderboardQuery.GameKey` MUST 是必填非空字段
  —— Application 层不猜自己在被问哪个棋种。理由与建房、房间列表的缺省一致:已发布的客户端
  不会送这个参数,而让它们从此看不到榜是不可接受的回归。
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

#### Scenario: 缺省棋种向后兼容
- **WHEN** 已发布的客户端调 `GET /api/leaderboard`(不带 `gameKey`)
- **THEN** 返回五子棋的榜,数字与 `add-per-game-rating` 之前完全一致

#### Scenario: 按棋种拉榜
- **WHEN** `GET /api/leaderboard?gameKey=xiangqi`
- **THEN** HTTP 200,只含在 `xiangqi` 上有战绩的玩家

#### Scenario: 不计分的棋种
- **WHEN** `GET /api/leaderboard?gameKey=tictactoe`
- **THEN** HTTP 200 + 空榜 —— 一字棋不计分,所以没有任何 `UserGameStats` 行,而这与"查询坏了"的区别在于它 MUST NOT 报错

### Requirement: `UserGameStats.RecordGameResult(GameOutcome, int newRating)` 原子更新某棋种的战绩与 Rating

系统 SHALL 在 `UserGameStats` 实体上提供 `RecordGameResult(GameOutcome outcome, int newRating)` 方法。调用后 MUST 原子完成:

- `GamesPlayed = GamesPlayed + 1`
- 根据 `outcome`:若 `Win` 则 `Wins++`,若 `Loss` 则 `Losses++`,若 `Draw` 则 `Draws++`
- `Rating = newRating`
- **`RowVersion` 通过 `TouchRowVersion()` 替换为新 16 字节值**(本次 `add-concurrency-hardening` 新增;保证乐观并发令牌推进,让并发 SaveChanges 能被 EF 捕获)

`outcome` 传入未定义的枚举值时 MUST 抛 `ArgumentOutOfRangeException`,抛出时该行状态 MUST 保持不变(包括 `RowVersion`)。

调用后 MUST 保持不变量:`Wins + Losses + Draws == GamesPlayed` —— 现在这条不变量
**对每个棋种各自成立**。

本方法此前在 `User` 上。搬到 `UserGameStats` 之后,`User` MUST NOT 再持有
`Rating` / `GamesPlayed` / `Wins` / `Losses` / `Draws` 中的任何一个,**也 MUST NOT 保留它们的镜像**
—— 镜像是第二份真源,它与 `UserGameStats` 里 `gomoku` 那行漂移之后,症状是排行榜与资料页
显示不同的分,而没有任何东西会拦住。

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
- **THEN** 抛 `ArgumentOutOfRangeException`;该 `UserGameStats` 行的状态 MUST 保持不变,包括 `RowVersion`

### Requirement: `IUserRepository.GetLeaderboardPagedAsync` 按棋种返回排行榜顺序

Application 层 SHALL 提供:

```
Task<(IReadOnlyList<UserGameStats> Entries, int Total)> GetLeaderboardPagedAsync(
    string gameKey, int page, int pageSize, CancellationToken cancellationToken);
```

**本 requirement 的标题此前是错的**:它写着 `GetTopByRatingAsync(int limit)`,而那个方法早在
`add-leaderboard-pagination` 就换成了分页版 —— 规范一直落后于代码,`add-per-game-rating` 顺手改正。

实现 MUST:
0. 过滤 `GameKey == gameKey` —— **谓词下推到 EF**,MUST NOT 在内存里筛
1. 过滤 `IsBot == false`(沿用 `add-ai-opponent` 约束);bot 不上榜。
2. 先做一次 `CountAsync` 得 Total(即"真人总数")。
3. 按 `(Rating DESC, Wins DESC, GamesPlayed ASC)` 排序,`Skip((page-1)*pageSize).Take(pageSize)` 物化。
4. 返回 `(Entries, Total)` tuple。

**没有该棋种 `UserGameStats` 行的用户 MUST NOT 出现在该棋种的榜上**,也 MUST NOT 以初始
1200 分占位。否则一个从没下过一字棋的人会出现在一字棋榜上,位置取决于有多少人恰好也没下过
—— 榜的含义会从"这些人的棋力顺序"变成"这些人里谁碰巧下过"。一个新棋种刚上线时它的榜
几乎是空的,那是**对的**。

返回类型 MUST 是领域类型(`IReadOnlyList<UserGameStats>` + `int`),不泄漏 `IQueryable` /
`IOrderedEnumerable` 等 EF 细节。**上一句此前写的是 `IReadOnlyList<User>`,与本 requirement 自己
给出的签名矛盾** —— 顺手改正。

返回 `UserGameStats` 而不是 `User`:排行榜要的是"某人在某棋种上的分",而那正是这个实体承载的东西。
调用方另取用户名 —— 它已经在用 `LookupUsernamesAsync` 做这件事。

#### Scenario: 排序正确(真人)
- **WHEN** 数据库有三位真人:A(Rating=1500, Wins=2)、B(Rating=1500, Wins=5)、C(Rating=1400, Wins=10),调 `GetLeaderboardPagedAsync("gomoku", 1, 100, ct)`
- **THEN** Entries 顺序 `[B, A, C]`;Total = 3

#### Scenario: 按 GamesPlayed ASC 作为三级排序
- **WHEN** 两位真人 `(Rating=1500, Wins=3, GamesPlayed=10)` 与 `(Rating=1500, Wins=3, GamesPlayed=5)`
- **THEN** 后者(场次少)排前

#### Scenario: 分页跳过
- **WHEN** 数据库有 5 位真人,调 `GetLeaderboardPagedAsync("gomoku", 2, 2, ct)`
- **THEN** Entries.Count == 2(第 3、4 名);Total == 5

#### Scenario: 过大 page 空结果
- **WHEN** 数据库有 5 位真人,调 `GetLeaderboardPagedAsync("gomoku", 10, 2, ct)`
- **THEN** Entries.Count == 0;Total 仍然 == 5(客户端可按 Total 算"无更多"或回第 1 页)

#### Scenario: Bot 被过滤
- **WHEN** 数据库有 5 位真人和 3 位 bot
- **THEN** Entries.Count ≤ 5(视分页);Total == 5(仅真人)

#### Scenario: 仅 bot 的极端情形
- **WHEN** 数据库仅存在 bot 账号
- **THEN** 返回空 Entries 列表,Total == 0

#### Scenario: 榜按棋种隔离
- **WHEN** Alice 在 `gomoku` 上有 1500 分、在 `xiangqi` 上有 1300 分,查 `xiangqi` 的榜
- **THEN** 返回的那条是 1300 分那行,MUST NOT 是 1500

#### Scenario: 没下过该棋种的人不上榜
- **WHEN** Bob 只有 `gomoku` 的 `UserGameStats` 行,查 `xiangqi` 的榜
- **THEN** Bob MUST NOT 出现;Total MUST NOT 把他算进去

#### Scenario: 未登记的棋种
- **WHEN** 以一个不存在的棋种键查询
- **THEN** 返回空列表且 Total == 0,MUST NOT 抛错

