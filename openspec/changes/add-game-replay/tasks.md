## 1. Application — DTOs

- [x] 1.1 `Common/DTOs/PagedResult.cs`:`public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);` + XML 注释。
- [x] 1.2 `Common/DTOs/GameReplayDto.cs`:按 design D2 定义字段。
- [x] 1.3 `Common/DTOs/UserGameSummaryDto.cs`:按 design D3 定义字段(不含 Host / Moves;含 `MoveCount`)。

## 2. Application — Exception

- [x] 2.1 `Common/Exceptions/GameNotFinishedException.cs`:sealed,继承 `System.Exception`,`(string message)` 构造;XML 注释指出 "Api 映射 HTTP 409"。

## 3. Application — `IRoomRepository` 扩展

- [x] 3.1 `Gomoku.Application/Abstractions/IRoomRepository.cs` 追加:
  ```
  Task<(IReadOnlyList<Room> Rooms, int Total)> GetUserFinishedGamesPagedAsync(
      UserId userId, int page, int pageSize, CancellationToken cancellationToken);
  ```
  XML 注释:Status=Finished,按 Game.EndedAt DESC,Include Game + Moves(算 MoveCount 用)。

## 4. Application — `GetGameReplay` feature

- [x] 4.1 `Features/Rooms/GetGameReplay/GetGameReplayQuery.cs`:`record GetGameReplayQuery(RoomId RoomId) : IRequest<GameReplayDto>`。
- [x] 4.2 `Features/Rooms/GetGameReplay/GetGameReplayQueryHandler.cs`:
  - Load room(null → RoomNotFoundException);
  - `Status != Finished` 或 `Game == null` → `GameNotFinishedException`;
  - 注意:White 必有(Finished 要求先 Playing 要求先 JoinAsPlayer);黑方就是 Host;
  - `LookupUsernamesAsync(room.CollectUserIds(), ct)`;
  - moves `OrderBy(m => m.Ply).Select(MoveDto)`;
  - 构造 `GameReplayDto` 返回。

## 5. Application — `GetUserGamesPaged` feature

- [x] 5.1 `Features/Users/GetUserGames/GetUserGamesPagedQuery.cs`:`record GetUserGamesPagedQuery(UserId UserId, int Page, int PageSize) : IRequest<PagedResult<UserGameSummaryDto>>`。
- [x] 5.2 `Features/Users/GetUserGames/GetUserGamesPagedQueryValidator.cs`:
  - `Page >= 1`;`PageSize >= 1`;`PageSize <= 100`;失败 ValidationException → 400。
- [x] 5.3 `Features/Users/GetUserGames/GetUserGamesPagedQueryHandler.cs`:
  - 调 `IRoomRepository.GetUserFinishedGamesPagedAsync`;
  - 对返回的 rooms 聚合所有 UserId(调 `CollectUserIds()` 合并 distinct);
  - `LookupUsernamesAsync`;
  - map rooms → `UserGameSummaryDto[]`:Black / White 从 usernames 构造,Result/WinnerUserId/EndReason 从 `game.Result/WinnerUserId/EndReason`,MoveCount = `game.Moves.Count`;
  - 返回 `new PagedResult<UserGameSummaryDto>(items, total, page, pageSize)`。

## 6. Infrastructure

- [x] 6.1 `RoomRepository.GetUserFinishedGamesPagedAsync`:
  ```csharp
  var baseQuery = _db.Rooms
      .Where(r => r.Status == RoomStatus.Finished)
      .Where(r => r.BlackPlayerId == userId ||
                  (r.WhitePlayerId.HasValue && r.WhitePlayerId.Value == userId));
  var total = await baseQuery.CountAsync(cancellationToken);
  var rooms = await baseQuery
      .Include(r => r.Game!).ThenInclude(g => g.Moves)
      .OrderByDescending(r => r.Game!.EndedAt)
      .Skip((page - 1) * pageSize).Take(pageSize)
      .ToListAsync(cancellationToken);
  return (rooms, total);
  ```
  注意:`UserId` 是 value object,EF 有 `UserIdConverter` 应能直接比较;若翻译不通改为 `r.BlackPlayerId == userId`(依赖 HasConversion 的表达)—— 必要时改走 `r.BlackPlayerId.Value == userId.Value`。

## 7. Api

- [x] 7.1 `RoomsController` 新 action:
  ```csharp
  [HttpGet("{id:guid}/replay")]
  public async Task<ActionResult<GameReplayDto>> Replay(Guid id, CancellationToken ct)
  {
      var dto = await _mediator.Send(new GetGameReplayQuery(new RoomId(id)), ct);
      return Ok(dto);
  }
  ```
- [x] 7.2 `UsersController` 新 action:
  ```csharp
  [HttpGet("{id:guid}/games")]
  public async Task<ActionResult<PagedResult<UserGameSummaryDto>>> Games(
      Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
  {
      var result = await _mediator.Send(new GetUserGamesPagedQuery(new UserId(id), page, pageSize), ct);
      return Ok(result);
  }
  ```
- [x] 7.3 `ExceptionHandlingMiddleware`:`GameNotFinishedException` 加入现有 409 分支。

## 8. Application 测试

- [x] 8.1 `Features/Rooms/GetGameReplayQueryHandlerTests.cs`(~4):
  - 成功:构造一个 Finished Room(通过 Room.Create → JoinAsPlayer → 数次 PlayMove → 黑方连五),mock repo 返回该 room;断言返回 DTO 字段正确:Result=BlackWin、EndReason=Connected5、MoveCount 正确、moves 按 Ply 升序。
  - Room 不存在 → RoomNotFoundException(mock repo 返回 null)。
  - Waiting 房间 → GameNotFinishedException。
  - Playing 房间(创建并 JoinAsPlayer 但不走到 Finished) → GameNotFinishedException。
- [x] 8.2 `Features/Users/GetUserGamesPagedQueryHandlerTests.cs`(~4):
  - 成功:mock repo 返回 3 个 Finished Room + total=3;map 后 items.Count=3,Total=3。
  - 空:mock 返回 ([], 0);返回空 list,Total=0。
  - 分页 page 超出范围(page=10,pageSize=20,total=5 → items=[], total=5)。
  - MoveCount 正确:room.Game.Moves.Count 直接反映到 DTO。
- [x] 8.3 `Features/Users/GetUserGamesPagedQueryValidatorTests.cs`(~3):
  - `Page=0` → 验证失败。
  - `PageSize=0` → 失败。
  - `PageSize=101` → 失败。
  - Valid 组合(Page=1,PageSize=20)通过。
- [x] 8.4 `dotnet test tests/Gomoku.Application.Tests`:93 → 约 104 全绿。

## 9. 端到端冒烟

- [x] 9.1 启动 Api,注册 Alice + Bob;Alice 创建房间 → Bob join;Alice 认输 → 房间 Finished。
- [x] 9.2 `GET /api/rooms/{id}/replay`(Alice token):200 + GameReplayDto,`Result == WhiteWin`,`EndReason == Resigned`,`Moves` 为空数组(认输没落子 —— 如果要验证 moves 非空,先 Alice 走一两步再认输)。
- [x] 9.3 `GET /api/users/{alice.id}/games?page=1&pageSize=10`:200 + PagedResult,`Items.length == 1`,`Items[0].Result == WhiteWin`,`Total == 1`。
- [x] 9.4 `GET /api/rooms/{某 Playing 房间}/replay`:409(GameNotFinishedException)。
- [x] 9.5 `GET /api/users/{bob.id}/games?page=0&pageSize=10`:400(validator)。
- [x] 9.6 `GET /api/rooms/{某 Waiting 房间}/replay`:409。

## 10. 归档前置检查

- [x] 10.1 `dotnet build Gomoku.slnx`:0 警告 0 错。
- [x] 10.2 `dotnet test Gomoku.slnx`:全绿(Domain 230 不变;Application 93 → 约 104)。
- [x] 10.3 Domain csproj 仍 0 PackageReference / 0 ProjectReference。
- [x] 10.4 Application csproj 无新 NuGet。
- [x] 10.5 `openspec validate add-game-replay --strict`:valid。
- [x] 10.6 分支 `feat/add-game-replay`,按层分组 commit(Application / Infrastructure / Api / docs-openspec 四条;Domain 零改动)。
