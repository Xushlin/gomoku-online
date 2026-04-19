## Context

三件事一起登场:**房间**(把玩家凑齐)、**对局**(进行中的棋盘状态)、**实时同步**(SignalR)。任一单独做都意义不大;合在一起正好构成"两人开始一盘棋"的 MVP。这也是项目里**第一次**出现 SignalR,它的接入模式(Hub 薄 / 事件推送走 `INotifier` 抽象 / 鉴权 / 重连)一次定型会直接影响后面所有实时 feature。

Domain 已有 `gomoku-domain`(Board / 判胜)和 `user-management`(User)。本次不触碰它们的任何 API,只是"消费"。

## Goals / Non-Goals

**Goals**

- 两个登录用户能创建房间 → 加入 → 轮流落子 → 判胜/平局 → 聊天 → 退出。全链路 HTTP + SignalR 打通,可端到端冒烟。
- Hub **只做路由**,业务在 Handler;Application 层对 SignalR 零感知(通过 `IRoomNotifier` 抽象解耦)。
- 领域层保持同步纯净:`Room` / `Game` 无 async、无外部依赖。
- 对局状态的服务端单一事实源:`Moves` 列表持久化,每次 handler 从 Moves 在内存 replay 得到最新 `Board`。
- 并发落子(双玩家同时点)由 EF 乐观并发 + 重试/拒绝保护,不会出现"两步都落上"。
- 聊天和围观者聊天频道隔离,催促(Urge)有冷却、仅在对方回合可用、仅推给对手。

**Non-Goals**

- ELO 结算、AI 对战、棋谱回放 UI / API —— 各自独立变更。
- 超时判负、认输、悔棋、重连后的"自动认输"规则 —— 留给 `add-timers-and-resign`。
- 房间密码 / 好友邀请 / 私密房间 —— 后续访问控制变更。
- 分页 / 搜索 / 排序的房间列表 —— 本次只做朴素全量返回(进行中的房间数量在早期实践中是小两位数,不是瓶颈)。
- 国际化 / 多语言聊天过滤 —— 未做。
- 不做 HTTP 级别的 rate limit(SignalR 连接限制默认即可)—— 留给未来的反滥用变更。

## Decisions

### D1. 能力划分:`room-and-gameplay` + `in-room-chat` 两个 spec

- 房间生命周期、对局推进、实时推送是一套规则(同一聚合内的事务),放一份 spec;
- 聊天(含催促)在 `Room` 聚合内但规则维度不同(频道可见性、催促冷却),单独 spec 便于未来演进(反滥用、离线消息、@ 提醒)不动主干。

### D2. 聚合边界:`Room` 是**唯一的**聚合根;`Game` / `Move` / `ChatMessage` 都是子实体

- 对"一场游戏"来说,加入 / 离开 / 落子 / 聊天全都是 `Room` 的事件,事务边界天然一致。
- 落子操作的领域入口是 `Room.PlayMove(UserId, Position, DateTime now)` —— 由 `Room` 校验身份、回合、状态,再转给内部的 `Game` 调用 `Board.PlaceStone(...)` 和 append `Move`。
- `Game` 不独立存活:房间销毁 → 游戏跟着没。
- 每个 `Room` 同时只有**一个**活跃 `Game`(这次的规则;未来"最好三局两胜"再开聚合)。

### D3. **内存中的 `Board` 每次 replay 从 `Moves` 构建**,`Game` 表不存盘面二进制

- 每次 handler 读聚合:`repo.FindByIdAsync(roomId)` 带 `Include(g => g.Moves)` → 在 Domain 层内存里按顺序调 `Board.PlaceStone` replay。最多 225 步,O(1) 判胜,总耗时 < 1 ms。
- **为什么不冗余存 `BoardState` 列**:
  1. Moves 已是真相,再存一次违反 "single source of truth",两者不一致谁对?
  2. `add-game-record` 未来做回放功能时本来就要按 Moves 逐步推进,复用同一入口。
  3. 不增加列宽度。225 × 4-bit 可以压到 128 字节,但代价是每次写两个地方。不值得。
- **代价**:handler 内存里要 new Board + replay。对 SignalR 的毫秒级要求无感。

### D4. 回合与落子流程

- 黑方先手(五子棋通例)。
- `Game` 持有 `CurrentTurn: Stone`,每次成功落子翻转。
- `Room.PlayMove(userId, position, now)` 流程(全部在 Domain 层同步方法):
  1. 校验 `Status == Playing` → 否则 `RoomNotInPlayException`
  2. 校验 `userId == BlackPlayerId` 或 `WhitePlayerId` → 否则 `NotAPlayerException`
  3. 校验 `userId` 对应的棋色 == `CurrentTurn` → 否则 `NotYourTurnException`
  4. 调 `Board.PlaceStone(new Move(position, currentTurn))` → 得到 `GameResult`
  5. Append `Move` 子实体(`Id`、`GameId`、`Ply`、`Position`、`Stone`、`PlayedAt`)
  6. `Game.CurrentTurn = opposite(currentTurn)`
  7. 若 `result` ≠ `Ongoing`:`Game.EndedAt = now`、`Game.Result = result`、`Game.WinnerId = ...`,`Room.Status = Finished`
  8. 返回 `(MoveRecorded, GameResult)` —— handler 用它决定要发哪些事件

### D5. **乐观并发**防止并发落子踩踏

- `Game` 表加 `RowVersion: byte[]`(SQLite 上 EF Core 8+ 支持 `IsRowVersion()`;SQLite 底层存 BLOB,EF 在应用层 maintain)。并发冲突 → EF 抛 `DbUpdateConcurrencyException`,中间件映射为 HTTP 409 + ProblemDetails `type: "concurrent-move"`,客户端**拉最新 RoomState 后重试或放弃**。
- 备选:应用层 per-room `SemaphoreSlim`。否决 —— 多实例部署下不生效,且与"状态在 DB"的本意冲突。

### D6. **掉线不影响对局**,SignalR 只管"是否推得到"

- `Room` 聚合**不记录连接状态**;`Room.Status` 只有 Waiting / Playing / Finished(以及后续 Abandoned)。
- SignalR 连接 ↔ `RoomId` 的映射存在 **Hub 进程内存的 `ConcurrentDictionary`** 中(见 D12)。进程重启即全部丢失 —— 客户端需要重连后显式 `JoinRoom` 重新挂到 group,这对玩家是"我又点了一下进入"。
- 好处:Hub 是**无状态可伸缩**的(将来用 Redis backplane 水平扩展时不用改 Hub 逻辑,只换一个 `IConnectionTracker` 实现);业务库里不会有"假在线"记录。

### D7. **`IRoomNotifier` 抽象:Application 不知道 SignalR 存在**

- Application 层定义接口,方法按事件一一对应:
  ```
  Task RoomStateChangedAsync(RoomId roomId, RoomStateDto state);
  Task PlayerJoinedAsync(RoomId roomId, UserSummaryDto payload);
  Task PlayerLeftAsync(RoomId roomId, UserSummaryDto payload);
  Task SpectatorJoinedAsync(RoomId roomId, UserSummaryDto payload);
  Task SpectatorLeftAsync(RoomId roomId, UserSummaryDto payload);
  Task MoveMadeAsync(RoomId roomId, MoveDto move);
  Task GameEndedAsync(RoomId roomId, GameEndedDto payload);
  Task ChatMessagePostedAsync(RoomId roomId, ChatChannel channel, ChatMessageDto msg);
  Task OpponentUrgedAsync(RoomId roomId, UserId urgedUser, UrgeDto payload);
  ```
- Handler 在**成功 `SaveChanges` 之后**调用 `IRoomNotifier`(在 DB 事务外,避免事件误发)。
- Api 层实现 `SignalRRoomNotifier`:内部用 `IHubContext<GomokuHub>`,按 roomId 构造 group 名 `room:{roomId}`,调 `Clients.Group(...).SendAsync(eventName, payload)`。**围观者聊天**推到子群 `room:{roomId}:spectators`。
- **备选**:用 MediatR `INotification` + `INotificationHandler` 在 Api 层订阅。否决 —— 再多一跳,且 handler 发布多个 `INotification` 时顺序和事务语义不清晰;直接调 `IRoomNotifier` 更直观。保留后续若需要"事件总线 / 审计"再加一层。

### D8. **SignalR 鉴权**:JWT 通过 query string 取

- SignalR 的 WebSocket 握手不允许自定义 header。按 ASP.NET Core 官方做法,在 `AddJwtBearer` 的 `Events.OnMessageReceived` 里:
  ```csharp
  var accessToken = ctx.Request.Query["access_token"];
  if (!string.IsNullOrEmpty(accessToken) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
      ctx.Token = accessToken;
  ```
- Hub 类上加 `[Authorize]`,`Context.UserIdentifier` 直接是 JWT `sub`(因为已经 `DefaultMapInboundClaims = false`)。
- 客户端连接时:`new HubConnectionBuilder().WithUrl("/hubs/gomoku?access_token=" + accessToken).Build()`。

### D9. **Hub 客户端 / 服务端方法命名与 payload 形态**

- 客户端可调 Hub 方法(都需 `[Authorize]`,参数最小化,返回 void 或 ack):
  - `JoinRoom(Guid roomId)` —— 加入 SignalR group(不改 `Room` 聚合 —— 聚合成员关系由 REST `POST /join` 管理)
  - `LeaveRoom(Guid roomId)` —— 离开 group;不改聚合
  - `MakeMove(Guid roomId, int row, int col)` —— 派 `MakeMoveCommand`
  - `SendChat(Guid roomId, string content, ChatChannel channel)` —— 派 `SendChatMessageCommand`
  - `Urge(Guid roomId)` —— 派 `UrgeOpponentCommand`
- 服务端推送事件(统一 camelCase,payload 都是 record):
  - `RoomState`:完整 `RoomStateDto`(join 后或状态机变化后)
  - `PlayerJoined` / `PlayerLeft` / `SpectatorJoined` / `SpectatorLeft`:`{ userId, username }`
  - `MoveMade`:`{ ply, row, col, stone, playedAt }`
  - `GameEnded`:`{ result, winnerUserId? }`
  - `ChatMessage`:`{ senderId, senderUsername, content, channel, sentAt, isUrge }`
  - `UrgeReceived`:`{ fromUserId, fromUsername, sentAt }` —— 仅推给被催的那一方
- **"REST 管关系、Hub 管实时"**:`JoinRoom` / `LeaveRoom` 在聚合层面由 REST 负责(产生审计 + 事务),Hub 的 `JoinRoom(groupId)` 只是"把这根 connection 加入 SignalR group 以便收推送"。这样断网重连不会产生重复的"PlayerJoined"事件,也便于未来加"重连时拉满增量"。

### D10. **催促(Urge)规则**

- 只有**两个玩家**可以触发;围观者禁用。
- 仅当轮到对手下棋时可调(`CurrentTurn` 是对手色)。否则 `NotOpponentsTurnException` → 400。
- **冷却 30 秒**:`Room.LastUrgeAt` 时间戳,30 秒内再次催促 → `UrgeTooFrequentException` → 429。
- 催促消息**仅推给被催方**,不广播给所有人 —— 观众看不到催促记录。
- 催促不写入 `ChatMessages` 表(非持久聊天),只产生 `UrgeReceived` 事件。

### D11. **聊天频道**:`Room` / `Spectator`

- `ChatMessage.Channel: enum { Room, Spectator }`。
- `Room` 频道:玩家和围观者都可发、都能收。
- `Spectator` 频道:**只围观者**可发、**只围观者**可收(玩家听不到"观众席的吐槽")。Hub 推送时,围观者在 `room:{roomId}:spectators` 子群中。
- 两种频道都持久化(便于回溯),但 `Room.Status == Finished` 且超过 30 分钟的房间会被后续的清理作业删除(**本次先不实现清理**,在 design 里记下依赖)。
- 消息长度 ≤ 500 字符,trim 后非空。FluentValidation 做这层。
- 速率:同一用户在同一房间 **5 条/10 秒** 的滚动窗口限制(`Room.RecentMessages` 字段不合适 —— 放在应用层的 in-memory `IMemoryCache` 即可;本次是可选的,若 design review 觉得必要再加,**默认不做**)。

### D12. **连接跟踪**:Hub 内一个简单 `IConnectionTracker`

- 一个单例 `ConnectionTracker : IConnectionTracker`,内部 `ConcurrentDictionary<string connectionId, (UserId, HashSet<RoomId>)>` 与 `ConcurrentDictionary<RoomId, HashSet<string connectionId>>`。
- Hub 的 `OnConnectedAsync` / `OnDisconnectedAsync` 调 `Track` / `Untrack`。
- 接口抽象让未来接 Redis backplane 时只换实现:
  ```csharp
  public interface IConnectionTracker {
      ValueTask TrackAsync(string connectionId, UserId userId);
      ValueTask UntrackAsync(string connectionId);
      ValueTask AssociateRoomAsync(string connectionId, RoomId roomId);
      ValueTask DissociateRoomAsync(string connectionId, RoomId roomId);
  }
  ```
- **不**把连接 ↔ 玩家 `userId` 的映射当作真相 —— 真相在 DB 的 `Room.PlayerIds` / `SpectatorIds`。Tracker 只是"谁的 connection 需要收消息"的索引。

### D13. **数据模型与 EF 映射**

- `Rooms` 表:`Id: Guid`、`Name: string(50)`、`HostUserId: Guid`、`BlackPlayerId: Guid?`、`WhitePlayerId: Guid?`、`Status: int (enum)`、`CreatedAt: DateTime`、`LastUrgeAt: DateTime?`、`LastUrgeByUserId: Guid?`。
- `Games` 表:`Id`、`RoomId (FK)`、`StartedAt`、`EndedAt?`、`Result: int? (GameResult)`、`WinnerUserId: Guid?`、`CurrentTurn: int (Stone)`、`RowVersion: byte[]`。`RoomId` 唯一索引(1:1)。
- `Moves` 表:`Id`、`GameId (FK)`、`Ply: int`、`Row: int`、`Col: int`、`Stone: int`、`PlayedAt: DateTime`。`(GameId, Ply)` 唯一,`Ply` 从 1 递增。
- `ChatMessages` 表:`Id`、`RoomId (FK)`、`SenderUserId: Guid`、`Content: string(500)`、`Channel: int (enum)`、`SentAt: DateTime`。
- `Spectators` 关系:`Room` 上的多对多 `SpectatorUserIds` —— 但不直接查 `User`,只存 id。实现为**联结表** `RoomSpectators(RoomId, UserId, JoinedAt)`,primary key `(RoomId, UserId)`,无级联到 Users(用户退房间清联结行即可)。
- 外键 `BlackPlayerId` / `WhitePlayerId` / `HostUserId` / `WinnerUserId`:不在 DB 层建 FK(跨聚合),只在代码层保证。避免 User 被"锁"在活跃对局里无法删除(未来删除用户是另一个话题)。

### D14. **房间生命周期状态机**

- `Waiting`:创建后 / 玩家未满。
- `Playing`:两个玩家就位(第二个玩家通过 `POST /join` 加入后立刻切换,并 init `Game`)。
- `Finished`:`Game.EndedAt != null`(某方胜 / 平局)。
- `Abandoned`:**本次不进入此状态**。留给后续"离开惩罚 / 超时"变更使用。
- 合法转换:Waiting → Playing → Finished,单向。任何其他方向的写都会抛 `InvalidRoomStatusTransitionException`。

### D15. **Application 层 fan-out 事件的顺序**

每次 `MakeMoveCommand` 成功:
1. `SaveChangesAsync`
2. `RoomStateChangedAsync(roomId, stateDto)` —— 让所有订阅者拿到权威状态(防事件丢失后不一致)
3. `MoveMadeAsync(roomId, moveDto)` —— 给前端"新这一步"以便动画
4. 若 `Game.Status == Finished`:`GameEndedAsync(roomId, payload)`

客户端该优先依赖 `RoomState` 做状态对齐,`MoveMade` 用于 UI 动画增量,二者短期内可能重复但信息一致。

### D16. **配置 & 可调参数**

`appsettings.json` 新增:
```json
"Rooms": {
  "MaxRoomNameLength": 50,
  "UrgeCooldownSeconds": 30,
  "MaxChatContentLength": 500,
  "FinishedRoomRetentionMinutes": 30
}
```
全部有代码默认值,配置节可缺。常量用 `IOptions<RoomsOptions>` 注入 Domain 层 **禁止**直接读(Domain 接收纯常量参数)。

### D17. **Serilog**?

CLAUDE.md 里提到用 Serilog 做日志,但 `add-user-authentication` 没引入(只用默认 `ILogger`)。本次**也不引入 Serilog 包**(避免扩大本次变更面),保留 `Microsoft.Extensions.Logging`。Serilog 放独立变更 `add-structured-logging`,届时调整 `Program.cs` 一处。design 里明记一下,避免 reviewer 以为漏了。

### D18. **围观者离开对称广播 `SpectatorLeft` 事件**

- `LeaveAsSpectatorCommandHandler` 成功后 MUST 调 `IRoomNotifier.SpectatorLeftAsync(roomId, userDto)` + `RoomStateChangedAsync(roomId, stateDto)`,与 `SpectatorJoinedAsync` 对称。
- `IRoomNotifier` 契约保留 `SpectatorLeftAsync(RoomId, UserSummaryDto)`,实现方向 `room:{roomId}` group 推送(玩家与其他围观者都能收到)。
- **为什么对称**:本项目当前规模(单房间预计 ≤ 20 观众)下,事件噪音可以忽略;前端实现"一个 `x 加入 / y 离开"的简洁 toast 体验会更好;对称模型对前端状态管理最简单(一套 `user-joined/left` 抽象)。
- **将来何时重新评估**:当某个房间观众数量进入三位数且事件频率明显影响前端/客户端带宽时,考虑改为"按节流合并广播"或"只推 `RoomStateChanged` 的观众计数增量"。届时不影响 `SpectatorJoinedAsync` 的客户端 API,只在 notifier 实现里做策略即可。

### D19. **`ChatMessage.SenderUsername` 入库时 snapshot,用户改名后历史消息保留旧名**

- Handler 在构造 `ChatMessage` 前查一次发送者的 `User.Username`,写入 `ChatMessage.SenderUsername` 字段入库。
- 查询聊天历史时直接读 `SenderUsername`,不 join `Users` 表。
- 好处:减少查询、改名不改历史、审计友好。
- 代价:若用户改名,老消息上显示旧名。这对五子棋对局聊天是可接受的;若未来做"全局消息"场景再考虑同步策略。

### D20. **`GetRoomState` 返回该局完整 Moves 列表**

- 单局 Moves 最大 225 条(15×15 满盘),按 `(Ply, Row, Col, Stone, PlayedAt)` 投影到 `MoveDto[]`,完整体量在 10–20 KB 级别 —— 对 JSON 载荷不是问题。
- 不做分页、不做切片。前端一次拿完,本地 replay 即可画出当前局面。
- 未来做"棋谱回放" / "历史对局查看"等独立变更时,那时的查询再按需切片(如按 Ply 范围查询、按时间戳过滤),与本次 `GetRoomState` 的契约不冲突。

## Risks / Trade-offs

- **SignalR 连接状态进程内内存** → 多实例部署无法感知对方。Mitigation:当前单体部署;未来水平扩展时换 `IConnectionTracker` 实现 + Redis backplane(SignalR 官方支持)。本次刻意不做,避免无意义的抽象。
- **乐观并发冲突返回 409 需要客户端正确处理** → 前端(Angular)目前还没开始,未来要在客户端实现"拉状态 → 对比 → 重试"。本次把 Hub / REST 的行为写进 spec,前端实现时按 spec 来。
- **REST 管关系 + Hub 管实时 是两条 API** → 客户端操作"加入房间"需要先 `POST /api/rooms/{id}/join`(更新聚合),再 `JoinRoom(roomId)`(接入 group)。两步分开看似不优雅,但比"Hub 里同时管事务与事件"要可控。在 HOW_TO_RUN 里写清楚。
- **聊天无审核 / 无 rate-limit** → 本次不做反滥用;未来一个变更加 token bucket + 敏感词过滤。
- **Moves replay 成本随盘数线性** → 单盘最多 225 步,微秒级。非瓶颈。如果未来单聚合进化为"三局两胜" / 载入过多历史,再加快照。
- **`DbUpdateConcurrencyException` 映射** → 中间件需要识别此异常并返回 409 + ProblemDetails。在本次中间件加一条 switch 分支,非技术债。
- **同一房间被玩家 A 加入后再调 `POST /leave` 会怎样** → 若对局未开始,`Room.Status` 回到 `Waiting`;若对局中,**按本次规则**玩家可"离席",但 `Game` 仍保留、`Status` 仍为 `Playing`(等对手 / 超时机制),SignalR 推送 `PlayerLeft`。具体行为在 spec 里写清。认输 / 超时判负由后续变更补。

## Migration Plan

零运行数据。按 tasks 顺序提交:Domain → Application → Infrastructure(migration 放这里)→ Api。每层可独立 build + 测自己。Api 层提交后做一次端到端冒烟:两个浏览器标签分别登录 Alice / Bob,Alice 创建房间,Bob `POST /join` → 双方 `SignalR JoinRoom` → 轮流 `MakeMove` 直到一方连五 → `GameEnded` 推送。回滚策略同上个变更:任一层被驳回,独立 revert 其 commit;DB 删除重来。

## Open Questions

(无 —— 提案讨论的三个悬置问题已落为 D18 / D19 / D20。)
