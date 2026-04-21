## Why

前端要做三件事,都依赖"按 id 读他人资料"与"按名字搜人":
- 点对局里的 `UserSummary.Username` → 跳对方个人主页(Rating / 战绩 / 注册时间)。
- "找朋友对战"输入框 → 名字前缀模糊搜索 → 点列表项进对方主页 / 发起邀请。
- `GET /api/users/{id}/games`(`add-game-replay`)返回的每条对局里的 UserSummary,前端再点进去看那个人的更多资料 —— 目前只有 `GET /api/users/me`,只能看自己。

所以要补两个只读端点:`GET /api/users/{id}`(单人公开主页)和 `GET /api/users?search=&page=&pageSize=`(搜索)。公开主页 DTO 只含 "社交可见" 字段,**明确**不带 Email / PasswordHash / RefreshToken。

## What Changes

- **Application**:
  - 新 DTO `UserPublicProfileDto(Guid Id, string Username, int Rating, int GamesPlayed, int Wins, int Losses, int Draws, DateTime CreatedAt)` —— 比 `UserSummaryDto` 多战绩,比 `UserDto`(`/me`)少 `Email`。
  - 新 feature `Features/Users/GetUserProfile/`:
    - `GetUserProfileQuery(UserId UserId) : IRequest<UserPublicProfileDto>`
    - handler:Load `User`(null → `UserNotFoundException` → 404);映射 DTO 返回。
    - **不**过滤 bot:bot 账号的 Guid / Username 已经是公开约定(migration seed),返回其"主页"让前端有统一消费路径(例如回放里看 "AI_Hard" 可以点进去看它的战绩)。
  - 新 feature `Features/Users/SearchUsers/`:
    - `SearchUsersQuery(string? Search, int Page, int PageSize) : IRequest<PagedResult<UserPublicProfileDto>>`。
    - Validator:`Page ≥ 1`,`PageSize ∈ [1, 100]`;`Search` **允许空**(等价于"浏览所有真人")。若非空,长度 ≤ 20(与 `Username` 最大长度对齐)。
    - Handler:调仓储分页 API;**过滤 bot**(排行榜与 search 同样"不列 bot");若 `Search` 非空 → StartsWith 前缀匹配(NOCASE);空 search → 返回所有真人按 Username ASC。
  - `IUserRepository` 新签名:`Task<(IReadOnlyList<User>, int Total)> SearchByUsernamePagedAsync(string? prefix, int page, int pageSize, CancellationToken)` —— 实现 `Where(!IsBot)` + 可选 `Where(Username.StartsWith(prefix, ignoreCase))` + `OrderBy(Username)` + Count + Skip/Take。
- **Infrastructure**:
  - `UserRepository.SearchByUsernamePagedAsync`:EF 翻译 StartsWith 到 SQL `Username LIKE 'prefix%' COLLATE NOCASE`(或 `.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)`—— EF 10 原生支持)。
- **Api**:
  - `UsersController` 新两个 actions:
    - `GET /api/users/{id:guid}` → `GetUserProfileQuery` → 200 + `UserPublicProfileDto`;404 用现有映射。
    - `GET /api/users?search=&page=&pageSize=` → `SearchUsersQuery` → 200 + `PagedResult<UserPublicProfileDto>`。
  - **URL 路径冲突注意**:`UsersController` 现有 `[Route("api/users")]` 下已有 `GET /api/users/me` → 必须保证 `{id:guid}` 路由**不**拦 "me" 字符串。`[HttpGet("{id:guid}")]` 用 route constraint `guid` 确保只匹配 Guid 格式,"me" 走独立 `[HttpGet("me")]`。
- **Tests**:
  - Handler 测试各 3-4 个(Success / NotFound / empty search / prefix match / pagination 边界 / 过滤 bot)。
  - Validator 测试 4 个(Page / PageSize 边界 / Search 长度)。

**显式不做**:
- "查询用户最近对局"作为主页 sub-endpoint → 前端拼 `/api/users/{id}/games`(已存在)即可。
- 按 Email / 注册时间搜索:隐私 + 搜索引擎复杂度,不值得。
- 用户名后缀 / Contains 搜索:StartsWith 已经覆盖多数场景;Contains 全表扫代价高。
- 搜索结果按 Rating / GamesPlayed 排序:默认 Username ASC 对"找人"直观。后续需要再加 `sortBy` 参数。
- 公开主页含 "当前是否在线"字段:归 `add-presence-tracking`(下一个 letter)。

## Capabilities

### Modified Capabilities

- **`user-management`** — 加 `UserPublicProfileDto`、两个新 query feature(`GetUserProfileQuery` / `SearchUsersQuery`)+ 对应 validator、`IUserRepository.SearchByUsernamePagedAsync` 签名、两个新 REST 端点 + 路由约束避免与 `/me` 冲突。

### New Capabilities

(无)

## Impact

- **代码规模**:~10 新 / 修改文件。
- **NuGet**:零。
- **HTTP 表面**:+2 端点。
- **SignalR**:零。
- **数据库**:零 schema;search 端点引入一次 CountAsync + 一次 paged query。
- **运行时**:搜索查询 ms 级(SQLite LIKE + NOCASE collation);量级上来后加 `Users(Username)` 的 UNIQUE 索引(已有)已能服务 StartsWith。
- **后续变更依赖**:前端主页页 / 找人对话框;`add-presence-tracking` 在主页 DTO 附加 "isOnline";`add-matchmaking` 用 Public 主页渲染对手信息。
