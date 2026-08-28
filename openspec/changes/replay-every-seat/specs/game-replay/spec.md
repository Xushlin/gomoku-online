## MODIFIED Requirements

### Requirement: `GET /api/rooms/{id}/replay` 返回 Finished 房间的完整对局回放

Api 层 SHALL 暴露 `GET /api/rooms/{id}/replay`(`[Authorize]`)。成功响应 HTTP 200 + `GameReplayDto`。

`GameReplayDto` 必含字段:
- `RoomId: Guid`、`Name: string`
- `Host: UserSummaryDto`
- `Seats: IReadOnlyList<RoomSeatDto>`,**按 `Index` 升序,每个在座的座位一条**;元素是与 `RoomStateDto.Seats`
  **同一个** `RoomSeatDto`(`Index: int` + `Player: UserSummaryDto`)
- `StartedAt: DateTime`、`EndedAt: DateTime`
- `Result: GameResult`(非 null —— Finished 保证)
- `WinnerUserId: Guid?`(平局时 null,否则非 null)
- `EndReason: GameEndReason`(非 null —— 由 `add-timeout-resign` 约束保证)
- `Moves: IReadOnlyList<MoveDto>`,**按 `Ply` 升序**

错误映射:
- Room 不存在 → HTTP 404(`RoomNotFoundException`)
- Room 在 Waiting / Playing → HTTP 409(`GameNotFinishedException`,"Replay is only available for finished games.")
- 未登录 → HTTP 401(JWT 中间件)

`GameReplayDto` MUST NOT 有 `Black` / `White` 字段。

**它们此前有,而对三座位棋种它们是错的 —— 实测过。** 那两个字段无条件读 `room.BlackPlayerId` /
`room.WhitePlayerId`,也就是 0 号与 1 号座位的派生读法,于是一局已结束的斗地主经此端点出来时:
2 号座位上的人**在任何字段里都不出现**,而另外两人被叫作黑方 / 白方。`Room` 自己的文档写着
「牌类棋种 MUST NOT 用这两个名字」,handler 照用不误。

**载荷因此是自相矛盾的,而这是比「少一个人」更硬的判据:** 同一份 DTO 的 `Moves[].Seat` 里
有 `0 / 1 / 2` 三个座位号,而玩家字段只能解析出其中两个 —— 59 手里有 20 手的出手人是**这份载荷
自己说不出是谁**的。所以这不是文案问题,换个标签解决不了。

`Seats` 与 `Moves[].Seat` MUST 是**同一套座位号**:每个 `Move.Seat` MUST 能在 `Seats` 里找到
恰好一条 `Index` 相同的记录。

删而不是留,理由是**留下来的那份会继续说谎**:改完之后这两个字段在整个仓库里
**零个读者**(唯一的消费方是回放页,而它这次改成读 `Seats`)—— `RoomStateDto` 留着它们是因为
那里有真读者,这里没有。一个没人读、又对三分之一棋种为假的字段,是下一个人照抄的模板。

任何登录用户 MAY 请求任意房间的 replay(无需是该房间的参与者),因为 gomoku 对局记录是公开的。

#### Scenario: 成功获取回放
- **WHEN** Alice 登录,`GET /api/rooms/{fin-id}/replay` 目标房间 Status=Finished
- **THEN** HTTP 200,Body 含完整 `GameReplayDto`;`Moves` 按 Ply 升序;`Seats[0].Player.Id == Host.Id`(创建者坐 0 号座);`EndReason` 非 null

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
#### Scenario: 三座位棋种的回放三个座位一个不少
- **WHEN** 请求一局已结束的斗地主(三个座位坐满)的回放
- **THEN** `Seats.Count == 3`(**恰好三条,不是「至少两条」**);三个 `Index` 是 `0 / 1 / 2`;
  2 号座位上那个人的 `Id` 与 `Username` 都在响应里

#### Scenario: 两座位棋种仍然是两个座位
- **WHEN** 请求一局已结束的五子棋对局的回放
- **THEN** `Seats.Count == 2`;`Index` 是 `0 / 1`。**这一条与上一条 MUST 同时存在** ——
  「每个座位都在」在一个只有两座位样本的集合上恒真

#### Scenario: 每一手的出手人都解析得出来
- **WHEN** 取任意一局回放,遍历 `Moves`
- **THEN** 每个 `Move.Seat` 在 `Seats` 里**恰好**匹配一条 `Index`;三座位样本里 `Moves` 用到的
  座位号集合 MUST 含 `2`,否则这条断言在样本上是空的


---

### Requirement: `GameReplayDto` 携带棋种键

`GameReplayDto` SHALL 带一个非空的 `GameKey` 字段,取自 `Room.GameKey`。

理由与房间状态 DTO 完全相同,而且这里更迫切:回放页**自己拼一个 `RoomState` 形状的对象**喂给同一个 `Board` 组件,所以它必须知道盘面几格。回放链接常常是冷启动打开的(分享、收藏、从战绩列表点进),那时客户端手上只有一个房间 id。

没有本字段时唯一能编译过的写法是在回放页里写死 `'gomoku'` —— 那会让一字棋的回放画成 15×15。而「刚下完一局 → 点查看回放」是主路径,不是边角。

#### Scenario: 一字棋回放带对棋种
- **WHEN** 请求一局 `tictactoe` 对局的回放
- **THEN** `GameKey == "tictactoe"`,回放页据此渲染 9 格

#### Scenario: 五子棋回放不受影响
- **WHEN** 请求一局 `gomoku` 对局的回放
- **THEN** `GameKey == "gomoku"`,回放页渲染 225 格

#### Scenario: `GameKey` 本身不改名不改型
- **WHEN** 比对任何一次后续变更前后的 `GameReplayDto`
- **THEN** `GameKey` 的名称与类型 MUST NOT 改变

本条此前写作「**既有字段**的名称与类型 MUST NOT 改变」,那句话是 `add-web-replay-and-profile`
给**自己**立的规矩(原文是「比对**本变更**前后」),而归档之后它变成了一条对所有人生效的
live 要求 —— 于是「删掉 `Black` / `White`」与它字面冲突。**一条被后来的事实推翻的要求,
要么改要么删,不能一边留着一边违反**:留着它而照改不误,下一个读规格的人会以为代码漂了。
这里收窄成它真正要守的东西 —— `GameKey` 是回放页选棋盘的唯一依据,改它会让每一局画错盘。

---

### Requirement: `IRoomRepository.GetUserFinishedGamesPagedAsync` 分页查询

Application 层 SHALL 在 `IRoomRepository` 上新增:

```
Task<(IReadOnlyList<Room> Rooms, int Total)> GetUserFinishedGamesPagedAsync(
    UserId userId, int page, int pageSize, CancellationToken cancellationToken);
```

实现 MUST:
- 过滤 `Status == Finished`;
- 过滤**任一座位**上的人是 `userId`(`RoomSeats.Any(x => x.RoomId == r.Id && x.UserId == userId)`);
- 按 `Game.EndedAt DESC` 排序;
- 先做一次 `CountAsync` 得 Total;
- `Skip((page - 1) * pageSize).Take(pageSize)` + `Include(r => r.Game!).ThenInclude(g => g.Moves)`;
- 返回 `(rooms, total)` tuple。

**不**物化 `Spectators` / `ChatMessages`(战绩列表不需要)。

签名不暴露 EF 类型。

**座位过滤这一条是在改回放契约时发现的规格漂移,而漂的是规格不是代码:** 实现早就走
`RoomSeats.Any(...)`(所有座位),规格却还写着「黑方或白方」(0 号或 1 号)。两者的差别只在
三座位棋种上看得见 —— 按规格的字面写法,一个坐 2 号座位的人**自己的对局不会出现在自己的战绩里**。
照着规格「修正」代码会造出那个缺陷,所以这里把规格对齐到已发布的行为。

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

用户维度范围:仅返回 `Status == Finished` 且 **`userId` 坐在任一座位上**的房间(见
`GetUserFinishedGamesPagedAsync` 那条要求里的同一处漂移说明)。三座位棋种因此**会**出现在
战绩列表里,而列表行点进去就是 `/replay/{id}` —— 这正是三座位回放的可达路径。

**`UserGameSummaryDto` 的 `Black` / `White` 本变更不动,而理由要写下来。** 它有与回放同一个缺陷
(2 号座位不出现),但修它要连带回答「三个人的一局,列表行上的『对手』是谁」——
那是显示层的取舍,不是契约的对错,混进来会让这个变更同时改两层。**拆除条件:** 有人要在
战绩列表里正确显示一局三人牌局的结果时(那时「谁赢了」也已经不是一个 `WinnerUserId` 说得清的了)。

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
