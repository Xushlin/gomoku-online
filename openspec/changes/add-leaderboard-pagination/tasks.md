## 1. Application — 签名 / query / validator 修订

- [x] 1.1 `IUserRepository.cs`:把 `GetTopByRatingAsync(int limit, CancellationToken)` **替换**为 `GetLeaderboardPagedAsync(int page, int pageSize, CancellationToken)`,返回 `Task<(IReadOnlyList<User> Users, int Total)>`。
- [x] 1.2 `Features/Users/GetLeaderboard/GetLeaderboardQuery.cs`:改为 `record GetLeaderboardQuery(int Page, int PageSize) : IRequest<PagedResult<LeaderboardEntryDto>>`。
- [x] 1.3 `Features/Users/GetLeaderboard/GetLeaderboardQueryValidator.cs`(新文件):`Page ≥ 1`、`PageSize ∈ [1, 100]`。
- [x] 1.4 `Features/Users/GetLeaderboard/GetLeaderboardQueryHandler.cs` 重写:调新仓储签名,映射 Items 并算全局 Rank(`(Page-1)*PageSize + i + 1`),包 `PagedResult`。

## 2. Infrastructure

- [x] 2.1 `UserRepository.cs`:`GetTopByRatingAsync` 改名 / 改形为 `GetLeaderboardPagedAsync`。核心 SQL:
  ```csharp
  var baseQuery = _db.Users.Where(u => !u.IsBot);
  var total = await baseQuery.CountAsync(ct);
  var users = await baseQuery
      .OrderByDescending(u => u.Rating)
      .ThenByDescending(u => u.Wins)
      .ThenBy(u => u.GamesPlayed)
      .Skip((page - 1) * pageSize)
      .Take(pageSize)
      .ToListAsync(ct);
  return (users, total);
  ```

## 3. Api

- [x] 3.1 `LeaderboardController.Get`:签名追加 `[FromQuery] int page = 1, [FromQuery] int pageSize = 20`;`ActionResult<PagedResult<LeaderboardEntryDto>>` 返回类型。

## 4. 测试更新

- [x] 4.1 `GetLeaderboardQueryHandlerTests.cs` 改造:所有用例改为 `new GetLeaderboardQuery(1, 20)`;断言 `result.Items` 而非 `result`;`result.Total` 正确。新增 **分页边界用例**:page=2 / pageSize=2 / total=5 → Items[0].Rank == 3。
- [x] 4.2 新 `GetLeaderboardQueryValidatorTests.cs`(5 用例):默认通过 / Page=0 / PageSize=0 / PageSize=101 / PageSize=100 边界。

## 5. 端到端冒烟

- [x] 5.1 启动 Api,已有若干真人用户(至少 3 个,能验证排序)。
- [x] 5.2 `GET /api/leaderboard?page=1&pageSize=10`:200 + PagedResult,Items 按 Rating DESC,Rank 从 1 起。
- [x] 5.3 `GET /api/leaderboard?page=2&pageSize=2`:Items 含排名 3 / 4(若有)。
- [x] 5.4 `GET /api/leaderboard`(不带 query):默认 page=1 / pageSize=20。
- [x] 5.5 `GET /api/leaderboard?pageSize=101`:400。

## 6. 归档前置检查

- [x] 6.1 `dotnet build Gomoku.slnx`:0 警告 / 0 错。
- [x] 6.2 `dotnet test Gomoku.slnx`:全绿(Domain 230 不变;Application 106 → 约 110)。
- [x] 6.3 `openspec validate add-leaderboard-pagination --strict`:valid。
- [x] 6.4 分支 `feat/add-leaderboard-pagination`,按层分组 commit(Application / Infrastructure / Api / docs-openspec 四条)。
