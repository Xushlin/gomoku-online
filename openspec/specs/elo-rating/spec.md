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

系统 SHALL 在 `User` 聚合根上新增 `RecordGameResult(GameOutcome outcome, int newRating)` 方法。调用后 MUST 原子完成:

- `GamesPlayed = GamesPlayed + 1`
- 根据 `outcome`:若 `Win` 则 `Wins++`,若 `Loss` 则 `Losses++`,若 `Draw` 则 `Draws++`
- `Rating = newRating`

`outcome` 传入未定义的枚举值时 MUST 抛 `ArgumentOutOfRangeException`。

调用后 MUST 保持不变量:`Wins + Losses + Draws == GamesPlayed`。

#### Scenario: 胜场更新
- **WHEN** 新用户(`GamesPlayed=0, Wins=0, Rating=1200`)调用 `RecordGameResult(GameOutcome.Win, 1216)`
- **THEN** `GamesPlayed=1`,`Wins=1`,`Losses=0`,`Draws=0`,`Rating=1216`

#### Scenario: 负场更新
- **WHEN** 新用户调用 `RecordGameResult(GameOutcome.Loss, 1184)`
- **THEN** `GamesPlayed=1`,`Losses=1`,`Rating=1184`

#### Scenario: 平局更新
- **WHEN** 新用户调用 `RecordGameResult(GameOutcome.Draw, 1200)`
- **THEN** `GamesPlayed=1`,`Draws=1`,`Rating=1200`

#### Scenario: 多局累积
- **WHEN** 同一用户连续调用 `RecordGameResult(Win, 1216) → RecordGameResult(Loss, 1200) → RecordGameResult(Draw, 1200)`
- **THEN** `GamesPlayed=3`,`Wins=1`,`Losses=1`,`Draws=1`,`Rating=1200`,且 `Wins+Losses+Draws == GamesPlayed`

#### Scenario: 非法枚举值
- **WHEN** 传入 `(GameOutcome)99` 或其他非定义值
- **THEN** 抛 `ArgumentOutOfRangeException`;`User` 状态 MUST 保持不变

---

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

Handler 调 `IRoomNotifier` 的时序保持原 Requirement(`RoomStateChangedAsync` → `MoveMadeAsync` → `GameEndedAsync`);**Rating 变更不单独广播**,客户端通过 `GameEnded` 事件到来后主动 `GET /api/users/me` 刷新。

#### Scenario: 非结束局不触动 User
- **WHEN** `outcome.Result == Ongoing`
- **THEN** Handler MUST NOT 调 `IUserRepository.FindByIdAsync` / `User.RecordGameResult`

#### Scenario: 黑方连五后结算
- **WHEN** `outcome.Result == BlackWin`,对局前黑方 `(Rating=1200, GamesPlayed=0)`,白方 `(Rating=1200, GamesPlayed=0)`
- **THEN** Handler 调 `EloRating.Calculate(1200, 0, 1200, 0, Win)`(两方 K=40,期望 0.5);`black.Rating == 1220`,`white.Rating == 1180`
- **AND** `black.Wins == 1`,`white.Losses == 1`,两方 `GamesPlayed == 1`

#### Scenario: 平局结算
- **WHEN** `outcome.Result == Draw`
- **THEN** 双方 `GameOutcome.Draw`;`RecordGameResult` 让 `Draws++`、`GamesPlayed++`;`Rating` 按 `EloRating.Calculate(..., Draw)` 结果(同级下不变)

#### Scenario: 单 SaveChanges
- **WHEN** 对局结束路径完整跑一遍
- **THEN** `IUnitOfWork.SaveChangesAsync` MUST 被调用恰好 **一次**(Room + 两位 User 变更合并在同一事务里)

---

### Requirement: `IUserRepository.GetTopByRatingAsync(int limit)` 返回按排行榜顺序的用户列表

Application 层 SHALL 在 `IUserRepository` 上新增:

```
Task<IReadOnlyList<User>> GetTopByRatingAsync(int limit, CancellationToken cancellationToken);
```

实现 MUST 按 `(Rating DESC, Wins DESC, GamesPlayed ASC)` 排序,取前 `limit` 条。返回类型 MUST 是领域类型(不泄漏 `IQueryable` / `IOrderedEnumerable` 等 EF 细节)。

#### Scenario: 排序正确
- **WHEN** 数据库有三位用户:A(Rating=1500, Wins=2), B(Rating=1500, Wins=5), C(Rating=1400, Wins=10),调 `GetTopByRatingAsync(100, ct)`
- **THEN** 返回顺序 `[B, A, C]`(Rating 高者前,同 Rating 下 Wins 高者前)

#### Scenario: 按 GamesPlayed ASC 作为三级排序
- **WHEN** 两位用户 `(Rating=1500, Wins=3, GamesPlayed=10)` 与 `(Rating=1500, Wins=3, GamesPlayed=5)`
- **THEN** 后者(场次少)排前

#### Scenario: 限流
- **WHEN** 数据库有 200 位用户,调 `GetTopByRatingAsync(100, ct)`
- **THEN** 返回 100 条

---

### Requirement: `GetLeaderboardQueryHandler` 分配 Rank 并映射 DTO

Application 层 SHALL 新增 `GetLeaderboardQuery` + `GetLeaderboardQueryHandler`,返回 `IReadOnlyList<LeaderboardEntryDto>`。Handler 内部:

1. 调 `IUserRepository.GetTopByRatingAsync(100, ct)`
2. 按顺序映射为 `LeaderboardEntryDto(Rank, UserId, Username, Rating, GamesPlayed, Wins, Losses, Draws)`,`Rank` 从 1 起按下标递增
3. 返回

DTO 定义 MUST 精确包含上述 8 个字段;MUST NOT 泄漏 `Email` / `PasswordHash` / refresh token 相关字段。

#### Scenario: Rank 从 1 递增
- **WHEN** 仓储返回 3 位用户
- **THEN** Rank 依次为 `1, 2, 3`

#### Scenario: 空榜单
- **WHEN** 数据库无用户(或无人有战绩)
- **THEN** 返回空列表,不抛异常

#### Scenario: DTO 不含敏感字段
- **WHEN** 审阅 `LeaderboardEntryDto` 定义与 mapping 代码
- **THEN** MUST 不出现 `Email` / `PasswordHash` / `RefreshTokens`

---

### Requirement: `GET /api/leaderboard` 端点返回前 100 条排行榜

Api 层 SHALL 暴露 `GET /api/leaderboard`,要求 `[Authorize]`。成功响应 HTTP 200 + `LeaderboardEntryDto[]`(最多 100 条)。

MUST NOT 接受 query 参数(limit / offset / filter —— 留给后续变更)。

#### Scenario: 未登录被拒
- **WHEN** 无 Authorization 头的请求
- **THEN** HTTP 401

#### Scenario: 登录后成功拉榜
- **WHEN** 合法 JWT + `GET /api/leaderboard`
- **THEN** HTTP 200,响应体是 `LeaderboardEntryDto[]`(可能为空),顺序符合 `Rating DESC, Wins DESC, GamesPlayed ASC`,每条 `Rank >= 1` 且无重复

