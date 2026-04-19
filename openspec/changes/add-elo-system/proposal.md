## Why

`add-user-authentication` 已经在 `User` 聚合上立好了 `Rating`(默认 1200)、`GamesPlayed`、`Wins`、`Losses`、`Draws` 五个字段,但到今天为止没有任何代码会写它们 —— 不管打多少盘棋,战绩永远为 0、Rating 永远 1200。

`add-rooms-and-gameplay` 已经在 `MakeMoveCommand` handler 里让对局能干净结束(`GameEnded` 事件 + `Room.Status = Finished`),正好是更新战绩的**唯一合适时机**。把 ELO 算法落进这个结束路径,同事务完成"对局结束 + 战绩 + Rating"的写入,玩家对局后刷新 `/api/users/me` 就能看到新积分。

顺手给前端准备好"排行榜"能力(最早期用户说"前三名特殊图标"依赖它)—— 一次把积分体系整个闭合。

## What Changes

- **Domain**:
  - 新增 `Gomoku.Domain/Users/GameOutcome.cs` 枚举:`Loss / Win / Draw`。
  - 新增 `User.RecordGameResult(GameOutcome, int newRating)` 领域方法:原子地 `GamesPlayed++`、更新对应计数器(`Wins` / `Losses` / `Draws`)、设置 `Rating = newRating`。
  - 新增 `Gomoku.Domain/EloRating/EloRating.cs` 纯计算静态类:`Calculate(int ratingA, int gamesA, int ratingB, int gamesB, GameOutcome outcomeA)` → `(int newRatingA, int newRatingB)`。**K 因子按己方 `GamesPlayed` 分段**(<30 → 40,<100 → 20,≥100 → 10),整数结果用 `MidpointRounding.AwayFromZero`。
- **Application**:
  - 扩展 `MakeMoveCommandHandler`:`Room.PlayMove` 返回 `Result != Ongoing` 时,从仓储 Load 双方 `User`,调 `EloRating.Calculate`,对两位 `User` 各调 `RecordGameResult`,同一次 `SaveChangesAsync` 提交。
  - 新增 query feature `Features/Users/GetLeaderboard`:`GetLeaderboardQuery` 返回 `IReadOnlyList<LeaderboardEntryDto>`,按 `Rating DESC, Wins DESC, GamesPlayed ASC` 排序,默认取前 100。
  - 新增 `LeaderboardEntryDto(int Rank, Guid UserId, string Username, int Rating, int GamesPlayed, int Wins, int Losses, int Draws)`。
  - `IUserRepository` 新增 `Task<IReadOnlyList<User>> GetTopByRatingAsync(int limit, CancellationToken ct)`。
- **Infrastructure**:
  - `UserRepository.GetTopByRatingAsync` 实现(EF `OrderByDescending(...).Take(limit)`)。
  - **无需 migration** —— User 表已有这 5 列。
- **Api**:
  - 新 `LeaderboardController`:`[Authorize]`、`GET /api/leaderboard` → 200 + `LeaderboardEntryDto[]`。(**前三名特殊图标是前端职责**,本次后端只返回 Rank;前端按 Rank 1/2/3 渲染金/银/铜即可。)
- **Tests**:
  - `Gomoku.Domain.Tests/Users/UserRecordGameResultTests.cs`(计数器 + Rating 设置)。
  - `Gomoku.Domain.Tests/EloRating/EloRatingTests.cs` 表驱动:同级黑胜 / 平局 / 上手对下手胜负 / K 分段边界 / 舍入边界。
  - `Gomoku.Application.Tests/Features/Rooms/MakeMoveCommandHandlerTests.cs` 扩展:对局结束场景断言 `IUserRepository.FindByIdAsync` 对双方都被调,两位 `User` 的 `Rating` / 计数器被更新,`SaveChangesAsync` 一次。
  - `Gomoku.Application.Tests/Features/Users/GetLeaderboardQueryHandlerTests.cs`:排序 + 限流 + Rank 分配。

**显式不做**(留给后续变更):
- 退货式 ELO 调整 / 悔棋重算。
- 按段位(bronze / silver / ...)分组:前端能基于 Rating 数值渲染;后端不加段位字段,后端契约不膨胀。
- 禁手规则 / AI 对战的 ELO 调整策略 —— 等对应变更时再扩展 `EloRating.Calculate` 的入参。
- 排行榜分页 / 搜索 / 按时间范围过滤 —— 本次固定 top 100 一次吐完;将来 `add-leaderboard-pagination` 再细化。
- 历史 Rating 曲线(每局快照)—— 要新表,本次不做。
- **User 乐观并发保护** —— Rating 并发更新在单聚合极少触发(一个用户罕见同时参与两盘结束),本次接受"后写覆盖先写"的极小窗口;`add-concurrency-hardening` 时再加 `IConcurrencyToken`。

## Capabilities

### New Capabilities

- `elo-rating`:ELO 计算公式(HS ELO,K 分段)、对局结束时的战绩更新、排行榜查询与排序规则。覆盖"一盘棋打完后双方积分怎么变"这一组不变量与 API。

### Modified Capabilities

(无 —— `user-management` 的已有 Requirement 不改,`User` 加新方法视作 `elo-rating` 能力对 User 聚合的**新增能力点**,不属于对既有 spec 的修订。`room-and-gameplay` 的 Requirement 亦不改;只是本次在 handler 实现里新增一条路径消费 `GameEnded` 数据。)

## Impact

- **代码规模**:~20 个新文件(含测试),0 migration。是至今最小的一次变更。
- **新增 NuGet**:零。
- **HTTP 表面**:+1 端点 `GET /api/leaderboard`。
- **数据库**:零 schema 变化;仅 `Users.Rating` / `GamesPlayed` / `Wins` / `Losses` / `Draws` 列开始被写入。
- **SignalR**:零变化;本次**不广播** "RatingChanged" 事件(`GameEnded` 事件后客户端自行刷新 `GET /api/users/me`,或在下一次 SignalR `RoomState` 事件里通过 `Host/Black/White` 的 `UserSummaryDto` 暂不带 Rating —— 若前端需要,本次 proposal **故意不加**,留给设计 open-question)。
- **后续变更将依赖**:前端的排行榜页、前三名图标、"我的主页"显示 Rating 变化。
