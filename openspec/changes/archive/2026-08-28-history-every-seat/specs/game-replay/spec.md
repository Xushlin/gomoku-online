## MODIFIED Requirements

### Requirement: `GET /api/users/{id}/games?page=N&pageSize=M` 返回用户战绩分页

Api 层 SHALL 暴露 `GET /api/users/{id}/games`(`[Authorize]`),接受 query `page`(默认 1)和 `pageSize`(默认 20)。成功响应 HTTP 200 + `PagedResult<UserGameSummaryDto>`。

`PagedResult<T>` 字段:`Items: IReadOnlyList<T>`、`Total: int`、`Page: int`、`PageSize: int`。

`UserGameSummaryDto` 字段:
- `RoomId: Guid`、`Name: string`
- `Seats: IReadOnlyList<RoomSeatDto>`,**按 `Index` 升序,每个在座的座位一条**;
  与 `GameReplayDto.Seats` / `RoomStateDto.Seats` 是同一个 `RoomSeatDto`
- `StartedAt: DateTime`、`EndedAt: DateTime`
- `Result: GameResult`、`WinnerUserId: Guid?`、`EndReason: GameEndReason`
- `MoveCount: int`(= `game.Moves.Count`)

**不含** Host(冗余,= Black)、**不含** Moves(列表视图太重;点进去再拉 `/replay`)。

排序:按 `Game.EndedAt DESC`(最近一局在前)。

Validator 规则(`GetUserGamesPagedQueryValidator`):
- `Page >= 1`,否则 HTTP 400。
- `PageSize` ∈ [1, 100],否则 HTTP 400。

用户维度范围:仅返回 `Status == Finished` 且 **`userId` 坐在任一座位上**的房间(见
`GetUserFinishedGamesPagedAsync` 那条要求里的同一处漂移说明)。三座位棋种因此**会**出现在
战绩列表里,而列表行点进去就是 `/replay/{id}` —— 这正是三座位回放的可达路径。

`UserGameSummaryDto` MUST NOT 有 `Black` / `White` 字段 —— 与 `GameReplayDto` 同一个理由,
也是同一次实测:那两个字段是 0 号与 1 号座位的派生读法,于是三座位棋种的战绩里
**2 号座位上的人不出现**。

`replay-every-seat` 曾把这一条列为「不做」,拆除条件写的是「有人要在战绩列表里正确显示
一局三人牌局的结果时」。**那个条件到了**,所以这里连同它一起还清。

**而「谁赢了」这一半 MUST NOT 假装能说清。** `WinnerUserId` 只装得下一个座位,而斗地主
两名农民是**一起**赢的 —— 领域层写明了这个取舍,并把出路留给客户端:「客户端从叫分历史里
知道谁是地主,自己能说出『农民赢了』」。**那条出路在这个 DTO 上不成立** —— 它刻意不含
`Moves`(「列表视图太重」),所以没走出去的那个农民,自己赢的一局会被算成负。

因此本要求只约束契约能承担的部分:`Seats` 说得出每一个人,而**每个座位各自的胜负由消费方
按「说得出 / 说不出」两支渲染**,见 `web-user-profile`。让服务端算出每人胜负是另一笔账 ——
它要的是棋种自己的阵营概念(`DoudizhuScoring.Settle`),而那份至今没有生产调用方,
**拆除条件是平台需要一条点数阶梯**。

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

#### Scenario: 三座位对局的战绩条目三个座位一个不少
- **WHEN** 请求一个参与过三座位对局的用户的战绩
- **THEN** 那一条的 `Seats.Count == 3`(**恰好三条**);2 号座位上的人在响应里

#### Scenario: 两座位对局仍然是两个座位
- **WHEN** 同一份响应里含一局五子棋
- **THEN** 那一条的 `Seats.Count == 2`。**这一条与上一条 MUST 同时存在** ——
  「每个座位都在」在一个只有两座位样本的集合上恒真

