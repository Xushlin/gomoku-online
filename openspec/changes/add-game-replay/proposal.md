## Why

CLAUDE.md 开篇列的核心功能里写着"**游戏记录存储与回放**"—— 我们已经**存了**(Room / Game / Moves 从 `add-rooms-and-gameplay` 开始就 EF Cascade 持久化),但**查不出来**:

- `GET /api/rooms/{id}` 只在 Playing / Waiting 时提供完整状态;Finished 房间同样能查但没有"这是我过去下过的棋"的入口。
- 没有"给我看 Alice 下过的所有棋"的 API。
- 前端想做"战绩页 + 点某一局回放"—— 后端缺支撑。

这轮补两个 HTTP 端点:

1. `GET /api/rooms/{id}/replay` —— 按 Ply 顺序返回一局 Finished 对局的完整 moves + 元数据(黑 / 白、开始 / 结束时间、结果、EndReason)。Playing 房间请求此端点 → 409。
2. `GET /api/users/{id}/games?page=1&pageSize=20` —— 分页返回该用户参与过的 Finished 对局列表(按 EndedAt 降序)。

数据模型零改 —— 所有需要的字段在现有 `Room` / `Game` / `Move` 表里已经齐全(`add-timeout-resign` 补了 `EndReason`,`add-elo-system` 补了战绩)。

## What Changes

- **Domain**:
  - 零改动。所有数据已在现有聚合里。
- **Application**:
  - 新 DTOs:
    - `PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)` —— 通用分页容器。
    - `GameReplayDto(Guid RoomId, string Name, UserSummaryDto Host, UserSummaryDto Black, UserSummaryDto White, DateTime StartedAt, DateTime EndedAt, GameResult Result, Guid? WinnerUserId, GameEndReason EndReason, IReadOnlyList<MoveDto> Moves)` —— 完整对局回放。
    - `UserGameSummaryDto(Guid RoomId, string Name, UserSummaryDto Black, UserSummaryDto White, DateTime StartedAt, DateTime EndedAt, GameResult Result, Guid? WinnerUserId, GameEndReason EndReason, int MoveCount)` —— 个人战绩列表项。
  - 新 Exception:`GameNotFinishedException`(`add-timeout-resign` 的 `TurnNotTimedOut` 之后的又一个 409 类型)—— 映射 HTTP 409。
  - 新 Feature `Features/Rooms/GetGameReplay/`:`GetGameReplayQuery(RoomId) : IRequest<GameReplayDto>` + handler。
    - Load room (null → RoomNotFoundException);
    - Status != Finished → `GameNotFinishedException`("Replay is only available for finished games.");
    - lookup usernames for Host/Black/White(用现有 `UsernameLookup.LookupUsernamesAsync`);
    - 构造 `GameReplayDto`;moves 按 Ply 升序。
  - 新 Feature `Features/Users/GetUserGames/`:`GetUserGamesPagedQuery(UserId, int Page, int PageSize) : IRequest<PagedResult<UserGameSummaryDto>>` + handler + validator。
    - Validator:`Page >= 1`;`PageSize` 在 [1, 100] 区间。
    - Handler:调 `IRoomRepository.GetUserFinishedGamesPagedAsync(userId, page, pageSize, ct)` → 返回 `(rooms, total)`;lookup usernames + 映射 `UserGameSummaryDto`;封 PagedResult。
  - `IRoomRepository` 新签名:
    - `Task<(IReadOnlyList<Room> Rooms, int Total)> GetUserFinishedGamesPagedAsync(UserId userId, int page, int pageSize, CancellationToken ct)`。
    - 实现 MUST 过滤 `Status == Finished`,`BlackPlayerId == userId OR WhitePlayerId == userId`;按 `Game.EndedAt DESC` 排序;`Skip((page-1) * pageSize).Take(pageSize)`;分页之前的 `Total` 计数先做一次。
- **Infrastructure**:
  - `RoomRepository.GetUserFinishedGamesPagedAsync` 实现:两条查询(Count + Page),`Include(r => r.Game!).ThenInclude(g => g.Moves)` —— moves 用于算 `MoveCount`(或前端直接用 `Game.Moves.Count`)。
- **Api**:
  - `RoomsController` 新 action:`GET /api/rooms/{id}/replay` → `GetGameReplayQuery` → 200 + `GameReplayDto`。`[Authorize]`。
  - `UsersController` 新 action:`GET /api/users/{id}/games?page=1&pageSize=20` → `GetUserGamesPagedQuery` → 200 + `PagedResult<UserGameSummaryDto>`。`[Authorize]`。
  - `ExceptionHandlingMiddleware`:`GameNotFinishedException` 合入现有 409 分支。
- **Tests**:
  - `Gomoku.Application.Tests/Features/Rooms/GetGameReplayQueryHandlerTests.cs`(~4):
    - 成功(Finished 房间):返回的 DTO 字段正确、moves 按 Ply 升序、玩家 usernames 填充。
    - Room 不存在 → RoomNotFoundException。
    - Room 在 Playing → GameNotFinishedException。
    - Room 在 Waiting → GameNotFinishedException(Waiting 房的 Game == null)。
  - `Gomoku.Application.Tests/Features/Users/GetUserGamesPagedQueryHandlerTests.cs`(~4):
    - 成功(用户参与过 3 局,全部 Finished):按 EndedAt DESC 排序,Total=3,page 1/20 返回 3。
    - 分页边界:5 局,page=1 pageSize=2 → 返回 2 + Total=5;page=3 → 返回 1;page=4 → 空 + Total=5。
    - 用户参与 0 局:空列表 + Total=0(不抛)。
    - 非法分页参数 validator:`page=0` / `pageSize=0` / `pageSize=101` → ValidationException。

**显式不做**(留给后续变更):
- 回放端点返回**动画 / 步骤播放控制**的 API(`/replay/step/5`):本次只给 moves 完整数组,动画由前端实现。
- 回放在 Playing 房间(当前进行中的棋的"半场回放"):Playing 房间用现有 `GET /api/rooms/{id}` 的 RoomStateDto 已经够(含 moves)。
- 按用户名 / 日期范围 / 对手 / 结果的高级搜索:留给 `add-game-search`。
- Cursor-based pagination(传 `continuationToken` 而非 `page / pageSize`):offset-page 对战绩页量级(单用户 < 几百局)足够。量级大后再做。
- 围观者的 "历史围观记录":`RoomSpectator` 子表有数据,本次不暴露;对 gomoku 不是核心。
- 导出对局为 gomoku PSN / RGF 等国际棋谱格式:留给 `add-game-export`。
- AI 对局回放时隐藏 bot 的 "思考耗时 / depth":bot 的 move 在 MoveDto 里跟人类 move 无差别显示。

## Capabilities

### New Capabilities

- **`game-replay`** — 两个 HTTP 端点暴露 Finished 对局的完整 moves 历史(按 Room)与用户维度的战绩分页(按 User);配套 DTO / 异常 / 仓储查询。

### Modified Capabilities

(无)

## Impact

- **代码规模**:~10 新文件(DTO 3 个 + 2 features + 2 handler tests + Repo impl + exception + PagedResult)+ 少量 controller / middleware / csproj 改动。
- **NuGet**:零。
- **HTTP 表面**:+2 端点。
- **SignalR 表面**:零。
- **数据库**:零 schema 变化;新增 2 条 SQL 查询(按 UserId 过滤 + 分页)。
- **运行时**:每次 `/replay` 一次 DB round trip;每次 `/games` 两次(count + page);量级小。
- **后续变更将依赖**:`add-game-search` 复用 PagedResult / UserGameSummaryDto 的字段;前端"战绩页 / 回放页"直接消费本次 DTO。
