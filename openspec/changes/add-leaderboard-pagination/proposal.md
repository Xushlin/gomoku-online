## Why

`add-elo-system` 的 `GetLeaderboardQueryHandler` 硬编码 `LeaderboardSize = 100`,并把分页留给"后续变更"(当时 spec 明示 `MUST NOT 接受 query 参数`);`add-game-replay` 刚落地了 `PagedResult<T>` 通用分页容器;现在正是补齐这最后一块的时机 —— 把 `/api/leaderboard` 端点改为分页,并用同一个 `PagedResult<LeaderboardEntryDto>` 形状返回。

Rank 在分页场景下的语义仍然是"全局名次",不是页内序号:page=2 / pageSize=20 的第一个 entry 仍然叫 Rank=21。这由 handler 按 `(page - 1) * pageSize + i + 1` 算出来;Repository 只负责"过滤 + 排序 + skip/take"。

## What Changes

- **Application**:
  - `IUserRepository.GetTopByRatingAsync(int limit, ...)` **签名变**为 `GetLeaderboardPagedAsync(int page, int pageSize, ...)`:返回 `(IReadOnlyList<User> Users, int Total)` tuple。注:签名层面是**重命名 + 改形**,需要同步更新 `UserRepository`(基础设施)与 `GetLeaderboardQueryHandler`(Application)。旧 `GetTopByRatingAsync` 方法没其它调用方(仅 `GetLeaderboardQueryHandler` 一处),删除无破坏。
  - `GetLeaderboardQuery` **改为** `GetLeaderboardQuery(int Page, int PageSize) : IRequest<PagedResult<LeaderboardEntryDto>>`(原先是无参 record + `IReadOnlyList` 返回)。
  - 新 `GetLeaderboardQueryValidator`:`Page ≥ 1`,`PageSize` ∈ [1, 100];非法 400。
  - 重写 `GetLeaderboardQueryHandler`:
    - 调 `_users.GetLeaderboardPagedAsync(query.Page, query.PageSize, ct)`;
    - 对返回的 `users` 按顺序映射为 `LeaderboardEntryDto`,`Rank = (query.Page - 1) * query.PageSize + i + 1`(**全局 rank,不是页内**);
    - 包 `PagedResult<LeaderboardEntryDto>`。
- **Infrastructure**:
  - `UserRepository.GetTopByRatingAsync` 改名 / 改形为 `GetLeaderboardPagedAsync(int page, int pageSize, ct)`:
    - `Where(u => !u.IsBot)` 过滤(沿用 `add-ai-opponent` 约束);
    - `CountAsync` → Total;
    - `OrderByDescending(Rating).ThenByDescending(Wins).ThenBy(GamesPlayed).Skip((page-1)*pageSize).Take(pageSize)` → Users。
- **Api**:
  - `LeaderboardController.Get` 追加 `[FromQuery] int page = 1, [FromQuery] int pageSize = 20` 参数。返回类型变为 `PagedResult<LeaderboardEntryDto>`。
- **Tests**:
  - `Gomoku.Application.Tests/Features/Users/GetLeaderboard/GetLeaderboardQueryHandlerTests.cs`:现有测试需更新(签名 + 返回类型变化)——确认:
    - 成功路径:mock repo 返回 `(users, total)`;handler 返回 Items 数 + Total 正确;Rank 从 `(page-1)*pageSize + 1` 起递增。
    - page=2 / pageSize=2 / total=5:items.Count==2,Items[0].Rank==3,Total==5。
    - 空榜单:Items==[],Total==0。
  - `Gomoku.Application.Tests/Features/Users/GetLeaderboard/GetLeaderboardQueryValidatorTests.cs`(新文件):Page=0 / PageSize=0 / PageSize=101 全拒;Page=1 PageSize=100 通过。

**显式不做**(留给后续变更):
- Cursor-based pagination:量级小时 offset 够用。
- 按段位 / 时间范围 / 地区过滤:`add-leaderboard-filters`。
- 排行榜缓存层:目前 query 在 SQLite 上 ms 级,不必。
- 把 `GetTopByRatingAsync` 标记为 `[Obsolete]` 保留一段时间 —— 只有 handler 一个调用方,直接删无风险。

## Capabilities

### New Capabilities

(无)

### Modified Capabilities

- **`elo-rating`** —
  - 仓储签名 `GetTopByRatingAsync(int limit)` → `GetLeaderboardPagedAsync(int page, int pageSize)`(返回 tuple)。
  - `GetLeaderboardQuery` 新增 `Page` / `PageSize` 字段,返回类型 `PagedResult<LeaderboardEntryDto>`。
  - `GET /api/leaderboard` **接受** `page` / `pageSize` query 参数(原先 spec 明示"MUST NOT 接受"—— 本变更明确解除该禁用)。
  - `Rank` 的定义维持**全局**名次,不受分页影响。

## Impact

- **代码规模**:~5 新 / 修改文件(仓储 / 接口 / handler / validator / controller)+ ~3 测试更新 / 新增。最小的一次变更。
- **NuGet**:零。
- **HTTP 表面**:端点路径不变;query 参数从"无"扩展为 `page` / `pageSize`。**未传参时行为变化**:之前返回 `LeaderboardEntryDto[]` 最多 100 条,现在返回 `PagedResult<...>` 默认 page=1 / pageSize=20。前端会感知 —— 是 breaking change。
- **SignalR 表面**:零。
- **数据库**:零 schema 变化;查询多一次 `CountAsync`。
- **后续变更将依赖**:前端"排行榜分页器"UI;`add-leaderboard-filters` 复用 PagedResult。
