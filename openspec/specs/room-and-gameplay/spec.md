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
- 若 `userId` 是玩家且 `Status == Waiting`(只有创建者这一种情况):创建者 MUST 抛 `HostCannotLeaveWaitingRoomException`,提示调用 `DELETE /api/rooms/{id}` 解散房间(**本次修订**:现在该错误消息指向一个**真实存在**的解散端点,不再是死胡同)。
- 若 `userId` 是玩家且 `Status == Playing`:该玩家视为"离席",`Status` 保持 `Playing`,`Game` 不变,棋局对手仍可落子;本次**不**自动判负(见 design Non-Goals;认输 / 超时判负留给 `add-timeout-resign`)。
- 若 `Status == Finished`:玩家 / 围观者均可自由离开。

#### Scenario: 围观者离开
- **WHEN** 围观者 `C` 调 `Room.Leave(c, now)`
- **THEN** `C ∉ Spectators`;其他字段不变

#### Scenario: 对局中的玩家离席
- **WHEN** 玩家 `Alice` 在 `Status == Playing` 时调 `Room.Leave(aliceId, now)`
- **THEN** `Status` 仍为 `Playing`,`Game` 状态不变,`BlackPlayerId` 仍为 `aliceId`(视为"挂起 / 离席",判负逻辑留给后续变更)

#### Scenario: Waiting 状态下 Host 尝试离开
- **WHEN** 创建者在 `Status == Waiting` 时调 `Room.Leave(hostId, now)`
- **THEN** 抛 `HostCannotLeaveWaitingRoomException`,**消息提示"请通过 `DELETE /api/rooms/{id}` 解散房间"**;Host 应用该端点替代 Leave

#### Scenario: 非成员离开
- **WHEN** 不在房间的用户调 `Room.Leave`
- **THEN** 抛 `NotInRoomException`

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
   - `Game.FinishWith(result, winnerUserId, **GameEndReason.Connected5**, now)`(本次修订:显式传入 `Connected5`)
   - `Room.Status = Finished`
8. 返回的 `MoveOutcome` MUST 包含新 `Move` 实体引用与 `GameResult`,供调用方决定发哪些事件。

#### Scenario: 最后一子连五
- **WHEN** 黑方已在 `(7,3)..(7,6)` 连四,调 `PlayMove(aliceId, (7,7), now)`
- **THEN** 返回 `GameResult.BlackWin`;`Game.EndedAt == now`;`Game.WinnerUserId == aliceId`;**`Game.EndReason == Connected5`**;`Room.Status == Finished`

#### Scenario: 合法落子且未连五
- **WHEN** `Playing` 状态,轮到 Alice(黑方),调 `PlayMove(aliceId, (7,7), now)`,棋盘此前为空
- **THEN** 返回 `GameResult.Ongoing`;`Game.Moves` 新增一条 `Ply=1` 的 Move;`Game.CurrentTurn == White`;`Room.Status == Playing`;**`Game.EndReason == null`**(未结束)

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
- **`EndReason: GameEndReason?`**(本次新增;结束时非 null,与 `Result` 同时为 null 或同时非 null)
- `CurrentTurn: Stone`
- `Moves: IReadOnlyCollection<Move>`
- `RowVersion: byte[]`(乐观并发令牌,由 Infrastructure 层维护)

`Game` 不独立于 `Room` 存活;构造仅由 `Room.JoinAsPlayer` 内部发生。`Game.FinishWith` 的签名 MUST 为 `FinishWith(GameResult, UserId?, GameEndReason, DateTime)` —— 新增必填 reason 参数,保证结束路径不漏填。

#### Scenario: 初始 Game 状态
- **WHEN** 白方加入触发 `JoinAsPlayer`
- **THEN** `Game.StartedAt == now`;`CurrentTurn == Black`;`Moves` 空;`EndedAt == null`;`Result == null`;**`EndReason == null`**

#### Scenario: Game 结束状态
- **WHEN** 某方连五或平局或认输或超时后
- **THEN** `EndedAt != null`;`Result != null`;若有胜方则 `WinnerUserId != null`;**`EndReason != null`** 且对应路径

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
| `TurnNotTimedOutException` | 409 |

(本次**仅新增**此 1 条映射;现有 16 条映射 `InvalidRoomNameException` / `RoomNotFoundException` / `RoomNotWaitingException` / `RoomFullException` / `AlreadyInRoomException` / `HostCannotLeaveWaitingRoomException` / `PlayerCannotSpectateException` / `NotInRoomException` / `NotSpectatingException` / `NotAPlayerException` / `NotYourTurnException` / `RoomNotInPlayException` / `InvalidRoomStatusTransitionException` / `DbUpdateConcurrencyException` / `NotRoomHostException`(由 `add-dissolve-room` 加入)/ `InvalidMoveException` 全部保留不变。)

#### Scenario: Worker 竞态的 409(罕见出现在 HTTP)
- **WHEN** `TurnTimeoutCommandHandler` 抛 `TurnNotTimedOutException` 并冒泡(实际 worker try/catch 吞之,故极少进 HTTP;若将来有 REST 端点引入则按 409 返回)
- **THEN** HTTP 409 `ProblemDetails` 指向 `TurnNotTimedOutException`

### Requirement: `Room.Dissolve` 允许 Host 销毁 Waiting 房间

系统 SHALL 在 `Room` 聚合根上提供 `Dissolve(UserId senderId)` 方法。规则:

- 若 `senderId != HostUserId` → MUST 抛 `NotRoomHostException`。
- 若 `Status != Waiting` → MUST 抛 `RoomNotWaitingException`(复用现有异常)。
- 两项校验通过时,方法 MUST 不修改 `Room` 的任何字段 —— 物理删除由仓储层(`IRoomRepository.DeleteAsync`)完成,聚合只"祝福"这次删除。

本方法**不**接收 `DateTime now`:无状态变更,无须记录时间戳。

#### Scenario: Host 解散 Waiting 房间
- **WHEN** Host 对自己创建的 Waiting 房间调 `Dissolve(hostId)`
- **THEN** 方法返回,不抛异常,`Room` 字段保持不变

#### Scenario: 非 Host 尝试解散
- **WHEN** 非 Host 的 `UserId`(例如围观者、未来玩家、任意其他用户)调 `Dissolve`
- **THEN** 抛 `NotRoomHostException`

#### Scenario: Playing 房间不得解散
- **WHEN** Host 在 `Status == Playing` 时调 `Dissolve(hostId)`
- **THEN** 抛 `RoomNotWaitingException`

#### Scenario: Finished 房间不得解散
- **WHEN** Host 在 `Status == Finished` 时调 `Dissolve(hostId)`
- **THEN** 抛 `RoomNotWaitingException`(同样用现有异常,不新增 `RoomAlreadyFinishedException`)

#### Scenario: 带围观者 / 聊天的 Waiting 房间仍可解散
- **WHEN** Waiting 房间有 2 个围观者与若干聊天消息,Host 调 `Dissolve(hostId)`
- **THEN** 方法不抛异常;后续仓储层物理删除会级联清除围观者与聊天(由 EF Cascade 保证)

---

### Requirement: `IRoomRepository.DeleteAsync` 标记删除房间聚合

Application 层 SHALL 在 `IRoomRepository` 上新增:

```
Task DeleteAsync(Room room, CancellationToken cancellationToken);
```

实现 MUST:
- 仅把聚合从上下文中标记为删除(`DbContext.Rooms.Remove(room)` 或等价),MUST NOT 调 `SaveChangesAsync`;
- 依赖 EF 配置的 `OnDelete(Cascade)`(Room → Game / Spectators / ChatMessages,Game → Moves),不在代码里手工遍历子实体删除。

签名 MUST NOT 出现 `IQueryable` / `Expression` / EF Core 类型。

#### Scenario: 仓储删除不提交
- **WHEN** 调用 `DeleteAsync(room, ct)` 后,且同一 handler 尚未调 `IUnitOfWork.SaveChangesAsync`
- **THEN** 数据库中该房间仍存在;`SaveChangesAsync` 被调用后才真正消失

#### Scenario: 级联删除生效
- **WHEN** `DeleteAsync` + `SaveChangesAsync` 一整次事务后,针对被删房间 Id 查询 Games / Moves / RoomSpectators / ChatMessages
- **THEN** 上述子表对应行**全部消失**

---

### Requirement: `IRoomNotifier.RoomDissolvedAsync` 广播房间解散事件

Application 层 SHALL 在 `IRoomNotifier` 上新增:

```
Task RoomDissolvedAsync(RoomId roomId, CancellationToken cancellationToken);
```

Api 层实现 MUST 把客户端方法 `RoomDissolved` 发到 `room:{roomId.Value}` SignalR group,payload 形如 `{ RoomId: Guid }`。

MUST 由 handler 在 `SaveChangesAsync` **之后**调用(遵守现有"事务成功后再推事件"的约束)。

#### Scenario: 广播成功后组清理
- **WHEN** 被解散房间 `{id}` 的 SignalR group 内有 1 个围观者 connection 订阅
- **THEN** 该连接收到一次 `RoomDissolved({RoomId: id})` 事件;之后连接侧主动 `LeaveRoom` 或前端自行处理即可;服务端**不主动**从 group 中移除 connection

#### Scenario: 广播到空 group
- **WHEN** 被解散房间没有任何订阅(无围观者连着)
- **THEN** 调用 MUST 不抛异常,无副作用(SignalR 对空 group 是 no-op)

---

### Requirement: `DELETE /api/rooms/{id}` 端点触发解散

Api 层 SHALL 暴露 `DELETE /api/rooms/{id}`(`[Authorize]`)。Controller 从 JWT `sub` 取 `UserId sender`;发 `DissolveRoomCommand(sender, new RoomId(id))`;成功 `204 No Content`。

MUST NOT 接受 body;MUST NOT 接受 query 参数。

#### Scenario: 成功
- **WHEN** Host 以合法 Bearer token 调 `DELETE /api/rooms/{id}`,对应房间是其创建的 Waiting 房
- **THEN** HTTP 204,响应体为空;随后 `GET /api/rooms/{id}` 返回 404

#### Scenario: 非 Host
- **WHEN** 非 Host 用户 `DELETE /api/rooms/{id}`
- **THEN** HTTP 403,`ProblemDetails.title` 指向 `NotRoomHostException`

#### Scenario: Playing 房间
- **WHEN** Host 对 Playing 房间调 `DELETE /api/rooms/{id}`
- **THEN** HTTP 409,`ProblemDetails` 指向 `RoomNotWaitingException`

#### Scenario: 未登录
- **WHEN** 无 Bearer token 调 `DELETE /api/rooms/{id}`
- **THEN** HTTP 401(由 JWT 中间件处理)

#### Scenario: 房间不存在(或已被并发删除)
- **WHEN** `DELETE /api/rooms/{id}`,但该 Id 不存在
- **THEN** HTTP 404,`ProblemDetails` 指向 `RoomNotFoundException`

---

### Requirement: 新增异常 `NotRoomHostException` 与其 HTTP 映射

系统 SHALL 在 `Gomoku.Domain/Exceptions/RoomExceptions.cs` 新增 `NotRoomHostException`(sealed,继承 `Exception`,提供 `(string message)` 构造器)。

Api 层全局异常中间件 MUST 映射:

| 异常 | HTTP |
|---|---|
| `NotRoomHostException` | 403 |

(现有 `RoomNotFoundException` → 404、`RoomNotWaitingException` → 409 保持不变,本 Requirement 不重申。)

#### Scenario: 映射生效
- **WHEN** 非 Host 用户触发 `NotRoomHostException`(例如通过 `DELETE /api/rooms/{id}`)
- **THEN** 响应 HTTP 403,`ProblemDetails.title` 指向 `NotRoomHostException`,`ProblemDetails.detail` 包含抛出时的 message

### Requirement: `GameEndReason` 枚举表达对局结束原因

系统 SHALL 在 `Gomoku.Domain/Enums/GameEndReason.cs` 定义 `enum GameEndReason { Connected5 = 0, Resigned = 1, TurnTimeout = 2 }`。底层整数值固定,以便序列化稳定性与未来追加(如 `Disconnected = 3`)。

#### Scenario: 枚举值存在
- **WHEN** 审阅 `Gomoku.Domain/Enums/GameEndReason.cs`
- **THEN** 存在三个值 `Connected5=0`、`Resigned=1`、`TurnTimeout=2`

---

### Requirement: `Game.EndReason` 字段记录对局结束原因

`Game` 子实体 MUST 新增 `EndReason: GameEndReason?` 只读属性(`get; private set;`)。对局进行中 MUST 为 `null`;对局结束(`Result != null`)时 MUST 非 `null`,且取值与触发结束的路径对应:`Room.PlayMove` 的连五路径 → `Connected5`、`Room.Resign` → `Resigned`、`Room.TimeOutCurrentTurn` → `TurnTimeout`。

`Game.FinishWith` 的签名 MUST 扩展为 `FinishWith(GameResult, UserId?, GameEndReason, DateTime)`,reason 为必填。

数据库层 MUST 为 `Games.EndReason` 列设置 `INTEGER NULL`,以便老数据(未结束局)保持 `null`。`AddGameEndReason` migration 的 Up MUST 一次性回填:`UPDATE Games SET EndReason = 0 WHERE Result IS NOT NULL`(所有老 Finished 局都是连五胜,唯一已实现的结束路径)。

#### Scenario: 进行中局 EndReason 为 null
- **WHEN** 查询某 `Status == Playing` 房间的 `Game.EndReason`
- **THEN** 返回 `null`

#### Scenario: 连五结束局 EndReason 为 Connected5
- **WHEN** 对局通过 `Room.PlayMove` 连五结束
- **THEN** `Game.EndReason == GameEndReason.Connected5`,`Game.Result != null`,`Game.EndedAt != null`

#### Scenario: 认输结束局 EndReason 为 Resigned
- **WHEN** 对局通过 `Room.Resign` 结束
- **THEN** `Game.EndReason == GameEndReason.Resigned`

#### Scenario: 超时结束局 EndReason 为 TurnTimeout
- **WHEN** 对局通过 `Room.TimeOutCurrentTurn` 结束
- **THEN** `Game.EndReason == GameEndReason.TurnTimeout`

---

### Requirement: `Room.Resign` 允许玩家任意时刻认输

系统 SHALL 在 `Room` 聚合根上提供 `Resign(UserId userId, DateTime now) : GameEndOutcome` 方法。规则:

- `Status != Playing` 或 `Game == null` → MUST 抛 `RoomNotInPlayException`
- `userId` 不是 `BlackPlayerId` 且不是 `WhitePlayerId` → MUST 抛 `NotAPlayerException`
- **MUST NOT** 检查 `CurrentTurn` —— 认输不限回合,可在对手回合认输
- 推导对手棋色与 UserId;`Game.FinishWith(opponentResult, opponentUserId, GameEndReason.Resigned, now)`;`Status` 转换为 `Finished`
- 返回 `GameEndOutcome(opponentResult, opponentUserId)`

新 record `GameEndOutcome(GameResult Result, UserId? WinnerUserId)` MUST 定义在 `Gomoku.Domain.Rooms` 命名空间,与现有 `MoveOutcome` 同文件,是 `Resign` / `TimeOutCurrentTurn` 的通用返回类型。

#### Scenario: 黑方认输
- **WHEN** Black 玩家(含 Host)在 Playing 状态调 `Resign(hostId, now)`
- **THEN** 返回 `GameEndOutcome(WhiteWin, whitePlayerId)`;`Game.Result == WhiteWin`;`Game.WinnerUserId == whitePlayerId`;`Game.EndReason == Resigned`;`Game.EndedAt == now`;`Room.Status == Finished`

#### Scenario: 白方认输
- **WHEN** White 玩家调 `Resign(whiteId, now)`
- **THEN** 返回 `GameEndOutcome(BlackWin, blackPlayerId)`;其他字段对称

#### Scenario: 非自己回合也可认输
- **WHEN** `CurrentTurn == Black`,White 玩家调 `Resign(whiteId, now)`
- **THEN** 不抛异常;对局按白方认输 / 黑方胜结束

#### Scenario: 非玩家认输被拒
- **WHEN** 非 Black / White 的 `UserId`(围观者或任意其他用户)调 `Resign`
- **THEN** 抛 `NotAPlayerException`

#### Scenario: Waiting / Finished 状态调用
- **WHEN** `Status != Playing`
- **THEN** 抛 `RoomNotInPlayException`

---

### Requirement: `Room.TimeOutCurrentTurn` 按阈值判当前回合玩家超时负

系统 SHALL 在 `Room` 聚合根上提供 `TimeOutCurrentTurn(DateTime now, int turnTimeoutSeconds) : GameEndOutcome`。规则:

- `Status != Playing` 或 `Game == null` → MUST 抛 `RoomNotInPlayException`
- `turnTimeoutSeconds < 1` → MUST 抛 `ArgumentOutOfRangeException`
- 计算 `lastActivity = Game.Moves.OrderBy(m => m.Ply).LastOrDefault()?.PlayedAt ?? Game.StartedAt`
- `(now - lastActivity).TotalSeconds < turnTimeoutSeconds` → MUST 抛 `TurnNotTimedOutException`(防 worker 竞态)
- `>= turnTimeoutSeconds` 时:`CurrentTurn` 的棋色方为 loser,对方为 winner;`Game.FinishWith(winnerResult, winnerUserId, GameEndReason.TurnTimeout, now)`;`Status = Finished`
- 返回 `GameEndOutcome(winnerResult, winnerUserId)`

#### Scenario: 黑方超时
- **WHEN** `CurrentTurn == Black`,`lastActivity = t0`,`now - t0 = 61s`,`timeout = 60`
- **THEN** 返回 `GameEndOutcome(WhiteWin, whitePlayerId)`;`Game.Result == WhiteWin`;`Game.WinnerUserId == whitePlayerId`;`Game.EndReason == TurnTimeout`;`Room.Status == Finished`

#### Scenario: 白方超时
- **WHEN** 黑方已走 1 子(ply=1, playedAt=t1),`CurrentTurn == White`,`now - t1 >= timeout`
- **THEN** 返回 `GameEndOutcome(BlackWin, blackPlayerId)`

#### Scenario: 无 Moves 时以 StartedAt 为基准
- **WHEN** `Game.Moves.Count == 0`,`now - Game.StartedAt >= timeout`
- **THEN** 黑方超时 → 白方胜

#### Scenario: 阈值恰好
- **WHEN** `(now - lastActivity).TotalSeconds == turnTimeoutSeconds`(例如都为 60)
- **THEN** **成功判负**(用 `>=` 比较,不是 `>`)

#### Scenario: 尚未超时
- **WHEN** `(now - lastActivity).TotalSeconds < turnTimeoutSeconds`(例如 59 vs 60)
- **THEN** 抛 `TurnNotTimedOutException`;`Room` / `Game` 状态保持不变

#### Scenario: 非法 timeout 参数
- **WHEN** `turnTimeoutSeconds == 0`
- **THEN** 抛 `ArgumentOutOfRangeException`

#### Scenario: 非 Playing 状态
- **WHEN** `Status != Playing`
- **THEN** 抛 `RoomNotInPlayException`

---

### Requirement: 新增异常 `TurnNotTimedOutException` 与其 HTTP 映射

系统 SHALL 在 `Gomoku.Domain/Exceptions/RoomExceptions.cs` 新增 `TurnNotTimedOutException`(sealed,继承 `Exception`,`(string message)` 构造)。

Api 层全局异常中间件 MUST 映射:

| 异常 | HTTP |
|---|---|
| `TurnNotTimedOutException` | 409 |

与现有 `RoomNotInPlayException` / `NotYourTurnException` 等 409 同组。

#### Scenario: 映射生效
- **WHEN** Worker 发 `TurnTimeoutCommand` 进入 handler 后 Domain 发现并未真超时,抛 `TurnNotTimedOutException`
- **THEN** 若事件冒泡到 HTTP(实际上 worker 会 try/catch 吞),响应 409;**在 worker 场景下,异常不冒泡到 HTTP**,worker 仅记日志并丢弃,下轮查询会自动不命中该房间

---

### Requirement: `IRoomRepository.GetRoomsWithExpiredTurnsAsync` 查询超时房间

Application 层 SHALL 在 `IRoomRepository` 上新增:

```
Task<IReadOnlyList<RoomId>> GetRoomsWithExpiredTurnsAsync(DateTime now, int turnTimeoutSeconds, CancellationToken cancellationToken);
```

实现 MUST 返回满足以下条件的所有房间 Id:
- `Status == Playing`
- `Game != null`
- `max(Moves.PlayedAt, Game.StartedAt) + turnTimeoutSeconds <= now`(即"当前回合已超时")

只返回 `RoomId` 列表,MUST NOT 物化 `Room` 聚合。签名 MUST 不暴露 EF 类型。

#### Scenario: 无超时房间
- **WHEN** 所有 Playing 房间的当前回合都在 `now - turnTimeoutSeconds` 之后
- **THEN** 返回空列表,不抛

#### Scenario: 一房间超时
- **WHEN** 一个 Playing 房间的最后一步 Move PlayedAt 是 `now - 70s`,`turnTimeoutSeconds = 60`
- **THEN** 返回该房间 Id(正好一个元素)

#### Scenario: Finished 房间不包括
- **WHEN** 一个房间已 Finished 但因某些原因 Moves 数据暴露在超时窗口内
- **THEN** MUST NOT 返回其 Id(`Status == Playing` 过滤生效)

---

### Requirement: `GameOptions` 绑定 `"Game"` 配置段

Application 层 SHALL 定义 `GameOptions`,绑定 `appsettings.json` 的 `"Game"` 段。字段:

- `TurnTimeoutSeconds: int`(`[Range(10, 3600)]`,默认 60)
- `TimeoutPollIntervalMs: int`(`[Range(1000, 60000)]`,默认 5000)

Api 层 `Program.cs` MUST 通过 `services.AddOptions<GameOptions>().BindConfiguration("Game").ValidateDataAnnotations().ValidateOnStart()` 注册。

#### Scenario: 启动默认值
- **WHEN** `appsettings.json` 没有 `"Game"` 段
- **THEN** `GameOptions` 采用默认值 `TurnTimeoutSeconds=60`、`TimeoutPollIntervalMs=5000`

#### Scenario: 合法覆盖
- **WHEN** `appsettings.Development.json` 写 `"Game": { "TurnTimeoutSeconds": 30, "TimeoutPollIntervalMs": 2000 }`
- **THEN** 运行时采用覆盖值

#### Scenario: 非法值拒绝
- **WHEN** 配置 `"TurnTimeoutSeconds": 5`(低于 10)
- **THEN** 应用启动失败(options validation 阻断),不进入 `app.Run()`

---

### Requirement: `ResignCommand` + `/api/rooms/{id}/resign` 暴露主动认输

Application 层 SHALL 新增:

```
public sealed record ResignCommand(UserId UserId, RoomId RoomId) : IRequest<GameEndedDto>;
```

Handler 流程:
1. Load room(null → `RoomNotFoundException`)
2. `var outcome = room.Resign(UserId, _clock.UtcNow)`
3. `await GameEloApplier.ApplyAsync(room, outcome.Result, _users, ct)`
4. `await _uow.SaveChangesAsync(ct)`
5. 构造 `GameEndedDto(outcome.Result, outcome.WinnerUserId?.Value, room.Game!.EndedAt!.Value, room.Game.EndReason!.Value)`
6. Notifier 顺序调用:`RoomStateChangedAsync(room.Id, state, ct)` → `GameEndedAsync(room.Id, dto, ct)`(**不**发 MoveMade)
7. 返回 `GameEndedDto`

Api 层 SHALL 暴露 `POST /api/rooms/{id}/resign`(`[Authorize]`),成功 200 + `GameEndedDto`。MUST NOT 接受 body;MUST NOT 接受 query。调用方 `UserId` 从 JWT `sub` 取。

#### Scenario: 玩家成功认输
- **WHEN** Playing 房间的 Black 玩家 Alice 调 `POST /api/rooms/{id}/resign`
- **THEN** HTTP 200,body 是 `GameEndedDto { Result: WhiteWin, WinnerUserId: whiteId, EndedAt, EndReason: Resigned }`;数据库 `Room.Status == Finished`;双方 `User.Rating` / `Wins` / `Losses` 更新一次

#### Scenario: 未登录拒绝
- **WHEN** 无 Bearer token 调 `POST /api/rooms/{id}/resign`
- **THEN** HTTP 401(JWT 中间件)

#### Scenario: 非玩家认输
- **WHEN** 围观者 / 非成员调 `POST /api/rooms/{id}/resign`
- **THEN** HTTP 403 `NotAPlayerException`

#### Scenario: 房间不在 Playing
- **WHEN** 对 Waiting 或 Finished 房间调
- **THEN** HTTP 409 `RoomNotInPlayException`

#### Scenario: 对局结束事件包含 EndReason
- **WHEN** 认输成功后
- **THEN** SignalR `GameEnded` event 的 payload `GameEndedDto` 的 `EndReason == Resigned`;客户端据此显示"对方认输"

---

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
- **WHEN** 审阅 `RoomsController` / `GomokuHub`
- **THEN** 无任何 action / method 构造或分发 `TurnTimeoutCommand`

#### Scenario: Worker 成功触发
- **WHEN** `TurnTimeoutWorker` 发 `TurnTimeoutCommand(roomId)`,handler 执行
- **THEN** Room.Status 转为 Finished;ELO 被应用;SignalR `GameEnded { EndReason: TurnTimeout }` 被广播

#### Scenario: 竞态:worker 晚到一步
- **WHEN** Worker 的 `GetRoomsWithExpiredTurnsAsync` 说"超时了",但到 handler 执行时对手刚落了一子
- **THEN** `Room.TimeOutCurrentTurn` 抛 `TurnNotTimedOutException`;worker 的 try/catch 吞下并记日志,**不**广播事件,Room 保持 Playing

---

### Requirement: `TurnTimeoutWorker` 后台轮询超时房间

Infrastructure 层 SHALL 新增 `BackgroundServices/TurnTimeoutWorker : BackgroundService`。循环:

```
while (!stopToken.IsCancellationRequested)
{
    await Task.Delay(options.TimeoutPollIntervalMs, stopToken);
    using var scope = sp.CreateScope();
    var rooms = scope.Resolve<IRoomRepository>();
    var sender = scope.Resolve<ISender>();
    var clock = scope.Resolve<IDateTimeProvider>();
    var ids = await rooms.GetRoomsWithExpiredTurnsAsync(clock.UtcNow, options.TurnTimeoutSeconds, stopToken);
    foreach (var id in ids)
    {
        try
        {
            await sender.Send(new TurnTimeoutCommand(id), stopToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "TurnTimeoutWorker failed on room {RoomId}", id);
        }
    }
}
```

MUST 满足:
- 每循环 `CreateScope` 以获得正确生命周期的 DbContext / handlers;
- 非取消异常不中断 worker;
- 使用同一 `IDateTimeProvider.UtcNow` 作为时间基准,便于测试注入假时钟。

#### Scenario: 空载
- **WHEN** 无 Playing 房间或全部都未超时
- **THEN** 每轮查询返回空;worker 不发命令,不报错

#### Scenario: 异常不中断
- **WHEN** 某次 `ISender.Send` 抛意外异常
- **THEN** worker 记 Error 日志,继续处理下一房间;下一轮正常运行

#### Scenario: 优雅关闭
- **WHEN** `stopToken` 触发
- **THEN** `ExecuteAsync` 退出,不吃 `OperationCanceledException`

---

### Requirement: `GameEloApplier` 共享 helper 把 ELO 应用抽公

Application 层 SHALL 在 `Features/Rooms/Common/GameEloApplier.cs` 定义 `internal static class GameEloApplier`,方法:

```
public static async Task ApplyAsync(
    Room room,
    GameResult result,
    IUserRepository users,
    CancellationToken cancellationToken);
```

实现 MUST 等价于原 `MakeMoveCommandHandler.ApplyEloAsync`:加载 Black / White 双方 `User`;推导 `GameOutcome`;调 `EloRating.Calculate`;两位 `User` 各调 `RecordGameResult`。MUST NOT 调 `SaveChangesAsync`。

三个 handler(`MakeMoveCommandHandler` / `ResignCommandHandler` / `TurnTimeoutCommandHandler`)在对局结束路径上 MUST 调用此 helper。

#### Scenario: 连五路径仍正常结算
- **WHEN** 玩家连五结束对局(`MakeMoveCommand` 触发 `GameEloApplier.ApplyAsync`)
- **THEN** ELO 行为与重构前完全一致(保留现有 `MakeMoveCommandHandlerTests` 的断言仍全绿)

#### Scenario: 认输路径结算
- **WHEN** 认输结束对局(`ResignCommand` 触发 `GameEloApplier.ApplyAsync`)
- **THEN** 同样调双方 `FindByIdAsync` 一次、各调 `RecordGameResult` 一次,事务在外层 handler 合并 SaveChangesAsync

#### Scenario: 超时路径结算
- **WHEN** 超时结束对局(`TurnTimeoutCommand`)
- **THEN** 与认输一致

---

### Requirement: `GameSnapshotDto` 扩展 TurnStartedAt / TurnTimeoutSeconds / EndReason

`GameSnapshotDto` MUST 追加三个字段(纯追加,向后兼容):

- `DateTime TurnStartedAt` —— 当前回合起始时间,等价于 `Moves.OrderBy(Ply).LastOrDefault()?.PlayedAt ?? Game.StartedAt`
- `int TurnTimeoutSeconds` —— 由 `GameOptions.TurnTimeoutSeconds` 传入的阈值(不同房间相同,为前端倒计时 UI 提供)
- `GameEndReason? EndReason` —— 与 `Game.Result` 同时为 null 或同时非 null

`GameEndedDto` MUST 追加字段 `GameEndReason EndReason`(非 nullable,结束事件时必有)。

`RoomMapping.ToState` MUST 在入参里接受 `turnTimeoutSeconds` 参数,并计算 `TurnStartedAt`。

#### Scenario: 进行中 DTO
- **WHEN** 对 Playing 房间构造 `GameSnapshotDto`
- **THEN** `TurnStartedAt` 是最后一步 `PlayedAt`(或 `StartedAt` 如无 Moves);`TurnTimeoutSeconds > 0`;`EndReason == null`

#### Scenario: 结束 DTO
- **WHEN** 对 Finished 房间构造 `GameSnapshotDto`
- **THEN** `EndReason` 取对应值(Connected5 / Resigned / TurnTimeout)

#### Scenario: GameEndedDto 总含 EndReason
- **WHEN** 任一路径触发 `GameEndedAsync` 广播
- **THEN** payload `GameEndedDto.EndReason` 非 null 且匹配实际原因

