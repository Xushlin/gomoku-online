# Room and Gameplay

## Purpose

房间生命周期与对局推进的核心能力:`Room` 聚合根(承载玩家、围观者、对局、聊天、催促时间戳)、`Game` 子实体(当前回合、结果、胜方、Moves 历史)、`Move` 子实体(按 Ply 递增)、`RoomSpectator` 联结实体,以及把 `gomoku-domain` 的 `Board` / 判胜接入对局流程的规则。

HTTP 表面:`POST/GET /api/rooms`、`GET /api/rooms/{id}`、`POST /api/rooms/{id}/{join,leave}`、`POST/DELETE /api/rooms/{id}/spectate`。SignalR 表面:`/hubs/gomoku` 的五个客户端方法(`JoinRoom` / `LeaveRoom` / `MakeMove` / `SendChat` / `Urge`)与服务端事件(`RoomState` / `PlayerJoined` / `PlayerLeft` / `SpectatorJoined` / `SpectatorLeft` / `MoveMade` / `GameEnded` / `ChatMessage` / `UrgeReceived`)。

实现位于 `backend/src/Gomoku.Domain/Rooms/`(聚合)、`backend/src/Gomoku.Application/Features/Rooms/`(CQRS handlers)、`backend/src/Gomoku.Infrastructure/Persistence/`(EF 映射与仓储)、`backend/src/Gomoku.Api/Hubs/`(SignalR Hub 与 IRoomNotifier 实现)。

## Requirements


### Requirement: `RoomId` 是 `Guid` 的强类型包装值对象

系统 SHALL 用 `RoomId` 值对象承载房间主键,内部为 `Guid`。`RoomId` MUST 不可变、基于值相等。Domain / Application 层的公共 API 在引用房间标识时 MUST 使用 `RoomId` 而非裸 `Guid`。

#### Scenario: 构造与取值
- **WHEN** 以 `Guid.NewGuid()` 构造 `RoomId`
- **THEN** 其 `Value` 属性等于传入的 `Guid`

#### Scenario: 值相等
- **WHEN** 两个 `RoomId` 包装同一 `Guid`
- **THEN** `==` / `.Equals()` / `.GetHashCode()` 均认定它们相等

---

### Requirement: `Room` 聚合根承载玩家、围观者、对局、状态与元数据

系统 SHALL 定义 `Room` 作为聚合根,字段包含:

- `Id: RoomId`
- `Name: string`(3–50 字符,非空白)
- `HostUserId: UserId`(创建者)
- `BlackPlayerId: UserId?` / `WhitePlayerId: UserId?`
- `Status: RoomStatus`(`Waiting` / `Playing` / `Finished`)
- `CreatedAt: DateTime`(UTC)
- `LastUrgeAt: DateTime?` / `LastUrgeByUserId: UserId?`
- `Game: Game?`(子实体;`Status == Waiting` 时为 `null`,`Playing`/`Finished` 时存在)
- `Spectators: IReadOnlyCollection<UserId>`(只读;内部私有集合)
- `ChatMessages: IReadOnlyCollection<ChatMessage>`(只读)

所有字段外部 MUST NOT 直接修改;变更仅通过领域方法。

#### Scenario: 字段可读
- **WHEN** 访问 `Room` 的任意上述属性
- **THEN** 返回相应类型的当前值

#### Scenario: `Spectators` 与 `ChatMessages` 只读
- **WHEN** 外部把 `Room.Spectators` / `Room.ChatMessages` 强转为可变集合并 `Add`
- **THEN** 该修改 MUST NOT 影响 `Room` 内部状态

---

### Requirement: `Room.Create` 静态工厂构造新房间

系统 SHALL 提供 `Room.Create(RoomId id, string name, UserId hostUserId, DateTime createdAt)`。返回的 `Room` MUST 满足:

- `Id / HostUserId / CreatedAt` 等于入参
- `Name` 经过 trim 后长度在 [3..50];非法名称抛 `InvalidRoomNameException`
- `BlackPlayerId = hostUserId`(创建者默认黑方)
- `WhitePlayerId = null`
- `Status = Waiting`
- `Game = null`
- `LastUrgeAt = null`, `LastUrgeByUserId = null`
- `Spectators` 为空,`ChatMessages` 为空

#### Scenario: 成功创建
- **WHEN** 以合法参数调用 `Room.Create(...)`
- **THEN** 返回 `Room` 实例,字段等于上述初始值

#### Scenario: 名称非法
- **WHEN** `name` 为 `null` / 空 / 全空白 / 短于 3 / 超过 50 字符
- **THEN** 抛 `InvalidRoomNameException`,消息明确违反规则

---

### Requirement: `Room.JoinAsPlayer` 让第二位玩家加入并启动对局

系统 SHALL 提供 `Room.JoinAsPlayer(UserId userId, DateTime now)`。调用后:

- 若 `Status != Waiting`:MUST 抛 `RoomNotWaitingException`
- 若 `userId == HostUserId`(即 `BlackPlayerId`):MUST 抛 `AlreadyInRoomException`
- 若 `userId ∈ Spectators`:MUST 先从围观者集合移除,再入座白方
- 若 `WhitePlayerId != null`:MUST 抛 `RoomFullException`
- 否则:`WhitePlayerId = userId`、`Status = Playing`、`Game = new Game(currentTurn: Black, startedAt: now)`

#### Scenario: 第二位玩家成功加入
- **WHEN** 房间处于 `Waiting`,调用 `JoinAsPlayer(bobId, now)`,白方为空
- **THEN** `WhitePlayerId == bobId`;`Status == Playing`;`Game != null` 且 `Game.CurrentTurn == Black`;`Game.StartedAt == now`

#### Scenario: 非等待状态
- **WHEN** `Status` 为 `Playing` 或 `Finished`,调用 `JoinAsPlayer`
- **THEN** 抛 `RoomNotWaitingException`

#### Scenario: 创建者重复加入
- **WHEN** 创建者以自己的 `UserId` 调 `JoinAsPlayer`
- **THEN** 抛 `AlreadyInRoomException`

#### Scenario: 围观者升级为玩家
- **WHEN** 用户先进入围观者集合,随后调 `JoinAsPlayer`
- **THEN** 该用户从 `Spectators` 移除,作为 `WhitePlayerId` 入座

#### Scenario: 房间已满
- **WHEN** `BlackPlayerId` 和 `WhitePlayerId` 都已存在,再有第三人 `JoinAsPlayer`
- **THEN** 抛 `RoomFullException`

---

### Requirement: `Room.Leave` 让玩家 / 围观者离开房间

系统 SHALL 提供 `Room.Leave(UserId userId, DateTime now)`。规则:

- 若 `userId` 不在该房间(既非玩家、也非围观者):MUST 抛 `NotInRoomException`
- 若 `userId` 是围观者:从 `Spectators` 移除
- 若 `userId` 是玩家且 `Status == Waiting`(只有创建者这一种情况):`Status` 保持 `Waiting`,`HostUserId`/`BlackPlayerId` 被清空?—— **本次不允许创建者单方面"不解散就离开"**:`Status == Waiting` 时创建者离开 MUST 抛 `HostCannotLeaveWaitingRoomException`(提示"请解散房间")。**解散房间**的 API 是独立的(`DELETE /api/rooms/{id}`,仅 Host 可调;本 spec 不在此方法内覆盖)。
- 若 `userId` 是玩家且 `Status == Playing`:该玩家视为"离席",`Status` 保持 `Playing`,`Game` 不变,棋局对手仍可落子;本次**不**自动判负(见 design Non-Goals)。
- 若 `Status == Finished`:玩家 / 围观者均可自由离开。

#### Scenario: 围观者离开
- **WHEN** 围观者 `C` 调 `Room.Leave(c, now)`
- **THEN** `C ∉ Spectators`;其他字段不变

#### Scenario: 对局中的玩家离席
- **WHEN** 玩家 `Alice` 在 `Status == Playing` 时调 `Room.Leave(aliceId, now)`
- **THEN** `Status` 仍为 `Playing`,`Game` 状态不变,`BlackPlayerId` 仍为 `aliceId`(视为"挂起 / 离席",判负逻辑留给后续变更)

#### Scenario: Waiting 状态下 Host 尝试离开
- **WHEN** 创建者在 `Status == Waiting` 时调 `Room.Leave(hostId, now)`
- **THEN** 抛 `HostCannotLeaveWaitingRoomException`

#### Scenario: 非成员离开
- **WHEN** 不在房间的用户调 `Room.Leave`
- **THEN** 抛 `NotInRoomException`

---

### Requirement: `Room.JoinAsSpectator` / `LeaveAsSpectator` 管理围观者集合

系统 SHALL 提供这两个方法:

- `JoinAsSpectator(UserId userId)`:
  - 若 `userId` 是当前玩家(`BlackPlayerId` / `WhitePlayerId`)→ MUST 抛 `PlayerCannotSpectateException`
  - 若 `userId ∈ Spectators` → 幂等成功(no-op)
  - 否则加入 `Spectators`
- `LeaveAsSpectator(UserId userId)`:
  - 若 `userId ∉ Spectators` → MUST 抛 `NotSpectatingException`
  - 否则移除

两者对 `Room.Status` 无限制(`Waiting` / `Playing` / `Finished` 均可围观)。

#### Scenario: 普通用户成为围观者
- **WHEN** 非玩家用户 `C` 调 `JoinAsSpectator(c)`
- **THEN** `C ∈ Spectators`

#### Scenario: 玩家尝试围观
- **WHEN** `BlackPlayerId` 本人调 `JoinAsSpectator`
- **THEN** 抛 `PlayerCannotSpectateException`

#### Scenario: 重复围观幂等
- **WHEN** 已在围观者集合的用户再次调 `JoinAsSpectator`
- **THEN** 不抛异常,`Spectators` 不出现重复项

---

### Requirement: `Room.PlayMove` 以原子事务落子、判胜并推进状态

系统 SHALL 提供 `Room.PlayMove(UserId userId, Position position, DateTime now)`,按顺序:

1. `Status != Playing` → MUST 抛 `RoomNotInPlayException`
2. `userId != BlackPlayerId && userId != WhitePlayerId` → MUST 抛 `NotAPlayerException`
3. 根据 `userId` 推断棋色 `Stone`,若不等于 `Game.CurrentTurn` → MUST 抛 `NotYourTurnException`
4. 调 `Board.PlaceStone(new Move(position, stone))`(gomoku-domain 负责越界 / 重复落子判定)
5. Append 一条 `Move` 子实体:`Ply = 上一 Ply + 1`、`Position`、`Stone`、`PlayedAt = now`
6. `Game.CurrentTurn = oppositeColor(stone)`
7. 若 Board 返回的 `GameResult != Ongoing`:
   - `Game.EndedAt = now`,`Game.Result = result`,`Game.WinnerUserId = BlackWin ? BlackPlayerId : (WhiteWin ? WhitePlayerId : null)`
   - `Room.Status = Finished`
8. 返回的 `MoveOutcome` MUST 包含新 `Move` 实体引用与 `GameResult`,供调用方决定发哪些事件。

#### Scenario: 合法落子且未连五
- **WHEN** `Playing` 状态,轮到 Alice(黑方),调 `PlayMove(aliceId, (7,7), now)`,棋盘此前为空
- **THEN** 返回 `GameResult.Ongoing`;`Game.Moves` 新增一条 `Ply=1` 的 Move;`Game.CurrentTurn == White`;`Room.Status == Playing`

#### Scenario: 最后一子连五
- **WHEN** 黑方已在 `(7,3)..(7,6)` 连四,调 `PlayMove(aliceId, (7,7), now)`
- **THEN** 返回 `GameResult.BlackWin`;`Game.EndedAt == now`;`Game.WinnerUserId == aliceId`;`Room.Status == Finished`

#### Scenario: 非玩家尝试落子
- **WHEN** 围观者或非成员调 `PlayMove`
- **THEN** 抛 `NotAPlayerException`

#### Scenario: 不是你的回合
- **WHEN** 白方在 `CurrentTurn == Black` 时调 `PlayMove`
- **THEN** 抛 `NotYourTurnException`

#### Scenario: 非 `Playing` 状态
- **WHEN** `Status == Waiting` 或 `Finished`,调 `PlayMove`
- **THEN** 抛 `RoomNotInPlayException`

#### Scenario: 底层棋盘规则违反
- **WHEN** 正确玩家在正确回合,但 `Position` 越界或该位置已有子
- **THEN** `Board` 抛 `InvalidMoveException`,`Room` MUST 让其原样冒泡;**`Move` 未被 append**,状态保持不变

---

### Requirement: `Game` 子实体承载对局运行状态

`Game` MUST 包含字段:
- `Id: Guid`
- `RoomId: RoomId`
- `StartedAt: DateTime`(UTC)
- `EndedAt: DateTime?`
- `Result: GameResult?`(对局进行时为 `null`)
- `WinnerUserId: UserId?`
- `CurrentTurn: Stone`
- `Moves: IReadOnlyCollection<Move>`
- `RowVersion: byte[]`(乐观并发令牌,由 Infrastructure 层维护)

`Game` 不独立于 `Room` 存活;构造仅由 `Room.JoinAsPlayer` 内部发生。

#### Scenario: 初始 Game 状态
- **WHEN** 白方加入触发 `JoinAsPlayer`
- **THEN** `Game.StartedAt == now`;`CurrentTurn == Black`;`Moves` 空;`EndedAt == null`;`Result == null`

#### Scenario: Game 结束状态
- **WHEN** 某方连五或平局后
- **THEN** `EndedAt != null`;`Result != null`;若有胜方则 `WinnerUserId != null`,平局则 `WinnerUserId == null`

---

### Requirement: `Move` 子实体记录每一步的上下文

`Move` MUST 包含:`Id: Guid`、`GameId: Guid`、`Ply: int (1-based)`、`Row: int`、`Col: int`、`Stone: Stone`、`PlayedAt: DateTime`(UTC)。数据库持久化:`(GameId, Ply)` 唯一。

#### Scenario: Ply 从 1 起严格递增
- **WHEN** 在同一局依次落 3 子
- **THEN** 三个 `Move` 的 `Ply` 分别为 1、2、3

---

### Requirement: 从 `Moves` 在内存 replay 得到当前 `Board`

系统 SHALL 在需要当前棋盘状态时(例如 `Room.PlayMove` 执行前、`GetRoomState` 查询、未来的 AI 搜索),按 `Moves` 的 `Ply` 升序,在内存 `new Board()` 后逐步调 `Board.PlaceStone`。服务端 MUST NOT 在数据库里冗余存储"完整盘面"作为另一真相源。

#### Scenario: 重启后重建盘面
- **WHEN** 服务重启,某房间 `Moves` 已落 10 步,查询 `GetRoomState`
- **THEN** 服务端在内存 replay 10 步得到完全一致的 Board,并在 `RoomStateDto` 中反映所有 10 步

---

### Requirement: 并发落子由 EF 乐观并发保护

`Game` 实体 MUST 配 `RowVersion` 列并在 EF 配置中 `.IsRowVersion()`。当两个 `MakeMoveCommand` handler 对同一 `Game` 并发 `SaveChangesAsync`,一者 MUST 得到 `DbUpdateConcurrencyException`;Api 层异常中间件 MUST 将其映射为 HTTP 409 + `ProblemDetails`,`type = "https://gomoku-online/errors/concurrent-move"`。

#### Scenario: 并发争抢
- **WHEN** 两个请求携带相同的 `RoomId` 和不同的 `Position`,几乎同时到达
- **THEN** 一者成功(HTTP 200 + 新 Move 持久),另一者收到 HTTP 409,客户端应重新拉取 `RoomState` 再决定是否重试

---

### Requirement: `RoomStatus` 状态机仅允许 `Waiting → Playing → Finished`

系统 SHALL 定义 `enum RoomStatus { Waiting=0, Playing=1, Finished=2 }`。非单向递进的转换 MUST 抛 `InvalidRoomStatusTransitionException`。`Room` 的领域方法内部不会违反此约束;若未来有外部赋值入口,该入口也要守住。

#### Scenario: 合法推进
- **WHEN** `JoinAsPlayer` 从 `Waiting` 进 `Playing`;对局结束从 `Playing` 进 `Finished`
- **THEN** 转换成功,无异常

#### Scenario: 非法回退
- **WHEN** 尝试把 `Status` 从 `Playing` 回到 `Waiting`,或从 `Finished` 回到 `Playing`
- **THEN** 抛 `InvalidRoomStatusTransitionException`

---

### Requirement: `IRoomRepository` 契约只暴露领域概念

Application 层 SHALL 定义 `IRoomRepository`,至少包含:

- `Task<Room?> FindByIdAsync(RoomId id, CancellationToken ct)` —— 实现 MUST `Include` `Game`、`Game.Moves`、`Spectators`、`ChatMessages`
- `Task<IReadOnlyList<Room>> GetActiveRoomsAsync(CancellationToken ct)` —— 返回 `Waiting` + `Playing` 状态的房间(不含 `Finished`)
- `Task AddAsync(Room room, CancellationToken ct)`

签名 MUST NOT 出现 `IQueryable`、`Expression`、EF Core 类型。

#### Scenario: 契约纯净性
- **WHEN** 审阅 `IRoomRepository.cs`
- **THEN** 不出现任何 `Microsoft.EntityFrameworkCore.*` 类型

---

### Requirement: REST 端点管理房间聚合(关系 / 状态)

Api 层 SHALL 暴露以下端点(均要求 `Authorize`):

| HTTP | 路径 | Body | 成功 | 描述 |
|---|---|---|---|---|
| POST | `/api/rooms` | `{ name }` | 201 + `RoomSummaryDto` | 创建房间(调用方成为 Host 与黑方) |
| GET | `/api/rooms` | — | 200 + `RoomSummaryDto[]` | 活跃房间列表(Waiting + Playing) |
| GET | `/api/rooms/{id}` | — | 200 + `RoomStateDto` | 完整房间状态(含 Moves) |
| POST | `/api/rooms/{id}/join` | — | 200 + `RoomStateDto` | 以当前用户身份加入为白方 |
| POST | `/api/rooms/{id}/leave` | — | 204 | 离开房间(玩家或围观者) |
| POST | `/api/rooms/{id}/spectate` | — | 204 | 加入围观 |
| DELETE | `/api/rooms/{id}/spectate` | — | 204 | 离开围观 |

**落子、聊天、催促不走 REST**,由 SignalR Hub 路由(见下一个 Requirement)。

#### Scenario: 列表只含活跃房间
- **WHEN** 已有 3 个 `Waiting`、2 个 `Playing`、1 个 `Finished` 房间,调 `GET /api/rooms`
- **THEN** 返回 5 个摘要,不含 `Finished` 房间

#### Scenario: 加入不存在的房间
- **WHEN** `POST /api/rooms/{id}/join` 指向不存在的 id
- **THEN** HTTP 404,错误类型 `RoomNotFoundException`

---

### Requirement: SignalR Hub `GomokuHub` 路由实时操作,但不写入业务逻辑

系统 SHALL 在 `/hubs/gomoku` 暴露 `GomokuHub`(`[Authorize]`)。Hub 客户端方法:

- `JoinRoom(Guid roomId)` —— 把当前 connection 加入 SignalR group `room:{roomId}`;若调用方已是该房间的玩家或围观者(聚合成员已由 REST 建立),则**额外**加入 `room:{roomId}:spectators` 子群(仅围观者)。不会修改 `Room` 聚合。
- `LeaveRoom(Guid roomId)` —— 从上述 group 中移除。不会修改聚合。
- `MakeMove(Guid roomId, int row, int col)` —— 派 `MakeMoveCommand`(领域合法性由 Handler 调 `Room.PlayMove` 决定)。
- `SendChat(Guid roomId, string content, ChatChannel channel)` —— 派 `SendChatMessageCommand`(规则见 `in-room-chat` spec)。
- `Urge(Guid roomId)` —— 派 `UrgeOpponentCommand`。

Hub 方法 MUST NOT 访问 `DbContext`、MUST NOT 直接发送 SignalR 消息(事件由 `IRoomNotifier` 在 Handler 完成后触发)。

#### Scenario: 未登录连接被拒
- **WHEN** 不带有效 JWT 的客户端尝试连接 `/hubs/gomoku`
- **THEN** 连接被 SignalR 中间件以 401 拒绝

#### Scenario: Hub 方法透传到 Handler
- **WHEN** 客户端调 `MakeMove(roomId, 7, 7)`
- **THEN** `MakeMoveCommand(userId, roomId, (7,7))` 被 `ISender.Send` 派发;Hub 方法本身不读写数据库,不调用 `Clients.*.SendAsync`

---

### Requirement: SignalR 服务端事件由 `IRoomNotifier` 抽象触发

Application 层 SHALL 定义 `IRoomNotifier` 契约,至少含:

- `RoomStateChangedAsync(RoomId, RoomStateDto)`
- `PlayerJoinedAsync(RoomId, UserSummaryDto)` / `PlayerLeftAsync(RoomId, UserSummaryDto)`
- `SpectatorJoinedAsync(RoomId, UserSummaryDto)` / `SpectatorLeftAsync(RoomId, UserSummaryDto)`
- `MoveMadeAsync(RoomId, MoveDto)`
- `GameEndedAsync(RoomId, GameEndedDto)`
- `ChatMessagePostedAsync(RoomId, ChatChannel, ChatMessageDto)`
- `OpponentUrgedAsync(RoomId, UserId urgedUser, UrgeDto payload)`

Handler MUST 在 `SaveChangesAsync` **之后** 调用 `IRoomNotifier`,且 MUST NOT 在事务内调用(避免"事件发了但事务回滚"的不一致)。Api 层实现 `SignalRRoomNotifier : IRoomNotifier`,用 `IHubContext<GomokuHub>` 把事件发到对应 SignalR group。

#### Scenario: 落子成功后的事件顺序
- **WHEN** `MakeMoveCommand` 成功持久化
- **THEN** Handler 按顺序调 `RoomStateChangedAsync`,然后 `MoveMadeAsync`;若对局结束,再调 `GameEndedAsync`

#### Scenario: 事务失败时不发事件
- **WHEN** `SaveChangesAsync` 抛 `DbUpdateConcurrencyException`
- **THEN** Handler MUST NOT 调 `IRoomNotifier` 的任何方法

---

### Requirement: JWT Bearer 在 SignalR 连接中从 query string 取 token

Api 层 SHALL 配置 `AddJwtBearer.Events.OnMessageReceived`,若请求路径以 `/hubs` 开头,则从 query 参数 `access_token` 读取 JWT 赋给 `ctx.Token`;其他路径保持默认(Authorization 头)。

#### Scenario: WebSocket 握手鉴权
- **WHEN** 客户端以 `GET /hubs/gomoku?access_token=<jwt>` 发起握手
- **THEN** JWT 被正确识别,`HubCallerContext.UserIdentifier == jwt.sub`;未带或 token 非法时连接被拒(401)

#### Scenario: 非 Hub 路径不受影响
- **WHEN** 请求 `GET /api/users/me` 并把 `access_token` 放在 query string(而非 Authorization 头)
- **THEN** JWT Bearer **不**从 query 取,保持原有行为(通常返回 401,除非另有机制)

---

### Requirement: 相关领域异常与其 HTTP 映射

本次新增的领域异常 MUST 被全局异常中间件按下表映射:

| 异常 | HTTP |
|---|---|
| `InvalidRoomNameException` | 400 |
| `InvalidRoomStatusTransitionException` | 400 |
| `RoomNotFoundException` | 404 |
| `RoomNotWaitingException` | 409 |
| `RoomNotInPlayException` | 409 |
| `RoomFullException` | 409 |
| `AlreadyInRoomException` | 409 |
| `HostCannotLeaveWaitingRoomException` | 409 |
| `PlayerCannotSpectateException` | 409 |
| `NotInRoomException` | 404 |
| `NotSpectatingException` | 404 |
| `NotAPlayerException` | 403 |
| `NotYourTurnException` | 409 |
| `DbUpdateConcurrencyException`(来自 EF) | 409 + `type: "concurrent-move"` |

现有映射(`InvalidMoveException` 等)MUST 保持不变。

#### Scenario: 加入已满房间
- **WHEN** 向已满房间 `POST /api/rooms/{id}/join`
- **THEN** HTTP 409,`ProblemDetails.title` 指示 `RoomFullException`

#### Scenario: 并发落子冲突
- **WHEN** 两个玩家几乎同时调 `MakeMove`,EF 在 `SaveChangesAsync` 抛 `DbUpdateConcurrencyException`
- **THEN** HTTP 409,`ProblemDetails.type == "https://gomoku-online/errors/concurrent-move"`
