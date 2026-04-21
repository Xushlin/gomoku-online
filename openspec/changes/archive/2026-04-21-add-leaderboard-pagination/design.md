## Context

`add-elo-system` 交付排行榜时,明确留下 "MUST NOT 接受 query 参数;排行榜分页 / 搜索 / 按时间范围过滤 —— 本次固定 top 100 一次吐完;将来 `add-leaderboard-pagination` 再细化"。`add-game-replay` 交付了 `PagedResult<T>` 通用容器。这轮把这两头接上。

## Goals / Non-Goals

**Goals**:
- `/api/leaderboard` 支持 `page` / `pageSize` query 参数;默认 page=1 / pageSize=20。
- 返回 `PagedResult<LeaderboardEntryDto>`(与 `add-game-replay` 的 `GET /api/users/{id}/games` 同形状)。
- `Rank` 仍然是**全局名次**,不随分页重置;第 N 页第 1 条仍然携带 `(N-1)*pageSize + 1` 的 Rank。
- 非法分页参数返回 400(复用 `add-game-replay` 的 validator 风格)。

**Non-Goals**:
- Cursor-based pagination。
- 按段位 / 时间范围 / 地区过滤。
- 缓存层。
- 旧接口(`IReadOnlyList<LeaderboardEntryDto>`)的兼容 shim:只有 Controller 一个调用方,直接改形。

## Decisions

### D1 — 仓储签名:`GetLeaderboardPagedAsync`(rename + tuple)

旧:
```csharp
Task<IReadOnlyList<User>> GetTopByRatingAsync(int limit, CancellationToken ct);
```

新:
```csharp
Task<(IReadOnlyList<User> Users, int Total)> GetLeaderboardPagedAsync(
    int page, int pageSize, CancellationToken ct);
```

rename 的原因:`GetTopByRating` 的语义是"取 Top N",`GetLeaderboardPaged` 更直接点出"分页拉榜"。避免一份方法名既支持 limit 又支持 paged 的二义性。

**唯一调用方**是 `GetLeaderboardQueryHandler`,handler 同步重写;无需保留旧签名。

### D2 — Rank 是全局名次

`LeaderboardEntryDto.Rank`:page=2 pageSize=20 的第一个 entry MUST 有 Rank=21,不是 1。计算公式:

```csharp
Rank = (query.Page - 1) * query.PageSize + i + 1
```

其中 `i` 是当前页内 0-based 下标。

这对前端含义明确:"我在排行榜全局的名次是 21 名"。若前端要显示"本页第 1 行",用 `i` 自己算即可。

### D3 — 与 `add-game-replay` 对齐的 Validator

复用同一风格:
- `Page ≥ 1`
- `PageSize ∈ [1, 100]`
- 非法抛 `ValidationException` → 400(由 `ValidationBehavior` 拦截)

Default 参数 `page=1, pageSize=20`(Controller 的 `[FromQuery] int page = 1`),与 user-games 一致。

### D4 — Bot 过滤语义不变

仓储层继续 `Where(u => !u.IsBot)` 过滤;Total 也是过滤后的真人总数。

### D5 — 既有测试更新点

`GetLeaderboardQueryHandlerTests.cs` 原有:
- 3 个 User → 返回 Rank 1/2/3(现在要改为 Rank 1/2/3 默认 page=1)
- 5 个 User → Count=5、Rank 递增
- 空仓储 → 空列表

这些保留核心断言,但要:
- Handler 构造时不变;调用 `Handle(new GetLeaderboardQuery(Page:1, PageSize:20))`。
- 返回类型 `PagedResult<...>`,断言 `result.Items` 而非 `result` 本身。
- 新增用例:page=2 / pageSize=2 / total=5 → Items[0].Rank=3。

## Risks / Trade-offs

| 风险 | 影响 | 缓解 |
|---|---|---|
| 既有前端调用 `/api/leaderboard` 预期 `Array<LeaderboardEntryDto>`,现改为 `PagedResult<...>` | Breaking change | 目前前端尚未开发;无实际客户 |
| `page * pageSize` 越界(page=1000000)| 仓储 Skip 到超大值,CountAsync 仍跑 | validator 限 pageSize≤100;page 不限(前端自行保护);生产量级内不成问题 |

## Migration Plan

无 DB migration。
