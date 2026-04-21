## MODIFIED Requirements

### Requirement: `IUserRepository.GetTopByRatingAsync(int limit)` 返回按排行榜顺序的用户列表

Application 层 SHALL 在 `IUserRepository` 上将旧的 `GetTopByRatingAsync(int limit, ...)` **替换**为:

```
Task<(IReadOnlyList<User> Users, int Total)> GetLeaderboardPagedAsync(
    int page, int pageSize, CancellationToken cancellationToken);
```

实现 MUST:
1. 过滤 `IsBot == false`(沿用 `add-ai-opponent` 约束);bot 不上榜。
2. 先做一次 `CountAsync` 得 Total(即"真人总数")。
3. 按 `(Rating DESC, Wins DESC, GamesPlayed ASC)` 排序,`Skip((page-1)*pageSize).Take(pageSize)` 物化到 Users。
4. 返回 `(Users, Total)` tuple。

返回类型 MUST 是领域类型(`IReadOnlyList<User>` + `int`),不泄漏 `IQueryable` / `IOrderedEnumerable` 等 EF 细节。

签名 rename 的原因:语义上本函数是"分页拉榜",而不是"取 Top N";避免一份方法既支持 limit 又支持 paged 的二义性。原 `GetTopByRatingAsync` 只有 `GetLeaderboardQueryHandler` 一个调用方,同步迁移即可。

#### Scenario: 排序正确(真人)
- **WHEN** 数据库有三位真人:A(Rating=1500, Wins=2)、B(Rating=1500, Wins=5)、C(Rating=1400, Wins=10),调 `GetLeaderboardPagedAsync(1, 100, ct)`
- **THEN** Users 顺序 `[B, A, C]`;Total = 3

#### Scenario: 按 GamesPlayed ASC 作为三级排序
- **WHEN** 两位真人 `(Rating=1500, Wins=3, GamesPlayed=10)` 与 `(Rating=1500, Wins=3, GamesPlayed=5)`
- **THEN** 后者(场次少)排前

#### Scenario: 分页跳过
- **WHEN** 数据库有 5 位真人,调 `GetLeaderboardPagedAsync(2, 2, ct)`
- **THEN** Users.Count == 2(第 3、4 名);Total == 5

#### Scenario: 过大 page 空结果
- **WHEN** 数据库有 5 位真人,调 `GetLeaderboardPagedAsync(10, 2, ct)`
- **THEN** Users.Count == 0;Total 仍然 == 5(客户端可按 Total 算"无更多"或回第 1 页)

#### Scenario: Bot 被过滤
- **WHEN** 数据库有 5 位真人和 3 位 bot
- **THEN** Users.Count ≤ 5(视分页);Total == 5(仅真人)

#### Scenario: 仅 bot 的极端情形
- **WHEN** 数据库仅存在 bot 账号
- **THEN** 返回空 Users 列表,Total == 0

---

### Requirement: `GetLeaderboardQueryHandler` 分配 Rank 并映射 DTO

Application 层 SHALL 把 `GetLeaderboardQuery` 改为接受 `Page` / `PageSize` 参数,返回 `PagedResult<LeaderboardEntryDto>`:

```
public sealed record GetLeaderboardQuery(int Page, int PageSize)
    : IRequest<PagedResult<LeaderboardEntryDto>>;
```

SHALL 同时新增 `GetLeaderboardQueryValidator`:

- `Page ≥ 1`,否则 `ValidationException` → HTTP 400。
- `PageSize` ∈ [1, 100]。

Handler 流程:

1. 调 `IUserRepository.GetLeaderboardPagedAsync(Page, PageSize, ct)` → `(users, total)`
2. 按顺序映射 Users → `LeaderboardEntryDto`;
3. **Rank 是全局名次**,按公式 `Rank = (Page - 1) * PageSize + i + 1`(`i` 是本页 0-based 下标)计算,使 page=2 pageSize=20 的第一个 entry 的 Rank == 21。
4. 包 `PagedResult<LeaderboardEntryDto>(Items, Total, Page, PageSize)` 返回。

DTO 定义 MUST 精确包含 8 个字段(沿用 `add-elo-system`);MUST NOT 泄漏 `Email` / `PasswordHash` / refresh token 相关字段。

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

---

### Requirement: `GET /api/leaderboard` 端点返回前 100 条排行榜

Api 层 SHALL 暴露 `GET /api/leaderboard`,要求 `[Authorize]`。成功响应 HTTP 200 + `PagedResult<LeaderboardEntryDto>`(`Items` 最多 `PageSize` 条,Total 是过滤 bot 后的真人总数)。

本 Requirement **修订** `add-elo-system` 原来的 `MUST NOT 接受 query 参数`:

- 端点 SHALL 接受 query `page`(默认 1)和 `pageSize`(默认 20)。
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
