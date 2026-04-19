## 1. Domain — 枚举与 ELO 计算

- [x] 1.1 `backend/src/Gomoku.Domain/Users/GameOutcome.cs`:`public enum GameOutcome { Loss = 0, Win = 1, Draw = 2 }` + XML 注释。
- [x] 1.2 `backend/src/Gomoku.Domain/EloRating/` 建子目录。
- [x] 1.3 `EloRating/EloRating.cs`:`public static class EloRating`,方法 `Calculate(int ratingA, int gamesA, int ratingB, int gamesB, GameOutcome outcomeA) : (int NewRatingA, int NewRatingB)`,按 spec 公式实现;`KFactor` 私有静态函数按 games 分段返回 40/20/10;`Math.Round(..., MidpointRounding.AwayFromZero)`。完整 XML 注释描述公式与 K 规则。

## 2. Domain — User.RecordGameResult

- [x] 2.1 在 `Gomoku.Domain/Users/User.cs` 加 `RecordGameResult(GameOutcome outcome, int newRating)` 方法:`GamesPlayed++` + switch 更新 Wins/Losses/Draws + `Rating = newRating`。未定义枚举值抛 `ArgumentOutOfRangeException`。
- [x] 2.2 XML 注释指出这是 elo-rating 能力的写入口;调用方(handler)必须先算好 newRating。

## 3. Domain 测试

- [x] 3.1 `Gomoku.Domain.Tests/Users/UserRecordGameResultTests.cs`:胜 / 负 / 平三基础用例 + 多局累积后不变量 `Wins+Losses+Draws==GamesPlayed` + 非法枚举值抛异常(断言状态不变)。~6 tests。
- [x] 3.2 `Gomoku.Domain.Tests/EloRating/EloRatingTests.cs` 表驱动(`[Theory]`):
  - 纯函数性:同入参三次调用返回相等
  - 同级对抗 K=20 胜/平/负(50 games / 50 games)
  - K 分段边界:games=29 / 30 / 99 / 100 各一组
  - 新手 vs 大师:games=0 vs 200,验证非对称变动
  - 上手输下手:ratingA>ratingB 但 outcomeA=Loss,断言 newA<ratingA & newB>ratingB
  - 舍入 AwayFromZero(构造一组入参让 delta 精确 0.5)—— 整数 rating 无法精确触发,改为直接断言 Math.Round 的两种模式差异以文档化选择。
  - 极端差值(2000 vs 1000)不抛 NaN
  ~10–12 tests。
- [x] 3.3 运行 `dotnet test tests/Gomoku.Domain.Tests` 全绿。

## 4. Application — 抽象扩展

- [x] 4.1 在 `IUserRepository.cs` 追加 `Task<IReadOnlyList<User>> GetTopByRatingAsync(int limit, CancellationToken cancellationToken);` + XML 注释指明排序规则由实现保证。

## 5. Application — DTO

- [x] 5.1 `Common/DTOs/LeaderboardEntryDto.cs`:`public sealed record LeaderboardEntryDto(int Rank, Guid UserId, string Username, int Rating, int GamesPlayed, int Wins, int Losses, int Draws);` + XML 注释强调不含敏感字段。

## 6. Application — GetLeaderboard feature

- [x] 6.1 `Features/Users/GetLeaderboard/GetLeaderboardQuery.cs`:`public sealed record GetLeaderboardQuery : IRequest<IReadOnlyList<LeaderboardEntryDto>>;`(空入参)。
- [x] 6.2 `Features/Users/GetLeaderboard/GetLeaderboardQueryHandler.cs`:
  - 常量 `private const int LeaderboardSize = 100;`
  - 调 `IUserRepository.GetTopByRatingAsync(LeaderboardSize, ct)`
  - `users.Select((u, i) => new LeaderboardEntryDto(i + 1, u.Id.Value, u.Username.Value, u.Rating, u.GamesPlayed, u.Wins, u.Losses, u.Draws))` → `ToList().AsReadOnly()`

## 7. Application — MakeMoveCommand handler 扩展

- [x] 7.1 修改 `Features/Rooms/MakeMove/MakeMoveCommandHandler.cs`:
  - 在 `await _uow.SaveChangesAsync(ct)` **之前**,若 `outcome.Result != GameResult.Ongoing`:
    - `var black = await _users.FindByIdAsync(room.BlackPlayerId, ct)` —— 不存在抛 `UserNotFoundException`(现有应用异常)
    - `var white = await _users.FindByIdAsync(room.WhitePlayerId!.Value, ct)` —— 同上
    - 按 `outcome.Result` switch 到 `outcomeForBlack: GameOutcome`(`BlackWin→Win`、`WhiteWin→Loss`、`Draw→Draw`)
    - `var outcomeForWhite = outcomeForBlack switch { Win => Loss, Loss => Win, Draw => Draw }`
    - `var (nb, nw) = EloRating.Calculate(black.Rating, black.GamesPlayed, white.Rating, white.GamesPlayed, outcomeForBlack)`
    - `black.RecordGameResult(outcomeForBlack, nb)` / `white.RecordGameResult(outcomeForWhite, nw)`
  - **然后**才 `SaveChangesAsync`,保持一个事务
  - Notifier 调用顺序维持原样(RoomStateChanged → MoveMade → GameEnded)
- [x] 7.2 上面新路径不影响 `Ongoing` 分支(性能无回归,不查 User)。

## 8. Application 测试

- [x] 8.1 `Features/Users/GetLeaderboard/GetLeaderboardQueryHandlerTests.cs`:
  - 空仓储 → 空列表,不抛
  - 3 个 User 按 Rating DESC 返回 → Rank 1/2/3
  - 仓储返回 5 个 → DTO Count 5,Rank 递增,字段映射正确
  - 断言 DTO 不含敏感字段(编译期即保证 —— 仅验证 property 数量)
- [x] 8.2 扩展 `Features/Rooms/MakeMove/MakeMoveCommandHandlerTests.cs`:
  - 成功未结束局:现有断言不变,**额外**断言双方 Rating/GamesPlayed 保持初值 1200/0(等价于"ELO 不走",避免对 `FindByIdAsync` 的 `Verify Never`:usernames lookup 本来就会调它)
  - 新测试:对局结束(黑胜)场景:mock `FindByIdAsync` 返回两位 User(初始 Rating=1200, GamesPlayed=0);断言 `black.Rating == 1220` / `white.Rating == 1180`;`SaveChangesAsync` 调用恰好一次
  - 新测试:对局平局(人为构造用例或直接 mock outcome.Result = Draw 不易实现;改在 Domain 层覆盖平局,在 Application 层只覆盖胜负至少一条路径即可)
- [x] 8.3 `dotnet test tests/Gomoku.Application.Tests` 全绿。

## 9. Infrastructure — Repository 实现

- [x] 9.1 `UserRepository.cs` 追加 `GetTopByRatingAsync(int limit, CancellationToken ct)`:
  ```csharp
  return await _db.Users
      .OrderByDescending(u => u.Rating)
      .ThenByDescending(u => u.Wins)
      .ThenBy(u => u.GamesPlayed)
      .Take(limit)
      .ToListAsync(cancellationToken);
  ```
  (Email / Username 是 OwnsOne,EF 会一并物化。)
- [x] 9.2 无 migration。确认 `dotnet build src/Gomoku.Infrastructure` 0 错。

## 10. Api — Leaderboard endpoint

- [x] 10.1 新 `Controllers/LeaderboardController.cs`:`[ApiController][Route("api/leaderboard")][Authorize]`,`GET` action `Get(CancellationToken ct)` → `ISender.Send(new GetLeaderboardQuery(), ct)` → `Ok(result)`。
- [x] 10.2 无 appsettings 变更;无 SignalR 变更。

## 11. 端到端冒烟

- [ ] 11.1 跑一盘完整对局(Alice vs Bob,黑胜):注册两位 → 创建房间 → 加入 → 走 9 步到连五。
- [ ] 11.2 `GET /api/users/me`(Alice):`Rating == 1220`,`GamesPlayed == 1`,`Wins == 1`。
- [ ] 11.3 `GET /api/users/me`(Bob):`Rating == 1180`,`GamesPlayed == 1`,`Losses == 1`。
- [ ] 11.4 `GET /api/leaderboard`(任一 token):返回 2 条,Rank 1 = Alice(1220)、Rank 2 = Bob(1180)。

> 11.x 是需要启动 API + 真实 HTTP/SignalR 客户端的手动操作,留给 PR 审核人/QA 执行;代码路径已在 Application 层单元测试中覆盖(`Winning_Move_Fires_All_Three_Events_Including_GameEnded` 断言 Rating 精确变更)。

## 12. 归档前置检查

- [x] 12.1 `dotnet build Gomoku.slnx`:0 警告 / 0 错。
- [x] 12.2 `dotnet test Gomoku.slnx`:全绿(Domain 167、Application 59,共 226)。
- [x] 12.3 Domain csproj 仍然 0 PackageReference / 0 ProjectReference。
- [x] 12.4 Application csproj 仍然只有 `Gomoku.Domain` 的 ProjectReference;无 EF / Infra。
- [x] 12.5 `grep -rEn "DateTime\.UtcNow" src/Gomoku.Application src/Gomoku.Domain`:无新增命中(仅 `IDateTimeProvider.cs` 的历史注释)。
- [x] 12.6 `openspec validate add-elo-system`:valid。
- [ ] 12.7 分支 `feat/add-elo-system`;按层分组 commit(Domain 1 个 / Application 1 个 / Infrastructure 1 个 / Api 1 个 / docs 1 个 / chore 1 个)。

> 12.7 留给用户按 PR 规范手动执行(branching + layered commits),本次实现产出的文件列表可作为分组依据。
