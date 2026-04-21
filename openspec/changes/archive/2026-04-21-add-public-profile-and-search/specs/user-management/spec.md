## ADDED Requirements

### Requirement: `UserPublicProfileDto` 是他人可见的用户资料快照

Application 层 SHALL 在 `Common/DTOs/UserPublicProfileDto.cs` 定义:

```
public sealed record UserPublicProfileDto(
    Guid Id,
    string Username,
    int Rating,
    int GamesPlayed,
    int Wins,
    int Losses,
    int Draws,
    DateTime CreatedAt);
```

DTO MUST NOT 含 `Email` / `PasswordHash` / `RefreshTokens` / `IsActive` / `IsBot` 字段。比起
`UserSummaryDto`(仅 Id + Username)更完整;比起 `UserDto`(`/me`)少 Email。

#### Scenario: 反射检查无敏感字段
- **WHEN** 审阅 `UserPublicProfileDto` 的 public properties
- **THEN** 属性集合精确为 `{Id, Username, Rating, GamesPlayed, Wins, Losses, Draws, CreatedAt}`

---

### Requirement: `GET /api/users/{id}` 按 Id 返回公开用户主页

Api 层 SHALL 暴露 `GET /api/users/{id:guid}`(`[Authorize]`):

- Controller 调 `GetUserProfileQuery(new UserId(id))`;
- Handler Load user;null 抛 `UserNotFoundException` → HTTP 404;
- **不过滤 bot**:允许查询 bot 账号(`BotAccountIds.Easy` / `Medium` / `Hard`)返回其资料,让前端回放中对 `AI_Hard` 的链接能正常展示战绩。
- 成功 HTTP 200 + `UserPublicProfileDto`。

路由约束 `{id:guid}` 保证 `GET /api/users/me` **不**被该 action 拦截 —— "me" 不是合法 Guid。

#### Scenario: 真人主页
- **WHEN** 登录用户 `GET /api/users/{aliceGuid}`,alice 是真人
- **THEN** HTTP 200;Body 含 Rating / 战绩 / CreatedAt;**不**含 Email

#### Scenario: Bot 主页也可查
- **WHEN** `GET /api/users/{BotAccountIds.Easy}`
- **THEN** HTTP 200;Username == "AI_Easy";战绩字段正常反映 bot 历史对局

#### Scenario: 找不到
- **WHEN** 请求不存在的 `Guid`
- **THEN** HTTP 404 `UserNotFoundException`

#### Scenario: `/me` 不被误拦
- **WHEN** `GET /api/users/me`(调用者登录)
- **THEN** HTTP 200;走既有 `Me` action,返回 `UserDto`(含 Email)—— 路由约束 `{id:guid}` 确保 "me" 不匹配

#### Scenario: 未登录
- **WHEN** 无 Bearer token
- **THEN** HTTP 401

---

### Requirement: `GET /api/users?search=&page=&pageSize=` 按用户名前缀搜索真人

Api 层 SHALL 暴露 `GET /api/users`(`[Authorize]`),接受 query:

- `search: string?` —— 可选;非空时按 Username **前缀**(大小写不敏感)过滤;空则返回所有真人。
- `page: int`(默认 1,`≥ 1`)
- `pageSize: int`(默认 20,`[1, 100]`)

Validator `SearchUsersQueryValidator`:`Page ≥ 1`、`PageSize ∈ [1, 100]`、`Search` 非空时 `Length ≤ 20`(与 `Username` 最大长度对齐);非法 HTTP 400。

Handler 调 `IUserRepository.SearchByUsernamePagedAsync(Search, Page, PageSize, ct)`,映射 `UserPublicProfileDto`,包 `PagedResult` 返回。

仓储实现 MUST:
- `Where(u => !u.IsBot)` —— bot **不**出现在搜索结果;
- 若 `prefix` 非空 → `Username LIKE prefix%`(case-insensitive,SQLite 靠 NOCASE collation;EF 翻译 `ToLower().StartsWith`);
- `OrderBy(Username ASC)`;
- `CountAsync` + `Skip((page-1)*pageSize).Take(pageSize)`;
- 返回 `(IReadOnlyList<User>, int Total)` tuple。

#### Scenario: 前缀匹配
- **WHEN** 数据库有 Alice / AliceB / Bob / Carol + 3 bot;调 `GET /api/users?search=Ali`
- **THEN** HTTP 200;`Items` 含 Alice + AliceB(Username ASC);**不**含 Bob / Carol / bot;`Total == 2`

#### Scenario: 大小写不敏感
- **WHEN** `search=ALI`
- **THEN** 同上(仍匹配 Alice / AliceB)

#### Scenario: 空 search 返回所有真人
- **WHEN** `GET /api/users`(不带 search)
- **THEN** HTTP 200;Items 含所有真人按 Username ASC;bot 不在

#### Scenario: 分页
- **WHEN** 5 个真人匹配某前缀,`page=2&pageSize=2`
- **THEN** Items.Count == 2(第 3、4 个);Total == 5

#### Scenario: 非法参数
- **WHEN** `pageSize=101` 或 `page=0` 或 `search=超过 20 字符的字符串...`
- **THEN** HTTP 400 `ValidationException`

#### Scenario: 未登录
- **WHEN** 无 Bearer token
- **THEN** HTTP 401

---

### Requirement: `IUserRepository.SearchByUsernamePagedAsync` 分页 + 前缀 + bot 过滤

Application 层 SHALL 在 `IUserRepository` 上新增:

```
Task<(IReadOnlyList<User> Users, int Total)> SearchByUsernamePagedAsync(
    string? prefix, int page, int pageSize, CancellationToken cancellationToken);
```

实现 MUST:
1. 过滤 `!IsBot`(搜索不应出现 bot)。
2. 若 `prefix` 非空,按 Username 大小写不敏感的**前缀匹配**过滤。
3. 按 `Username ASC` 排序。
4. `CountAsync` → Total;`Skip((page-1)*pageSize).Take(pageSize)` → Users 物化。
5. 返回 `(Users, Total)` tuple。

签名 MUST 不暴露 `IQueryable` 等 EF 类型。

#### Scenario: Bot 过滤
- **WHEN** 库里有 3 真人(含 Alice)+ 3 bot(AI_Easy/Medium/Hard),调 `SearchByUsernamePagedAsync(null, 1, 100, ct)`
- **THEN** Users.Count == 3(仅真人);Total == 3

#### Scenario: 前缀 + 分页
- **WHEN** 库里有 5 个 "Al" 前缀真人,调 `SearchByUsernamePagedAsync("Al", 2, 2, ct)`
- **THEN** Users.Count == 2(第 3、4 个);Total == 5
