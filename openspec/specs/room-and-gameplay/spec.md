# Room and Gameplay

## Purpose

房间生命周期与对局推进的核心能力:`Room` 聚合根(承载玩家、围观者、对局、聊天、催促时间戳)、`Game` 子实体(当前回合、结果、胜方、Moves 历史)、`Move` 子实体(按 Ply 递增)、`RoomSpectator` 联结实体,以及把 `gomoku-domain` 的 `Board` / 判胜接入对局流程的规则。

HTTP 表面:`POST/GET /api/rooms`、`GET /api/rooms/{id}`、`POST /api/rooms/{id}/{join,leave}`、`POST/DELETE /api/rooms/{id}/spectate`。SignalR 表面:`/hubs/gomoku` 的五个客户端方法(`JoinRoom` / `LeaveRoom` / `MakeMove` / `SendChat` / `Urge`)与服务端事件(`RoomState` / `PlayerJoined` / `PlayerLeft` / `SpectatorJoined` / `SpectatorLeft` / `MoveMade` / `GameEnded` / `ChatMessage` / `UrgeReceived`)。

实现位于 `backend/src/Gewu.Domain/Rooms/`(聚合)、`backend/src/Gewu.Application/Features/Rooms/`(CQRS handlers)、`backend/src/Gewu.Infrastructure/Persistence/`(EF 映射与仓储)、`backend/src/Gewu.Api/Hubs/`(SignalR Hub 与 IRoomNotifier 实现)。
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

系统 SHALL 提供 `Room.Create(RoomId id, string name, UserId hostUserId, DateTime createdAt, string gameKey)`。返回的 `Room` MUST 满足:

- `Id / HostUserId / CreatedAt / GameKey` 等于入参
- `Name` 经过 trim 后长度在 [3..50];非法名称抛 `InvalidRoomNameException`
- `BlackPlayerId = hostUserId`(创建者默认黑方)
- `WhitePlayerId = null`
- `Status = Waiting`
- `Game = null`
- `LastUrgeAt = null`, `LastUrgeByUserId = null`
- `Spectators` 为空,`ChatMessages` 为空

`gameKey` MUST 为非空字符串;`Room.Create` 本身 MUST NOT 校验该键是否已登记 —— `Domain`
不认识注册表,校验属于 Application 层(见下方"建房路径校验棋种"),这是 `Domain` 零外部
依赖约束的直接后果。

#### Scenario: 成功创建
- **WHEN** 以合法参数调用 `Room.Create(...)`
- **THEN** 返回 `Room` 实例,字段等于上述初始值

#### Scenario: 名称非法
- **WHEN** `name` 为 `null` / 空 / 全空白 / 短于 3 / 超过 50 字符
- **THEN** 抛 `InvalidRoomNameException`,消息明确违反规则

#### Scenario: 棋种为空
- **WHEN** `gameKey` 为 `null` / 空 / 全空白
- **THEN** 抛 `ArgumentException`

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

`Move` MUST 包含:`Id: Guid`、`GameId: Guid`、`Ply: int (1-based)`、`Stone: Stone`、`PlayedAt: DateTime`(UTC),外加**恰好一种载荷**(见下一条 Requirement)。数据库持久化:`(GameId, Ply)` 唯一。

#### Scenario: Ply 从 1 起严格递增
- **WHEN** 在同一局依次走 3 步
- **THEN** 三个 `Move` 的 `Ply` 分别为 1、2、3

---

### Requirement: 从 `Moves` 在内存 replay 得到当前 `Board`

`Game` MUST NOT 冗余存储盘面。需要当前 `Board` 时,SHALL 由 `Game.ReplayBoard(IGameRules rules)` 从 `Moves` 按 `Ply` 升序重放得到 —— 棋盘的尺寸与连子长度来自传入的规则,因此同一段落子序列在不同棋种下重放出对应尺寸的棋盘。

规则同样 MUST 由调用方传入,理由与 `PlayMove` 一致。

#### Scenario: replay 还原盘面
- **WHEN** `Game.Moves` 含 10 步,调 `ReplayBoard(gomokuRules)`
- **THEN** 返回的 `Board` 上这 10 个位置的 `Stone` 与 `Moves` 一致,其余为 `Empty`

#### Scenario: replay 尺寸随规则
- **WHEN** 以五子棋规则重放
- **THEN** 得到 15×15 的棋盘

### Requirement: 并发落子由 EF 乐观并发保护

`Game` 实体 MUST 配 `RowVersion` 列并在 EF 配置中 `.IsRowVersion()`。当两个 `MakeMoveCommand` handler 对同一 `Game` 并发 `SaveChangesAsync`,一者 MUST 得到 `DbUpdateConcurrencyException`;Api 层异常中间件 MUST 将其映射为 HTTP 409 + `ProblemDetails`,`type = "https://gewu/errors/concurrent-move"`。

#### Scenario: 并发争抢
- **WHEN** 两个请求携带相同的 `RoomId` 和不同的 `Position`,几乎同时到达
- **THEN** 一者成功(HTTP 200 + 新 Move 持久),另一者收到 HTTP 409,客户端应重新拉取 `RoomState` 再决定是否重试

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
| POST | `/api/rooms` | `{ name, gameKey }` | 201 + `RoomSummaryDto` | 创建房间(调用方成为 Host 与黑方) |
| GET | `/api/rooms?gameKey=` | — | 200 + `RoomSummaryDto[]` | 指定棋种的活跃房间列表(Waiting + Playing) |
| POST | `/api/rooms/ai` | `{ name, difficulty, humanSide?, gameKey }` | 201 + `RoomStateDto` | 创建人机房间 |
| GET | `/api/rooms/{id}` | — | 200 + `RoomStateDto` | 完整房间状态(含 Moves) |
| POST | `/api/rooms/{id}/join` | — | 200 + `RoomStateDto` | 以当前用户身份加入为白方 |
| POST | `/api/rooms/{id}/leave` | — | 204 | 离开房间(玩家或围观者) |
| POST | `/api/rooms/{id}/spectate` | — | 204 | 加入围观 |
| DELETE | `/api/rooms/{id}/spectate` | — | 204 | 离开围观 |

`gameKey` 在这三个端点上 MUST 为**必填**。Api 层 MUST NOT 为它填任何缺省值 —— 调用方不说自己要哪个棋种时,服务端 MUST 回 400,而不是替它选一个。

缺省曾经存在,理由写的是「已发布的客户端不会送这个字段」。已发布的客户端有**零个**:本仓库没有部署,唯一的客户端就在 `frontend-web/`,而它从未送过这个字段。那不是兼容层,是一处写在服务端、因而任何客户端读者都看不见的硬编码。

`humanSide` 仍然可缺省(填 `Stone.Black`),两者**不对称是刻意的**:给一个缺省的边,是在调用方已经指名的棋种**之内**补全一个不完整的请求;给一个缺省的棋种,是换掉他在玩的游戏。

**落子、聊天、催促不走 REST**,由 SignalR Hub 路由(见下一个 Requirement)。

#### Scenario: 列表只含活跃房间
- **WHEN** 已有 3 个 `Waiting`、2 个 `Playing`、1 个 `Finished` 五子棋房间,调 `GET /api/rooms?gameKey=gomoku`
- **THEN** 返回 5 个摘要,不含 `Finished` 房间

#### Scenario: 加入不存在的房间
- **WHEN** `POST /api/rooms/{id}/join` 指向不存在的 id
- **THEN** HTTP 404,错误类型 `RoomNotFoundException`

#### Scenario: 列表按棋种隔离
- **WHEN** 存在 2 个 `gomoku` 活跃房间与 3 个 `tictactoe` 活跃房间,调 `GET /api/rooms?gameKey=tictactoe`
- **THEN** 只返回那 3 个一字棋房间

#### Scenario: 缺少棋种是 400,不是 gomoku
- **WHEN** 调 `GET /api/rooms`(不带查询串),或 `POST /api/rooms` 送 `{ name }`,或 `POST /api/rooms/ai` 送 `{ name, difficulty }`
- **THEN** HTTP 400,错误点名 `GameKey` 字段;MUST NOT 建出任何房间,MUST NOT 返回五子棋房间列表

#### Scenario: 未登记的棋种建房被拒
- **WHEN** `POST /api/rooms` 送 `{ name, gameKey: "go" }`(围棋不在本平台上)
- **THEN** HTTP 400 —— 房间尚不存在,这是请求本身不合法,而不是资源缺失

#### Scenario: 未登记的棋种查列表返回空
- **WHEN** `GET /api/rooms?gameKey=go`
- **THEN** HTTP 200 + 空数组 —— 集合端点上"没有这种房间"与"没有这个棋种"对调用方无区别,MUST NOT 报错

#### Scenario: 已登记但无人人对战的棋种建真人房被拒
- **WHEN** `POST /api/rooms` 送 `{ name, gameKey: "tictactoe" }`
- **THEN** HTTP 400 —— 理由是 `SupportsHumanVsHuman == false`,**不是**"这个棋种不存在"

#### Scenario: 象棋现在开得出真人房
- **WHEN** `POST /api/rooms` 送 `{ name, gameKey: "xiangqi" }`
- **THEN** HTTP 201 —— 象棋自 `enable-xiangqi-human-play` 起开放人人对战。**本条此前举的例子就是象棋,而它已经过期。** 举例用的棋种会随能力变化而失效,而一条把过期事实钉成正确的断言会一直是绿的 —— `enforce-human-vs-human` 为这件事付过一次账

#### Scenario: 缺省的边仍然被补全
- **WHEN** `POST /api/rooms/ai` 送 `{ name, difficulty, gameKey }` 而不带 `humanSide`
- **THEN** HTTP 201,真人执黑 —— 本条与棋种的必填不对称,是有意为之

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

系统 SHALL 在 `Gewu.Domain/Exceptions/RoomExceptions.cs` 新增 `NotRoomHostException`(sealed,继承 `Exception`,提供 `(string message)` 构造器)。

Api 层全局异常中间件 MUST 映射:

| 异常 | HTTP |
|---|---|
| `NotRoomHostException` | 403 |

(现有 `RoomNotFoundException` → 404、`RoomNotWaitingException` → 409 保持不变,本 Requirement 不重申。)

#### Scenario: 映射生效
- **WHEN** 非 Host 用户触发 `NotRoomHostException`(例如通过 `DELETE /api/rooms/{id}`)
- **THEN** 响应 HTTP 403,`ProblemDetails.title` 指向 `NotRoomHostException`,`ProblemDetails.detail` 包含抛出时的 message

### Requirement: `GameEndReason` 枚举表达对局结束原因

`GameEndReason` SHALL 定义 `Decided = 0` / `Resigned` / `TurnTimeout`。

`Decided` **重命名自 `Connected5`**,底层值不变。原名描述的是五子棋的胜利条件,而这个字段回答的
问题是「这局怎么结束的」,答案只有三类:规则从局面判出了结果 / 有人认输 / 时间到。

它不是陈旧而是**错的** —— 一字棋从上线第一天起就在给三连记录「Connected5」,象棋会给将死记录
同一个词。`Decided` 同时覆盖平局(一字棋满盘和棋也是规则判出来的)。

底层值保持 `0`,数据库存的是 int,**不需要数据迁移**;变的只有 JSON 线上的字符串,
而 web 与后端同批发布。

#### Scenario: 底层值不变
- **WHEN** 检视 `GameEndReason.Decided`
- **THEN** 其值为 `0`,与原 `Connected5` 相同 —— 既有行不需要改写

#### Scenario: 枚举里没有棋种专名
- **WHEN** 反射检视 `GameEndReason` 的成员名
- **THEN** MUST NOT 出现任何以某个棋种胜利条件命名的成员

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

新 record `GameEndOutcome(GameResult Result, UserId? WinnerUserId)` MUST 定义在 `Gewu.Domain.Rooms` 命名空间,与现有 `MoveOutcome` 同文件,是 `Resign` / `TimeOutCurrentTurn` 的通用返回类型。

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

系统 SHALL 在 `Gewu.Domain/Exceptions/RoomExceptions.cs` 新增 `TurnNotTimedOutException`(sealed,继承 `Exception`,`(string message)` 构造)。

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
- **WHEN** 审阅 `RoomsController` / `MatchHub`
- **THEN** 无任何 action / method 构造或分发 `TurnTimeoutCommand`

#### Scenario: Worker 成功触发
- **WHEN** `TurnTimeoutWorker` 发 `TurnTimeoutCommand(roomId)`,handler 执行
- **THEN** Room.Status 转为 Finished;ELO 被应用;SignalR `GameEnded { EndReason: TurnTimeout }` 被广播

#### Scenario: 竞态:worker 晚到一步
- **WHEN** Worker 的 `GetRoomsWithExpiredTurnsAsync` 说"超时了",但到 handler 执行时对手刚落了一子
- **THEN** `Room.TimeOutCurrentTurn` 抛 `TurnNotTimedOutException`;worker 的 try/catch 吞下并记日志,**不**广播事件,Room 保持 Playing

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

### Requirement: `IRoomRepository.GetActiveRoomsByUserAsync` 查询用户参与的活跃房间

Application 层 SHALL 在 `IRoomRepository` 上新增:

```
Task<IReadOnlyList<Room>> GetActiveRoomsByUserAsync(
    UserId userId, CancellationToken cancellationToken);
```

实现 MUST:
- 过滤 `Status != Finished`(覆盖 Waiting + Playing);
- 过滤 `BlackPlayerId == userId OR WhitePlayerId == userId`(玩家,不含围观者);
- `Include(Game).ThenInclude(Moves)` + `Include("_spectators")`(用于 `RoomSummaryDto.SpectatorCount`);
- 按 `CreatedAt DESC` 排序(最近创建在前)。
- 返回类型是领域类型,不暴露 EF。

围观者关系**不**进结果 —— 本 requirement 只回答"我作为玩家的活动房间";围观由后续
`add-my-spectating-rooms` 独立覆盖。

#### Scenario: 返回用户的 Waiting + Playing
- **WHEN** Alice 有 1 个 Waiting(她 Host) + 1 个 Playing(她 Black)+ 1 个别人的房(她仅围观)+ 1 个 Finished(她参与过);调 `GetActiveRoomsByUserAsync(alice.Id, ct)`
- **THEN** 返回 2 个房间:her Waiting + her Playing;围观的 + Finished 都不在

#### Scenario: Alice 作为 White 玩家也进结果
- **WHEN** Alice 在某 Playing 房是 WhitePlayerId
- **THEN** 该房间出现在结果中

#### Scenario: 无活动房间
- **WHEN** 用户从未加入任何房间
- **THEN** 返回空列表(Count == 0)

#### Scenario: 排序
- **WHEN** 用户有两个活动房间,CreatedAt 分别为 T1 < T2
- **THEN** 结果顺序 `[T2, T1]`

---

### Requirement: `GetMyActiveRoomsQuery` 映射为 `RoomSummaryDto[]`

Application 层 SHALL 在 `Features/Rooms/GetMyActiveRooms/` 定义:

```
public sealed record GetMyActiveRoomsQuery(UserId UserId)
    : IRequest<IReadOnlyList<RoomSummaryDto>>;
```

Handler 调 `IRoomRepository.GetActiveRoomsByUserAsync`,收集所有出现的 UserId(Host + Black + White,通过 `room.CollectUserIds()`),一次性 `LookupUsernamesAsync`,然后用 `RoomMapping.ToSummary` 映射。**不**分页(典型返回 0-5 条)。

#### Scenario: 多房映射完整
- **WHEN** 仓储返回 Alice 的 2 个活动房(Alice Host,Bob White 加入其一)
- **THEN** 返回 `RoomSummaryDto[]` 长 2;每条 `Host.Username` == "Alice";White 字段有值的那条 `White.Username == "Bob"`

#### Scenario: 空结果
- **WHEN** 仓储返回空
- **THEN** handler 返回空列表(不抛、不 lookup usernames)

---

### Requirement: `GET /api/users/me/active-rooms` 端点

Api 层 SHALL 暴露 `GET /api/users/me/active-rooms`(`[Authorize]`):

- Controller 从 JWT `sub` 取 `UserId`;派 `GetMyActiveRoomsQuery(currentUserId)`;
- 成功 HTTP 200 + `IReadOnlyList<RoomSummaryDto>`;
- 未登录 HTTP 401(JWT 中间件)。

路由不与现有 `me`(`GET /me`)或 `{id:guid}/games` 冲突 —— 路径精确匹配 `me/active-rooms`。

#### Scenario: 登录用户拉活动房间
- **WHEN** Alice 登录,参与 1 Waiting + 1 Playing;调 `GET /api/users/me/active-rooms`
- **THEN** HTTP 200;body 长 2 的 `RoomSummaryDto[]`;按 CreatedAt DESC 排序

#### Scenario: 无活动房间
- **WHEN** Alice 无任何活动房间
- **THEN** HTTP 200;body `[]`

#### Scenario: 未登录
- **WHEN** 无 Bearer token
- **THEN** HTTP 401

#### Scenario: Finished 房间不在
- **WHEN** Alice 的某房间进入 Finished;再调
- **THEN** 该房间不在返回

### Requirement: `Room.SwapPlayers(now)` 在棋局未开局时交换黑白方

`Room` 聚合 SHALL 提供 `void SwapPlayers(DateTime now)` 公共方法,行为:

- 前置条件:`Status == RoomStatus.Playing` AND `Game!.Moves.Count == 0`(刚 `JoinAsPlayer` 完、第一手还没下)。任一条件不满足 MUST 抛 `InvalidOperationException`("Cannot swap players after the first move." 或等价描述)。
- 操作:**仅交换** `BlackPlayerId` 与 `WhitePlayerId` 两个字段。
- 不变量:`HostUserId` MUST NOT 改变(host 仍是房间创建者);`Game.CurrentTurn` MUST NOT 改变(始终是 `Stone.Black`,因为黑子先行的规则与"谁坐黑"无关)。
- 不发任何 SignalR 事件 —— 通常在 `CreateAiRoomCommandHandler` 内 + `JoinAsPlayer` 同事务里调用,事务提交后客户端首次拉房间状态拿到的就是已交换的状态。

#### Scenario: 合法窗口内交换
- **WHEN** 一房间刚 `Room.Create + JoinAsPlayer` 完成(Status=Playing,Moves 为空),调 `room.SwapPlayers(now)`
- **THEN** `BlackPlayerId` 和 `WhitePlayerId` 互换;`HostUserId` 不变;`Game.CurrentTurn == Stone.Black`(不变)

#### Scenario: 已有落子时拒绝
- **WHEN** 房间已经有至少 1 步落子,调 `room.SwapPlayers(now)`
- **THEN** 抛 `InvalidOperationException`;字段未改变

#### Scenario: Waiting 状态拒绝
- **WHEN** 一房间刚 `Room.Create` 完(Status=Waiting,WhitePlayerId=null),调 `room.SwapPlayers(now)`
- **THEN** 抛 `InvalidOperationException`(只有完整双方 Playing 状态才允许 swap)

#### Scenario: Finished 状态拒绝
- **WHEN** 房间 Status=Finished(已结束的对局),调 `room.SwapPlayers(now)`
- **THEN** 抛 `InvalidOperationException`

### Requirement: `Room` 记录自己是哪一种棋

`Room` SHALL 持有 `GameKey`(非空字符串),标识该房间玩的是哪个棋种。既有房间一律为 `'gomoku'`。

`GameKey` MUST 是字符串而非枚举 —— 新增棋种的全部意义就在于不必修改一个共享类型,与游戏目录、`IPuzzleRules` 注册表的选择一致。

创建房间的路径 SHALL 接受调用方指定棋种,并 MUST 在建房前校验该键能在 `IGameRulesRegistry`
中解析 —— 未登记的键 MUST 在聚合被构造之前就被拒绝。

落子路径 SHALL 在解析规则失败时返回 404 —— 那是"房间的 `GameKey` 指向一个本构建不认识的
棋种"的唯一可能来源(手工改过的数据,或降级过的构建)。

#### Scenario: 既有房间是五子棋
- **WHEN** 读取迁移前创建的任意房间
- **THEN** `GameKey == "gomoku"`

#### Scenario: 新建房间写入已登记的棋种
- **WHEN** 通过 `CreateRoom` 或 `CreateAiRoom` 建房并指定 `"tictactoe"`
- **THEN** `GameKey == "tictactoe"`,且该键能在规则注册表中解析出规则

#### Scenario: 房间指向未知棋种时落子返回 404
- **WHEN** 某房间的 `GameKey` 在注册表中不存在,玩家尝试落子
- **THEN** handler 返回 404,MUST NOT 抛未处理异常

### Requirement: 落子入参校验只管与棋种无关的那一半

落子入参的校验 SHALL 只在应用层校验器里保留**与棋种无关**的那一半:行列非负,违反返回 400。

上界属于棋种,而校验器跑在解析房间(因而也是棋种)之前,所以超界 SHALL 由 `Room.PlayMove` 经 `IGameRules.IsInBounds` 判定,抛 `InvalidMoveException`,映射为 **409**。

这是相对本变更之前的一处**有意的状态码变更**:`(20, 20)` 这类坐标此前返回 400。改后更准确 —— 它是一个格式良好的请求,只是在五子棋里不合法而在假想的 21×21 棋种里合法,那属于"这一步在本局不合规"而非"请求有语法错"。Web 客户端只渲染实际存在的格子,因此触及不到这条路径。

#### Scenario: 负坐标仍是 400
- **WHEN** 提交 `row = -1`
- **THEN** 校验器拒绝,返回 400

#### Scenario: 超出棋种上界是 409
- **WHEN** 在五子棋房间提交 `row = 20`
- **THEN** `Room.PlayMove` 抛 `InvalidMoveException`,返回 409,`Move` 未被 append

### Requirement: 建房路径校验棋种已登记

`CreateRoomCommandValidator` 与 `CreateAiRoomCommandValidator` SHALL 各校验 `GameKey` 非空、且 MUST 能在 `IGameRulesRegistry` 中解析出规则,否则校验失败(映射为 HTTP 400)。

`CreateRoomCommandValidator` SHALL **额外**要求解析出的规则 `SupportsHumanVsHuman == true`,否则同样 400。`CreateAiRoomCommandValidator` MUST NOT 有这条规则 —— 人机正是这些棋种支持的玩法,在那条路径上拦住等于把它们逐出平台。

校验 MUST 发生在聚合被构造之前 —— 一个 `GameKey` 无人认识的 `Room` 一旦落库就再也玩不了,
只能靠手工改数据修复。

Validator MUST 通过注入的 `IGameRulesRegistry` 判断,MUST NOT 内联一份棋种白名单 ——
两处清单迟早会不一致,而不一致的那一天不会有人发现。同理,两条规则 MUST 各只有一处定义
(`Common/Validation` 下的 `IRuleBuilder` 扩展),由两条建房路径按需组合。

#### Scenario: 已登记且支持人人对战的键通过
- **WHEN** 以 `gameKey = "gomoku"` 建真人房
- **THEN** 校验通过

#### Scenario: 未登记的键被拒
- **WHEN** 以一个未在注册表中登记的 `gameKey`(如 `"go"`)建房
- **THEN** 校验失败,HTTP 400,错误信息点名该字段;真人房与 AI 房两条路径 MUST 表现一致

#### Scenario: 已登记但无人人对战的键在真人房路径被拒
- **WHEN** 以 `gameKey = "tictactoe"` 或 `"xiangqi"` 调 `POST /api/rooms`
- **THEN** 校验失败,HTTP 400 —— 该棋种 `SupportsHumanVsHuman == false`

#### Scenario: 同一个键在 AI 房路径通过
- **WHEN** 以 `gameKey = "tictactoe"` 或 `"xiangqi"` 调 `POST /api/rooms/ai`
- **THEN** 校验通过 —— 人机不受本规则约束

#### Scenario: 判定遍历注册表,不是一份名单
- **WHEN** 遍历 `IGameRulesRegistry` 中每一个规则,对其键跑 `CreateRoomCommandValidator`
- **THEN** 校验通过当且仅当该规则 `SupportsHumanVsHuman == true`;该遍历 MUST 另有一条断言证明它同时覆盖到了两类棋种(一个只走到空集合的遍历会全绿地什么都不验)

#### Scenario: 校验器不持有白名单
- **WHEN** 检视两个 validator 的实现
- **THEN** 它们 MUST 依赖 `IGameRulesRegistry`,MUST NOT 出现硬编码的棋种字符串集合

---

### Requirement: `GetRoomListQuery` 按棋种过滤

`GetRoomListQuery` SHALL 携带必填的 `GameKey`,handler MUST 只返回 `Room.GameKey` 与之相等的
活跃房间。

大厅是分棋种的:五子棋大厅里出现一字棋房间既无法加入(盘面不同),也让"有几局在等人"这个
数字失去意义。

`GET /api/users/me/active-rooms` MUST NOT 按棋种过滤 —— 它回答的是"我此刻在哪些局里",
跨棋种正是该问题的正确答案,也是玩家唯一希望它们混在一起的地方。

#### Scenario: 只返回本棋种
- **WHEN** 以 `GameKey = "gomoku"` 查询,库中同时存在两种棋的活跃房间
- **THEN** 只返回 `GameKey == "gomoku"` 的房间

#### Scenario: 我的活跃房间跨棋种
- **WHEN** 某用户同时在一个五子棋房间和一个一字棋房间里,调 `GET /api/users/me/active-rooms`
- **THEN** 两个房间都被返回

### Requirement: 房间 DTO 携带棋种键

`RoomStateDto` 与 `RoomSummaryDto` SHALL 各带一个非空的 `GameKey` 字段,取自 `Room.GameKey`。

这不是装饰性字段,而是客户端**画不出棋盘就得靠它**:玩家进入 `/rooms/{id}` 有四条路 ——
从建房页跳转、刷新页面、点收藏链接、从"我的对局"进入 —— 只有第一条路上客户端知道棋种
(是它自己刚选的)。另外三条它手上只有一个房间 id,而没有本字段时 DTO 里没有任何东西能
区分 3×3 与 15×15。所以"棋种从路由参数带过来"这条捷径只在四条路里的一条上成立。

映射 MUST NOT 因此获得新依赖:`Room.GameKey` 已经存在且已填好,`ToState` / `ToSummary`
就是把它映出来。

本变更 MUST NOT 在 DTO 里下发盘面尺寸(`Rows` / `Cols`)—— 那需要把 `IGameRulesRegistry`
穿过九处 `ToState` / `ToSummary` 调用点。见 `add-web-tictactoe-ai` design D1:客户端从自己的
游戏注册表解析尺寸,该重复此刻比它的替代方案便宜,且 `generalize-match-contract` 反正要
重写这两个 DTO,届时再改为服务端下发。

#### Scenario: 五子棋房间
- **WHEN** 读取任意 `gomoku` 房间的状态或摘要
- **THEN** `GameKey == "gomoku"`

#### Scenario: 一字棋房间
- **WHEN** 读取任意 `tictactoe` 房间的状态或摘要
- **THEN** `GameKey == "tictactoe"`

#### Scenario: 只增字段,不改既有字段
- **WHEN** 比对本变更前后的 DTO
- **THEN** 既有字段的名称、类型、顺序语义 MUST NOT 改变 —— 已发布客户端反序列化行为不变

### Requirement: `POST /api/rooms/ai` 接受棋种键

`POST /api/rooms/ai` 的请求体 SHALL 接受可选的 `gameKey`,缺省 `"gomoku"`,与 `POST /api/rooms` 的处理一致。

未登记的棋种 MUST 返回 400(由 `CreateAiRoomCommandValidator` 判定),与人人建房路径同一行为
—— 该棋种是否**有 AI** 则是另一件事,由落子时的 AI 注册表解析决定。

#### Scenario: 建一字棋 AI 房
- **WHEN** `POST /api/rooms/ai` 送 `{ name, difficulty: "Hard", humanSide: "Black", gameKey: "tictactoe" }`
- **THEN** 201 + `RoomStateDto`,`GameKey == "tictactoe"`,`Status == Playing`,白方是 `BotAccountIds.Hard`

#### Scenario: 缺省仍是五子棋
- **WHEN** 请求体不含 `gameKey`
- **THEN** 建出的房间 `GameKey == "gomoku"`

### Requirement: `Room.PlayMove` 校验回合与玩家身份，把盘面判定交给规则

`Room.PlayMove(UserId userId, MoveIntent intent, DateTime now, IGameRules rules)` SHALL 依次执行:

1. `Status != Playing` → 抛 `RoomNotInPlayException`
2. `userId` 不是黑 / 白方 → 抛 `NotAPlayerException`
3. 不是该方回合 → 抛 `NotYourTurnException`
4. 调 `rules.Apply(history, intent, side)` —— **越界、重复落子、走法合法性全部由规则回答**
5. 合法则 append 一条 `Move`(含可空起点)、切换回合
6. `Result != Ongoing` 则 `Game.FinishWith(result, winner, GameEndReason.Decided, now)` 并转 `Finished`

**聚合根 MUST NOT 再调 `rules.IsInBounds` / `rules.CreateBoard` / `Board.PlaceStone`。** 盘面语义
整个属于规则。这是象棋能进这个聚合的前提:它的一格上是七种棋子之一 × 两方,胜负是将死 / 困毙,
与最后一步的位置没有直接关系 —— 没有一条能塞进「连 N 子棋盘」。

签名从 `Position position` 改为 `MoveIntent intent`。落子类棋种的调用方传 `MoveIntent(null, to)`。

#### Scenario: 非玩家落子
- **WHEN** 一个围观者调 `PlayMove`
- **THEN** 抛 `NotAPlayerException`,MUST NOT 调 `rules.Apply`

#### Scenario: 不是自己的回合
- **WHEN** 白方在黑方回合调 `PlayMove`
- **THEN** 抛 `NotYourTurnException`,MUST NOT 调 `rules.Apply`

#### Scenario: 规则拒绝则聚合状态不变
- **WHEN** `rules.Apply` 抛 `InvalidMoveException`
- **THEN** `Game.Moves` 不增加、`CurrentTurn` 不变、`Status` 仍是 `Playing`

#### Scenario: 规则判出胜负则对局结束
- **WHEN** `rules.Apply` 返回 `BlackWin`
- **THEN** `Status == Finished`、`Game.Result == BlackWin`、`EndReason == Decided`、`WinnerUserId` 是黑方

### Requirement: 领域错误带稳定错误码,并以 `HubException` 送达客户端

每一个被 API 有意映射的领域异常 SHALL 继承 `DomainException` 并携带一个稳定的 kebab-case `Code`(如 `not-your-turn`、`invalid-move`、`self-check`、`idiom-not-found`)。

码 MAY 来自**具名静态工厂**而不是一个独立类型:一种拒绝需要自己的文案、却不值得为它多一个异常类型时,`InvalidMoveException.SelfCheck(...)` 那样的工厂是既定做法。成语接龙的三条规则各用一个(`idiom-not-found` / `idiom-does-not-link` / `idiom-already-used`)——「不是成语」「接不上」「说过了」是三种不同的纠正,一个码说不出任何一种。

码是这个错误的**身份**;消息仍然是给日志看的人类散文,MUST NOT 被客户端展示。

SignalR hub SHALL 通过一个过滤器把 `DomainException` 转成 `HubException(code)`,负载**只有码**。

**这不是整洁问题,是一个在生产环境里关掉了的功能。** 一个 hub 方法抛出普通异常时,它的消息只有在 `EnableDetailedErrors` 打开时才会送到客户端,而 `Program.cs` 把它设成 `IsDevelopment()`。因此在 Production 下 SignalR 会把消息换成一句通用文案,客户端此前基于**服务端英文散文**做的关键字匹配全部落空。

实测(同一次非法象棋着法、同一个构建、同一份数据库):

| 环境 | 玩家看到 |
| --- | --- |
| Development | 「That move isn't allowed.」 |
| **Production** | **「Something went wrong. Please try again.」** |

`HubException` 的消息**在两种环境下都会送达** —— 这正是这个类型存在的意义,也是为什么修法不是「在生产打开详细错误」(那会把栈和内部消息一起发给每个客户端)。

但它**不是原样送达**的。实测的线上帧在 `EnableDetailedErrors` 开与关时逐字节相同:

```
"An unexpected error occurred invoking 'MovePiece' on the server. HubException: invalid-move"
```

因此客户端 MUST **从这个包装里取出码**,而不是拿整串去比。规范把它写下来,是因为「消息原样送达」这个说法听起来对、实际不对,而它错了的表现是:服务端已经在发码了,界面上却仍然显示通用错误。

负载只放码而不附带消息,是为了让「展示服务端英文」这件事**做不到**,而不是靠自觉不做。原始异常连同消息 MUST 在服务端记录。

码 MUST 全局唯一。新增一个领域异常时,`DomainException` 的构造函数**强制**它给出一个码 —— 这与「维护一张需要记得扩充的表」不同,后者是纪律,前者是编译器。

#### Scenario: 领域异常在 hub 上变成码
- **WHEN** 一个 hub 方法内部抛出 `NotYourTurnException`
- **THEN** 客户端收到的错误串以 `HubException: not-your-turn` 结尾

#### Scenario: 生产环境送达同样的东西
- **WHEN** `EnableDetailedErrors` 为 false 时重复上一条
- **THEN** 收到的错误串与 Development 下**逐字节相同**

#### Scenario: 服务端英文不出现在负载里
- **WHEN** 抛出的异常带一句具体消息(如 `"A General cannot move from (9, 4) to (7, 4)."`)
- **THEN** 客户端收到的负载 MUST NOT 包含那句消息;它 MUST 出现在服务端日志里

#### Scenario: 非领域异常不被伪装成领域错误
- **WHEN** hub 方法内部抛出一个不继承 `DomainException` 的异常
- **THEN** 过滤器 MUST NOT 把它转成 `HubException`;它按既有方式处理(生产下客户端只得到通用错误)

#### Scenario: 码唯一
- **WHEN** 遍历所有 `DomainException` 子类**以及每一个返回该类型的 public static 工厂方法**
- **THEN** 它们的 `Code` 两两不同,且都非空

#### Scenario: 工厂产出的码也在遍历范围内
- **WHEN** 新增一个像 `SelfCheck` 那样的具名静态工厂,给它一个已被占用的码
- **THEN** 上一条 MUST 失败 —— 遍历只走类型时,`self-check` 从引入起就从未被自己的唯一性断言覆盖过,
  而多三个工厂就是把同一个洞扩大三倍

### Requirement: 一步棋要么是位置,要么是文本,不能既是又不是

`Move`、`MoveIntent`、`PlayedMove` SHALL 各携带两种互斥载荷之一:

- **位置类** —— `Row` / `Col`(终点,非空)加可选的 `FromRow` / `FromCol`(起点)。落子类棋种(五子棋 / 一字棋)没有起点;走子类(中国象棋)有。`FromRow` 与 `FromCol` MUST 同为 `null` 或同为非 `null` —— 半个坐标不是坐标。
- **文本类** —— `Text`(非空非空白),四个坐标列全为 `null`。成语接龙的一步是一个成语,它没有格子。

**恰好一种 MUST 被填充。** 两种都填、两种都不填,MUST 在构造时抛异常,MUST NOT 只写在文档里。这个不变量 MUST 由一条枚举非法组合的测试守着,而不是靠"只能从工厂函数构造"—— 工厂是约定,构造器检查是机制。

坐标列因此 MUST 可空。**MUST NOT 用 `Row = 0, Col = 0` 表示"这一步没有格子"** —— 那与本 spec 已经禁止的「用一个合法值表示没有起点」是同一件事,只是换了一个字段:读代码的人看到 `(0,0)` 得猜这是左上角还是不适用。

仍然 MUST NOT 改用 JSON 载荷列。理由未被本变更削弱:一个成语是**一个标量**,一列就装得下,而列仍然可查询、EF 原生映射、replay 仍是强类型的。JSON 会为一个还没有人提出的扩展性付钱。

#### Scenario: 落子类的起点为空
- **WHEN** 记录一步五子棋
- **THEN** `FromRow == null && FromCol == null`,`Row` / `Col` 非空,`Text == null`

#### Scenario: 走子类的起点非空
- **WHEN** 记录一步中国象棋
- **THEN** 四个坐标列都非 `null`,`Text == null`

#### Scenario: 文本类没有坐标
- **WHEN** 记录一步成语接龙
- **THEN** `Text` 非空,`FromRow` / `FromCol` / `Row` / `Col` 四列全为 `null`

#### Scenario: 两种载荷都给会被拒
- **WHEN** 构造一个同时带 `Text` 与 `Row`/`Col` 的 `MoveIntent` 或 `Move`
- **THEN** 构造 MUST 失败并抛异常

#### Scenario: 一种载荷都不给会被拒
- **WHEN** 构造一个既无 `Text` 也无 `Row`/`Col` 的 `MoveIntent` 或 `Move`
- **THEN** 构造 MUST 失败并抛异常

#### Scenario: 空白文本不算文本
- **WHEN** 以 `Text` 为 `""` 或 `"   "` 构造
- **THEN** 构造 MUST 失败 —— 一个空字符串不是一步棋

#### Scenario: 不变量由测试枚举,不由工厂保证
- **WHEN** 审阅这条不变量的测试
- **THEN** 它 MUST 直接构造非法组合,MUST NOT 只调用 `Place` / `Slide` / `Say` 三个工厂

#### Scenario: 迁移是加宽,不是回填
- **WHEN** 在含既有 `Moves` 行的库上跑迁移
- **THEN** 每行的 `Ply` / `Row` / `Col` / `Stone` 一字不变;新增的 `Text` 列为 `NULL`;`Row` / `Col` 由非空改为可空

#### Scenario: `Down` 遇到文本类记录必须失败
- **WHEN** 在已经存在文本类 `Move` 的库上回滚本迁移
- **THEN** 迁移 MUST 报错中止,MUST NOT 把那些行的 `Row` / `Col` 填 0 或把它们静默丢弃 —— 收窄一列而底下有装不进去的数据时,唯一诚实的动作是拒绝

### Requirement: `MakeMoveCommand` 携带它收到的那一种载荷

`MakeMoveCommand` SHALL 带 `int? Row`、`int? Col`、`int? FromRow`、`int? FromCol`、`string? Text`,与 `MoveIntent` / `Move` 在 `generalize-match-payload` 之后的形状一致。

Handler MUST 依据载荷选出**恰好一个** `MoveIntent` 工厂(`Place` / `Slide` / `Say`),MUST NOT 自己再实现一遍"恰好一种载荷"——那条不变量由 `MoveIntent` 的构造器强制,handler 拼错了会当场抛。

`MakeMoveCommandValidator` SHALL:

- 坐标**存在时**非负。上界仍属于棋种,理由不变:校验器跑在解析房间之前,那时还不知道这是哪一种棋。
- 文本**存在时**非空白。

这是"位置或文本"这个形状在本仓库的**第三处**编码(值对象、持久化实体、命令),而这是一次**有取舍的选择**,不是疏漏:更整洁的做法是让命令直接带 `MoveIntent`,那样编码就只剩一处。不这么做的具体理由是 `Position` 的构造器拒绝负坐标 —— 把 intent 的构造上移到 Hub,会把负坐标的拒绝从 `MakeMoveCommandValidator`(**400 + 点名字段**)挪到命令还不存在的时候抛出。那条错误路径被 `web-game-board` 与 `add-hub-error-codes` 两处钉着。改它是一个说得通的变更,在一个功能变更里顺手改掉不是。

#### Scenario: 落子类
- **WHEN** 命令带 `Row` / `Col`,不带 `FromRow` 也不带 `Text`
- **THEN** handler 用 `MoveIntent.Place`

#### Scenario: 走子类
- **WHEN** 命令另带 `FromRow` / `FromCol`
- **THEN** handler 用 `MoveIntent.Slide`

#### Scenario: 文本类
- **WHEN** 命令带 `Text`,四个坐标为 `null`
- **THEN** handler 用 `MoveIntent.Say`

#### Scenario: 负坐标仍是 400
- **WHEN** 命令带 `Row = -1`
- **THEN** 校验失败 —— 这条错误路径与本变更之前完全一致

#### Scenario: 空白文本被拒
- **WHEN** 命令带 `Text = "   "`
- **THEN** 校验失败

#### Scenario: 缺坐标的落子不被校验器放行成文本
- **WHEN** 命令四个坐标与 `Text` 全为 `null`
- **THEN** 请求 MUST 失败 —— 由 `MoveIntent` 的构造器兜住,handler MUST NOT 悄悄补一个默认值

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

