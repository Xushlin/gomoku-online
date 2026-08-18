# room-and-gameplay Specification Delta

## RENAMED Requirements

标题本身含平台旧名。应用顺序 RENAMED → REMOVED → MODIFIED → ADDED,所以下面 MODIFIED 用的是新标题。

- FROM: ### Requirement: SignalR Hub `GomokuHub` 路由实时操作,但不写入业务逻辑
- TO: ### Requirement: SignalR Hub `MatchHub` 路由实时操作,但不写入业务逻辑


## MODIFIED Requirements

### Requirement: 并发落子由 EF 乐观并发保护

`Game` 实体 MUST 配 `RowVersion` 列并在 EF 配置中 `.IsRowVersion()`。当两个 `MakeMoveCommand` handler 对同一 `Game` 并发 `SaveChangesAsync`,一者 MUST 得到 `DbUpdateConcurrencyException`;Api 层异常中间件 MUST 将其映射为 HTTP 409 + `ProblemDetails`,`type = "https://gewu/errors/concurrent-move"`。

#### Scenario: 并发争抢
- **WHEN** 两个请求携带相同的 `RoomId` 和不同的 `Position`,几乎同时到达
- **THEN** 一者成功(HTTP 200 + 新 Move 持久),另一者收到 HTTP 409,客户端应重新拉取 `RoomState` 再决定是否重试

### Requirement: SignalR Hub `MatchHub` 路由实时操作,但不写入业务逻辑

系统 SHALL 在 `/hubs/match` 暴露 `MatchHub`(`[Authorize]`)。Hub 客户端方法:

- `JoinRoom(Guid roomId)` —— 把当前 connection 加入 SignalR group `room:{roomId}`,并按**聚合里的身份**额外加入恰好一个子群:围观者进 `room:{roomId}:spectators`,其余(玩家,以及进了房但还没围观的连接)进 `room:{roomId}:non-spectators`。身份由 `GetRoomRoleQuery` 从聚合解析,MUST NOT 采信客户端自报。不会修改 `Room` 聚合。

  两个子群 MUST **互斥且穷尽**。互斥不成立会让某个连接收到两份 `RoomState`、由到达顺序决定它看到什么;不穷尽会让某个连接一份都收不到。子群名叫「非围观者」而不是「玩家」正是为了穷尽 —— 按「玩家」分会把还没围观的旁观连接漏在缝里。

  **此前这里的实现与本条不符**:`JoinRoom` 只加 `room:{roomId}`,而围观子群靠客户端自己调 `JoinSpectatorGroup`,那个方法**不做任何校验**。于是这局的玩家调一次就进了围观子群,实时收到全部围观频道消息。实测过。
- `LeaveRoom(Guid roomId)` —— 从上述 group 中移除。不会修改聚合。
- `MakeMove(Guid roomId, int row, int col)` —— **落子类**棋种(五子棋 / 一字棋)。派 `MakeMoveCommand`。
- `MovePiece(Guid roomId, int fromRow, int fromCol, int row, int col)` —— **走子类**棋种(中国象棋)。
- `SayWord(Guid roomId, string word)` —— **文本类**棋种(成语接龙)。
- `SendChat(Guid roomId, string content, ChatChannel channel)` —— 派 `SendChatMessageCommand`(规则见 `in-room-chat` spec)。
- `JoinSpectatorGroup(Guid roomId)` —— 幂等地把连接放进围观子群,**身份由服务端查聚合确认**。对非围观者 MUST 静默无操作:「我还不是围观者」不是错误,把它变成异常只会让客户端多一条要处理的分支。它保留的用途是重连,以及 `JoinRoom` 之后才 `POST /spectate` 的顺序。
- `Urge(Guid roomId)` —— 派 `UrgeOpponentCommand`。

三条走子入口 MUST 是**三个方法**,MUST NOT 合并为一个带可选参数的方法。**SignalR 不套用 C# 的可选参数默认值**,参数个数是**双向精确匹配**:

| 调用 | 目标 | 服务端回 |
| --- | --- | --- |
| `SayWord` 1 个参数 | 2 参 | `InvalidDataException: Invocation provides 1 argument(s) but target expects 2.` |
| `SayWord` 3 个参数 | 2 参 | `InvalidDataException: Invocation provides 3 argument(s) but target expects 2.` |
| `MakeMove` 2 个参数 | 3 参 | `InvalidDataException: Invocation provides 2 argument(s) but target expects 3.` |

多一个参数与少一个参数都被拒。所以给既有方法加参数,**两个方向都断**:旧客户端少发一个会被拒,而新客户端也没法先发着等服务端升级。这一条是实测出来的,不是推断的 —— `generalize-match-domain` 由 `AiSmoke` 撞上,本变更用一条真实长轮询连接复测过。

领域合法性一律由 Handler 调 `Room.PlayMove` 决定;Hub 只把参数搬成一个 `MakeMoveCommand`。哪个棋种收哪种载荷由**规则**判(棋盘类规则收到文本会拒,反之亦然),Hub MUST NOT 知道这件事。

Hub 方法 MUST NOT 访问 `DbContext`、MUST NOT 直接发送 SignalR 消息(事件由 `IRoomNotifier` 在 Handler 完成后触发)。

#### Scenario: 未登录连接被拒
- **WHEN** 不带有效 JWT 的客户端尝试连接 `/hubs/match`
- **THEN** 连接被 SignalR 中间件以 401 拒绝

#### Scenario: Hub 方法透传到 Handler
- **WHEN** 客户端调 `MakeMove(roomId, 7, 7)`
- **THEN** `MakeMoveCommand` 被 `ISender.Send` 派发,携带落点 `(7,7)`;Hub 方法本身不读写数据库,不调用 `Clients.*.SendAsync`

#### Scenario: 文本类走子透传
- **WHEN** 客户端调 `SayWord(roomId, "一心一意")`
- **THEN** `MakeMoveCommand` 被派发,`Text == "一心一意"` 且四个坐标为 `null`

#### Scenario: 三个方法各自独立
- **WHEN** 审阅 hub 的走子入口
- **THEN** MUST 存在三个独立方法,且没有任何一个带可选参数

#### Scenario: 参数个数不符一律被拒
- **WHEN** 客户端以 1 个或 3 个参数调 `SayWord`
- **THEN** 两种都被 SignalR 的参数绑定拒掉,不进入 Hub 方法体

### Requirement: SignalR 服务端事件由 `IRoomNotifier` 抽象触发

Application 层 SHALL 定义 `IRoomNotifier` 契约,至少含:

- `RoomStateChangedAsync(RoomId, RoomStateDto)`
- `PlayerJoinedAsync(RoomId, UserSummaryDto)` / `PlayerLeftAsync(RoomId, UserSummaryDto)`
- `SpectatorJoinedAsync(RoomId, UserSummaryDto)` / `SpectatorLeftAsync(RoomId, UserSummaryDto)`
- `MoveMadeAsync(RoomId, MoveDto)`
- `GameEndedAsync(RoomId, GameEndedDto)`
- `ChatMessagePostedAsync(RoomId, ChatChannel, ChatMessageDto)`
- `OpponentUrgedAsync(RoomId, UserId urgedUser, UrgeDto payload)`

Handler MUST 在 `SaveChangesAsync` **之后** 调用 `IRoomNotifier`,且 MUST NOT 在事务内调用(避免"事件发了但事务回滚"的不一致)。Api 层实现 `SignalRRoomNotifier : IRoomNotifier`,用 `IHubContext<MatchHub>` 把事件发到对应 SignalR group。

#### Scenario: 落子成功后的事件顺序
- **WHEN** `MakeMoveCommand` 成功持久化
- **THEN** Handler 按顺序调 `RoomStateChangedAsync`,然后 `MoveMadeAsync`;若对局结束,再调 `GameEndedAsync`

#### Scenario: 事务失败时不发事件
- **WHEN** `SaveChangesAsync` 抛 `DbUpdateConcurrencyException`
- **THEN** Handler MUST NOT 调 `IRoomNotifier` 的任何方法

### Requirement: JWT Bearer 在 SignalR 连接中从 query string 取 token

Api 层 SHALL 配置 `AddJwtBearer.Events.OnMessageReceived`,若请求路径以 `/hubs` 开头,则从 query 参数 `access_token` 读取 JWT 赋给 `ctx.Token`;其他路径保持默认(Authorization 头)。

#### Scenario: WebSocket 握手鉴权
- **WHEN** 客户端以 `GET /hubs/match?access_token=<jwt>` 发起握手
- **THEN** JWT 被正确识别,`HubCallerContext.UserIdentifier == jwt.sub`;未带或 token 非法时连接被拒(401)

#### Scenario: 非 Hub 路径不受影响
- **WHEN** 请求 `GET /api/users/me` 并把 `access_token` 放在 query string(而非 Authorization 头)
- **THEN** JWT Bearer **不**从 query 取,保持原有行为(通常返回 401,除非另有机制)

### Requirement: 相关领域异常与其 HTTP 映射

系统 SHALL 把 `DbUpdateConcurrencyException`(来自 EF)映射为 HTTP 409 + `ProblemDetails`(`type: "https://gewu/errors/concurrent-move"`)。本次修订 MUST 把该映射的覆盖面从原先"仅 Room/Game 并发"扩展到"Room/Game **与** User 聚合并发冲突";两种情况下 EF 抛出同一异常类型,Api 中间件 MUST NOT 为二者引入不同的 `ProblemDetails.type`。

- 既有(`add-rooms-and-gameplay` 引入):Room / Game 并发冲突(由 `Game.RowVersion` 保护)。
- **新增**(`add-concurrency-hardening`):User 聚合 `RecordGameResult` 写入冲突(由 `User.RowVersion` 保护)。

本次 MUST NOT 新增其它异常与 HTTP 映射条目(所有其它既有条目 `RoomNotFoundException` / `RoomNotWaitingException` / ... / `TurnNotTimedOutException` 保持不变)。

| 异常 | HTTP |
|---|---|
| `DbUpdateConcurrencyException`(来自 EF,覆盖 Game 并发 **与** User 并发) | 409 + `type: "concurrent-move"` |

#### Scenario: 并发落子冲突(覆盖既有)
- **WHEN** 两个玩家几乎同时调 `MakeMove`,EF 在 `SaveChangesAsync` 抛 `DbUpdateConcurrencyException`(Game.RowVersion 冲突)
- **THEN** HTTP 409,`ProblemDetails.type == "https://gewu/errors/concurrent-move"`

#### Scenario: 并发战绩更新冲突(本次新增)
- **WHEN** 两个对局结束事务并发更新同一 User 的战绩(Alice 同时是两盘的黑方,两盘都触发 ResignCommand / TurnTimeoutCommand 几乎同刻完成)
- **THEN** 一者成功(第一次 RecordGameResult 的结果持久);另一者 EF 抛 `DbUpdateConcurrencyException`;Api 返回 HTTP 409,客户端重拉 `GET /api/users/me` + 相关 `GET /api/rooms/{id}` 再决定重试

### Requirement: `TurnTimeoutCommand` 是 worker 内部命令

Application 层 SHALL 新增:

```
public sealed record TurnTimeoutCommand(RoomId RoomId) : IRequest<Unit>;
```

Handler 流程:
1. Load room(null → `RoomNotFoundException`)
2. `var outcome = room.TimeOutCurrentTurn(_clock.UtcNow, _opts.Value.TurnTimeoutSeconds)`
3. `await GameEloApplier.ApplyAsync(room, outcome.Result, _users, ct)`
4. `await _uow.SaveChangesAsync(ct)`
5. Notifier 顺序:`RoomStateChangedAsync` → `GameEndedAsync`
6. 返回 `Unit.Value`

此命令 **不**暴露 REST 端点、**不**路由 SignalR Hub;仅 `TurnTimeoutWorker` 通过 `ISender.Send` 发送。

#### Scenario: 命令不可经 HTTP 触发
- **WHEN** 审阅 `RoomsController` / `MatchHub`
- **THEN** 无任何 action / method 构造或分发 `TurnTimeoutCommand`

#### Scenario: Worker 成功触发
- **WHEN** `TurnTimeoutWorker` 发 `TurnTimeoutCommand(roomId)`,handler 执行
- **THEN** Room.Status 转为 Finished;ELO 被应用;SignalR `GameEnded { EndReason: TurnTimeout }` 被广播

#### Scenario: 竞态:worker 晚到一步
- **WHEN** Worker 的 `GetRoomsWithExpiredTurnsAsync` 说"超时了",但到 handler 执行时对手刚落了一子
- **THEN** `Room.TimeOutCurrentTurn` 抛 `TurnNotTimedOutException`;worker 的 try/catch 吞下并记日志,**不**广播事件,Room 保持 Playing
