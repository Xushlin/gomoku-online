## Context

`Room / Game / Move` 三张表从 `add-rooms-and-gameplay` 起就完整持久化,`add-timeout-resign` 补了 `EndReason`。数据是齐的,只差"查询端点 + DTO"。

本变更纯读路径 —— 不动 Domain、不动聚合、不写数据库。工作量小,作用域清晰,和 F 号位期望一致。

## Goals / Non-Goals

**Goals**:
- 任一 Finished 房间的完整 moves 历史可通过 GET 取回,供前端做回放动画。
- 用户维度的战绩列表(按 EndedAt DESC 分页),供前端做战绩页。
- 返回的 DTO 字段**自说明**:结果、胜方、结束原因、move 数量都在 payload 里,客户端无需二次请求。
- 分页参数 `page` / `pageSize` 有 validator,非法输入返回 400。

**Non-Goals**:
- 回放的"步骤播放"服务端控制(/replay/step/N):前端自行 slice moves 数组。
- 高级搜索 / 过滤(按对手 / 结果 / 时间):`add-game-search`。
- Cursor-based pagination:offset-based 已足够。
- 围观者历史列表。
- 棋谱格式导出(RGF / PSN)。
- 实时"正在进行的对局"的回放(用现有 `GET /api/rooms/{id}` 的 RoomStateDto)。

## Decisions

### D1 — 回放端点独立于 `GET /api/rooms/{id}`

`GET /api/rooms/{id}` 返回的 `RoomStateDto` 已经含 moves,理论上能覆盖回放。但:

- 语义不同:`/rooms/{id}` 是"房间当前状态"(含 chat 消息、围观者列表),`/rooms/{id}/replay` 是"对局成品 + 结果"。
- 回放场景不需要 chat / spectator 列表;DTO 更小更专注。
- 未来可能在 `/replay` 端点加入"隐藏围观 chat"、"只返游戏结束之前的 moves"等差异化,不污染 `RoomStateDto`。

**考虑过但弃用**:让 `/rooms/{id}` 在 Finished 状态时多返回一个 `replay` 字段 —— DTO 变 union,前端解析麻烦。

### D2 — `GameReplayDto` 字段设计

```csharp
public sealed record GameReplayDto(
    Guid RoomId,
    string Name,
    UserSummaryDto Host,
    UserSummaryDto Black,
    UserSummaryDto White,
    DateTime StartedAt,
    DateTime EndedAt,
    GameResult Result,
    Guid? WinnerUserId,
    GameEndReason EndReason,
    IReadOnlyList<MoveDto> Moves);
```

- `Host` 与 `Black` 在现有设计下**必然相同**(创建者默认黑方),但分别返回 —— 前端可能显示 "Host" 标签;若将来"非 Host 黑方"出现(custom room 设置)能兼容。
- `Moves` 按 `Ply` 升序。
- `Result` / `WinnerUserId` / `EndReason` 非 null(Finished 保证)—— `WinnerUserId` 仅在平局时 null。

### D3 — `UserGameSummaryDto` 比 Replay 更精简

```csharp
public sealed record UserGameSummaryDto(
    Guid RoomId,
    string Name,
    UserSummaryDto Black,
    UserSummaryDto White,
    DateTime StartedAt,
    DateTime EndedAt,
    GameResult Result,
    Guid? WinnerUserId,
    GameEndReason EndReason,
    int MoveCount);
```

列表里**不含 Host**(黑方就是 Host,多余)、**不含 Moves**(列表卡片不要显示棋谱;点进去再拉 `/replay`)。`MoveCount` 是 `game.Moves.Count`。

### D4 — `PagedResult<T>` 通用 wrapper

```csharp
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);
```

- 将来 `leaderboard` 分页也可复用。
- 客户端用 `ceil(Total / PageSize)` 计算总页数,不需要额外字段。

### D5 — 分页约束

`GetUserGamesPagedQueryValidator`:
- `Page >= 1`
- `PageSize` 在 `[1, 100]`;默认 20。

Repository 层 `Skip((page - 1) * pageSize).Take(pageSize)`;total 独立 `CountAsync`。

**考虑过但弃用**:把 count 和 page 合并成一条 SQL(`LIMIT ? OFFSET ? OVER (COUNT)`)—— EF 翻译不稳定,两条查询更清晰,且性能差异在战绩量级下可忽略。

### D6 — 权限:任何登录用户可看任何用户的战绩和回放

- `GET /api/rooms/{id}/replay`:`[Authorize]`,不检查调用方与房间关系。gomoku 的对局记录是"公开的"(类似 GitHub 公开仓库的提交历史)。
- `GET /api/users/{id}/games`:同样 `[Authorize]`,不检查 `id == 自己`。

**考虑过但弃用**:只允许用户看自己的战绩 —— 和用户社交 / 对战前"看对手实力"的常见需求冲突。未来若需要隐私控制,加"是否公开对局"字段,归 `add-game-privacy`。

### D7 — `GameNotFinishedException` 是应用层异常(不是 domain)

Playing / Waiting 房间调 `/replay` 是一个**应用级**错误(Domain 没有"回放"的概念),放在 `Gomoku.Application/Common/Exceptions/` 而非 `Gomoku.Domain/Exceptions/`。HTTP 映射 409。

### D8 — Controllers 的归属

- `/api/rooms/{id}/replay` 放 `RoomsController`(RESTful:replay 是 room 的 sub-resource)。
- `/api/users/{id}/games` 放 `UsersController`(同理:games 是 user 的 sub-resource)。

**不新建** `GamesController` / `ReplaysController`—— 和 REST 惯例冲突,且增加维护面。

### D9 — Repo 的 Include 策略

`GetUserFinishedGamesPagedAsync` 需要 `Include(r => r.Game!).ThenInclude(g => g.Moves)` 以便计算 `MoveCount`。其它字段(Host / Black / White 仅 UserId)不需要 navigation。

**分页 + Include** 有 "cartesian explosion" 风险(多 collection navigation);本次只 1 个 collection(Moves),且典型 < 50 条 per game,不触发;无需 `AsSplitQuery`。

### D10 — Moves 排序

`Room.FindByIdAsync` 的 Include 已在 `add-rooms-and-gameplay` 里装好,但没强制按 Ply 排序 —— `Game.Moves` 是 EF navigation,默认顺序是 DB 返回顺序。`GameReplayDto.Moves` MUST 由 handler 调 `OrderBy(m => m.Ply)`,这是语义要求。

**不做**:在 EF config 里加 `HasIndex` / `UsePlySort` —— 应用层 sort 即可。

## Risks / Trade-offs

| 风险 | 影响 | 缓解 |
|---|---|---|
| 同一用户战绩 1000+ 局时分页查询 `CountAsync` + `Skip` 在 SQLite 慢 | 仅战绩列表加载 ~100ms | 量级上来后改 cursor-based;建 `(BlackPlayerId, EndedAt)` / `(WhitePlayerId, EndedAt)` 索引(由 EF 自动;若不够再手工加) |
| `GameReplayDto` 对 225 步满盘对局 payload 大 | ~20KB JSON,一次 GET | 可接受;浏览器 gzip 自动压到 3-4KB |
| EF 多 collection Include 的 cartesian 风险 | 理论上有 | 本次只 1 个 collection(Moves),不触发 |
| 分页参数 race(用户在两页之间看到同一条 / 漏一条)| 战绩的增速慢(每场游戏一条),用户体验无感 | offset-page 固有问题,量级小时忽略;后续 cursor |
| 非法 / 越权的 UserId 访问别人战绩 | 设计上允许 | D6 明示;若要隐私化,`add-game-privacy` |

## Migration Plan

无 DB migration。

## Open Questions

无。所有默认值(`page=1` / `pageSize=20` / 允许 PageSize 最大 100 / 按 EndedAt DESC / 公开可见)都已在 design.md 固化。
