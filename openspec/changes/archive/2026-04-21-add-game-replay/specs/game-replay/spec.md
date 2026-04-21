## ADDED Requirements

### Requirement: `GET /api/rooms/{id}/replay` 返回 Finished 房间的完整对局回放

Api 层 SHALL 暴露 `GET /api/rooms/{id}/replay`(`[Authorize]`)。成功响应 HTTP 200 + `GameReplayDto`。

`GameReplayDto` 必含字段:
- `RoomId: Guid`、`Name: string`
- `Host: UserSummaryDto`、`Black: UserSummaryDto`、`White: UserSummaryDto`(Finished 房间保证 White 非 null)
- `StartedAt: DateTime`、`EndedAt: DateTime`
- `Result: GameResult`(非 null —— Finished 保证)
- `WinnerUserId: Guid?`(平局时 null,否则非 null)
- `EndReason: GameEndReason`(非 null —— 由 `add-timeout-resign` 约束保证)
- `Moves: IReadOnlyList<MoveDto>`,**按 `Ply` 升序**

错误映射:
- Room 不存在 → HTTP 404(`RoomNotFoundException`)
- Room 在 Waiting / Playing → HTTP 409(`GameNotFinishedException`,"Replay is only available for finished games.")
- 未登录 → HTTP 401(JWT 中间件)

任何登录用户 MAY 请求任意房间的 replay(无需是该房间的参与者),因为 gomoku 对局记录是公开的。

#### Scenario: 成功获取回放
- **WHEN** Alice 登录,`GET /api/rooms/{fin-id}/replay` 目标房间 Status=Finished
- **THEN** HTTP 200,Body 含完整 `GameReplayDto`;`Moves` 按 Ply 升序;`Black.Id == Host.Id`(创建者默认黑方);`EndReason` 非 null

#### Scenario: 非登录用户
- **WHEN** 无 Bearer token 请求 replay
- **THEN** HTTP 401

#### Scenario: Room 不存在
- **WHEN** 请求不存在的 RoomId 的 replay
- **THEN** HTTP 404 `RoomNotFoundException`

#### Scenario: 房间未结束
- **WHEN** 目标房间 Status = Playing 或 Waiting
- **THEN** HTTP 409 `GameNotFinishedException`

#### Scenario: 任意登录用户可查看他人的对局回放
- **WHEN** 用户 Carol(与该房间无关联)`GET /api/rooms/{fin-id}/replay`
- **THEN** HTTP 200 + 完整 Replay DTO(gomoku 对局公开)

---

### Requirement: `GET /api/users/{id}/games?page=N&pageSize=M` 返回用户战绩分页

Api 层 SHALL 暴露 `GET /api/users/{id}/games`(`[Authorize]`),接受 query `page`(默认 1)和 `pageSize`(默认 20)。成功响应 HTTP 200 + `PagedResult<UserGameSummaryDto>`。

`PagedResult<T>` 字段:`Items: IReadOnlyList<T>`、`Total: int`、`Page: int`、`PageSize: int`。

`UserGameSummaryDto` 字段:
- `RoomId: Guid`、`Name: string`
- `Black: UserSummaryDto`、`White: UserSummaryDto`
- `StartedAt: DateTime`、`EndedAt: DateTime`
- `Result: GameResult`、`WinnerUserId: Guid?`、`EndReason: GameEndReason`
- `MoveCount: int`(= `game.Moves.Count`)

**不含** Host(冗余,= Black)、**不含** Moves(列表视图太重;点进去再拉 `/replay`)。

排序:按 `Game.EndedAt DESC`(最近一局在前)。

Validator 规则(`GetUserGamesPagedQueryValidator`):
- `Page >= 1`,否则 HTTP 400。
- `PageSize` ∈ [1, 100],否则 HTTP 400。

用户维度范围:仅返回 `Status == Finished` 且 `BlackPlayerId == userId OR WhitePlayerId == userId` 的房间。

任何登录用户 MAY 查看他人战绩(无需 `id == 调用方`),同 Replay 公开原则。

#### Scenario: 成功分页
- **WHEN** Alice 参与过 5 局 Finished,`GET /api/users/{alice}/games?page=1&pageSize=2`
- **THEN** HTTP 200;`Items.Count == 2`;`Total == 5`;`Page == 1`;`PageSize == 2`;`Items` 按 `EndedAt DESC`

#### Scenario: 页码超出范围
- **WHEN** Alice 有 5 局,`page=4&pageSize=2`(需要 skip 6 条)
- **THEN** HTTP 200;`Items == []`;`Total == 5`(依然可算总页数)

#### Scenario: 用户无战绩
- **WHEN** 新注册用户 `GET /api/users/{new}/games`
- **THEN** HTTP 200;`Items == []`;`Total == 0`

#### Scenario: 分页参数非法
- **WHEN** `page=0` 或 `pageSize=0` 或 `pageSize=101`
- **THEN** HTTP 400 `ValidationException`

#### Scenario: 默认参数
- **WHEN** `GET /api/users/{id}/games` 不带 query
- **THEN** HTTP 200,采用 `page=1, pageSize=20`

#### Scenario: 只含 Finished
- **WHEN** 用户参与了 1 个 Waiting(其自己创建的)+ 2 个 Playing(未结束)+ 3 个 Finished 房间
- **THEN** 响应 `Items.Count == 3`,`Total == 3`;Waiting / Playing 不包含

---

### Requirement: `PagedResult<T>` 通用分页容器

Application 层 SHALL 在 `Common/DTOs/PagedResult.cs` 定义:

```
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);
```

可复用 for 其它分页端点(未来 `add-leaderboard-pagination` 等)。

#### Scenario: 类型是 record
- **WHEN** 审阅 `PagedResult<T>`
- **THEN** 是 `sealed record`,所有字段不可变;支持 C# record 的值相等

---

### Requirement: `GameNotFinishedException` 为"对 Playing/Waiting 房间请求回放"专用异常

Application 层 SHALL 在 `Common/Exceptions/GameNotFinishedException.cs` 定义 sealed 异常类(继承 `Exception`,`(string message)` 构造)。

Api 全局异常中间件 MUST 映射:

| 异常 | HTTP |
|---|---|
| `GameNotFinishedException` | 409 |

(与 `RoomNotInPlayException` / `RoomNotWaitingException` / `TurnNotTimedOutException` 等同一 409 分组;不新增独立 `ProblemDetails.type`。)

#### Scenario: 映射生效
- **WHEN** 对 Playing 房间请求 `/replay`
- **THEN** HTTP 409,`ProblemDetails.title` 为 "Conflict.",`ProblemDetails.detail` 含 exception message

---

### Requirement: `IRoomRepository.GetUserFinishedGamesPagedAsync` 分页查询

Application 层 SHALL 在 `IRoomRepository` 上新增:

```
Task<(IReadOnlyList<Room> Rooms, int Total)> GetUserFinishedGamesPagedAsync(
    UserId userId, int page, int pageSize, CancellationToken cancellationToken);
```

实现 MUST:
- 过滤 `Status == Finished`;
- 过滤 `BlackPlayerId == userId OR WhitePlayerId == userId`;
- 按 `Game.EndedAt DESC` 排序;
- 先做一次 `CountAsync` 得 Total;
- `Skip((page - 1) * pageSize).Take(pageSize)` + `Include(r => r.Game!).ThenInclude(g => g.Moves)`;
- 返回 `(rooms, total)` tuple。

**不**物化 `Spectators` / `ChatMessages`(战绩列表不需要)。

签名不暴露 EF 类型。

#### Scenario: 正确过滤
- **WHEN** 数据库有:1 个 Alice 的 Waiting 房 + 2 个 Alice 的 Playing 房 + 3 个 Alice 的 Finished 房 + 其他用户房
- **THEN** `GetUserFinishedGamesPagedAsync(alice, 1, 10)` 返回 Total=3,Rooms=3 条(仅 Finished 且 Alice 参与)

#### Scenario: 排序降序
- **WHEN** Alice 的 3 个 Finished 房分别 EndedAt 为 `T1 < T2 < T3`
- **THEN** Rooms 顺序 `[T3, T2, T1]`(最近一局在前)

#### Scenario: 分页跳过
- **WHEN** Alice 有 5 个 Finished,`page=2, pageSize=2`
- **THEN** Rooms 含第 3、4 条(按 EndedAt DESC),Total=5

---

### Requirement: `GameReplayDto.Moves` 与 `UserGameSummaryDto.MoveCount` 正确反映对局

系统 SHALL 保证 replay / user-games 两个端点返回的 DTO 对 moves 历史无遗漏且有序:

- `GameReplayDto.Moves` 的元素 MUST **按 `Ply` 升序**,不跳过。认输 / 超时的对局可能 `Moves == []`(Black / White 还没落子就结束),此时 DTO 的 `Moves` 是空列表而**非 null**。
- `UserGameSummaryDto.MoveCount` MUST 等于 `game.Moves.Count`(无二次过滤;不做"有效 move 判定")。

#### Scenario: 认输后的回放 Moves 为空
- **WHEN** Alice 开房 → Bob join → Alice 未落子直接认输 → `GET /replay`
- **THEN** `GameReplayDto.Moves == []`;`Result == WhiteWin`;`EndReason == Resigned`

#### Scenario: 落子后结束
- **WHEN** Alice 创建房 → Bob join → Alice 落 (7,7) 一子后 Bob 认输
- **THEN** `GameReplayDto.Moves.Count == 1`;`Result == BlackWin`;`EndReason == Resigned`

#### Scenario: 连五结束的 MoveCount
- **WHEN** Alice 连五结束,总共 9 步落子
- **THEN** `GameReplayDto.Moves.Count == 9`;对应 `UserGameSummaryDto.MoveCount == 9`

---

### Requirement: Replay 与 UserGames 端点的权限语义

两个端点 MUST 要求 `[Authorize]`(登录才能访问),但 MUST NOT 再做"访问者 == 参与者"的检查 —— gomoku 对局记录公开,与 GitHub 公开仓库的 commit 历史类比。

若将来引入"私密对局"(例如定向邀请房),SHALL 由独立变更 `add-game-privacy` 给 `Room` 聚合加 `IsPublic` 字段 + handler 做过滤;本变更不覆盖。

#### Scenario: 非参与者查回放 OK
- **WHEN** Carol(不参与此对局)登录请求 `GET /api/rooms/{alice-bob-fin}/replay`
- **THEN** HTTP 200;Carol 能看到完整棋谱

#### Scenario: 非当事人查战绩 OK
- **WHEN** Alice 登录请求 `GET /api/users/{bob}/games`
- **THEN** HTTP 200;Alice 能看到 Bob 的战绩
