## Why

`gomoku-domain` 有棋盘和判胜,`user-management` 有用户身份,但**两者还没有被连起来**:没有"谁和谁在哪下棋"的场景。这个变更把两张地图拼上,引入 **房间 + 对局 + 实时同步**,一次把项目从"可登录的空壳"推到"两个人能打一局棋"。

同时顺带为所有后续实时功能(观战、聊天、催促、未来的 AI 对战广播、排行榜推送)铺好 **SignalR + CQRS + 领域事件** 的主干:Hub 只路由,业务逻辑进 Handler,Handler 发布事件由 Api 层投递到客户端。这套模式一次定型,后面每个实时功能就是"加一个 Command/Handler + 一个事件通知"。

## What Changes

- **Domain**:新增 `RoomId` 值对象、`Room` 聚合根(承载玩家、围观者、游戏状态、聊天、催促记录)、`Game` 子实体(包裹 `Board` + `Moves` 列表 + `CurrentTurn`)、`Move` 与 `ChatMessage` 子实体、`RoomStatus` 枚举,以及一组领域异常(`RoomFullException` / `NotYourTurnException` / 等)。
- **Application**:
  - 新 features(命令/查询):`CreateRoom` / `JoinRoom` / `LeaveRoom` / `JoinAsSpectator` / `LeaveAsSpectator` / `MakeMove` / `SendChatMessage` / `UrgeOpponent` / `GetRoomList` / `GetRoomState`。
  - 新抽象:`IRoomRepository`、`IRoomNotifier`(事件广播契约,Api 层实现)。
  - 新 DTO:`RoomSummaryDto` / `RoomStateDto` / `MoveDto` / `ChatMessageDto`。
  - 新**领域/应用事件**:`RoomCreated` / `PlayerJoined` / `PlayerLeft` / `SpectatorJoined` / `SpectatorLeft` / `MoveMade` / `GameEnded` / `ChatMessagePosted` / `OpponentUrged` —— 以 MediatR `INotification` 表达。
- **Infrastructure**:
  - 新 EF Core 配置与新 migration `AddRoomsAndGameplay`:`Rooms`、`Games`、`Moves`、`ChatMessages`。
  - `RoomRepository` 实现 `IRoomRepository`;所有"读一局棋"场景连 `Include(Moves)` 以便内存 replay。
  - `Game` 加乐观并发列(rowversion)防止双玩家并发落子踩踏。
- **Api**:
  - 新 REST:`POST /api/rooms`、`GET /api/rooms`、`GET /api/rooms/{id}`、`POST /api/rooms/{id}/join`、`POST /api/rooms/{id}/leave`、`POST /api/rooms/{id}/spectate`、`DELETE /api/rooms/{id}/spectate`。**落子 / 聊天 / 催促** 走 SignalR,不走 REST。
  - 新 SignalR Hub `GomokuHub`(挂在 `/hubs/gomoku`):客户端方法 `JoinRoom` / `LeaveRoom` / `MakeMove` / `SendChat` / `Urge`;服务端推送 `RoomState` / `MoveMade` / `GameEnded` / `ChatMessage` / `UrgeReceived` / `PlayerJoined` / `PlayerLeft` / `SpectatorJoined` / `SpectatorLeft`。
  - Hub 只做"鉴权 + 路由到 MediatR + 连接 ↔ 房间映射";一切业务逻辑在 Handler 里。
  - `SignalRRoomNotifier` 实现 `IRoomNotifier`(Api 层),用 `IHubContext<GomokuHub>` 往 SignalR group 推事件。`Application.Events.*` 通过 MediatR `INotificationHandler` 订阅后再委托给 `IRoomNotifier`,保持 Application 对 SignalR 无感知。
  - JWT Bearer 扩展到 SignalR:`Events.OnMessageReceived` 从 `access_token` query 取 token(SignalR WebSocket 无法带自定义 header)。
- **Tests**:
  - `Gomoku.Domain.Tests/Rooms/` 覆盖 `Room` / `Game` / `Move` / `ChatMessage` 的不变量与状态机转换(创建 → 等待 → 进行 → 结束),回合推进,催促冷却,围观者不得落子等。
  - `Gomoku.Application.Tests/Features/Rooms/` 覆盖每个 Handler 的成功路径与错误路径(Moq 仓储和 `IRoomNotifier`,断言事件被发布,断言仓储/UoW 交互)。
  - Infrastructure 的集成测试仍**不做**,继续留给 `add-integration-tests`。

**显式不做**(后续变更):
- **ELO 积分**与对局结果联动(战绩字段 `User.Rating` / `Wins` 等暂不改动);留给 `add-elo-system`,该变更只需在 `GameEnded` 事件上挂新 handler。
- **AI 对战**:留给 `add-ai-opponent`。
- **棋谱回放**独立 UI / API:本次 `Moves` 已完整持久化,足够下次做回放变更读取,但不包含"回放控制"接口。
- **掉线即判负 / 超时判负 / 认输**:本次掉线仅视为 SignalR 连接断开,不影响 `Game` 状态;对手可以继续落子,断线方重连后拉取最新 `RoomState` 即可。超时 / 认输留给 `add-timers-and-resign`。
- **禁手规则**:继续由 `gomoku-domain` 承载"长连即赢",未变。
- **房间密码 / 好友邀请 / 私密房间**:全部公开房间,后续变更再细化访问控制。
- **观战回看历史**:房间在 `Finished` 状态下**保留 30 分钟**以延续聊天,期间可查看终局;之后清除 —— 历史库由未来的棋谱功能承载。

## Capabilities

### New Capabilities

- `room-and-gameplay`:房间生命周期(创建 / 加入 / 离开 / 围观 / 解散)、对局推进(轮次、落子、判胜、结束)、所有相关实时推送的规则与不变量。**落子合法性**继续由 `gomoku-domain` 的 `Board` 决定,本能力把"**谁的回合 + 是不是玩家 + 房间状态对不对**"这几层守门员建立起来。
- `in-room-chat`:房间聊天 / 围观者聊天两种频道的定义,消息可见性规则,催促(Urge)行为及其冷却。

### Modified Capabilities

(无 —— `gomoku-domain` / `user-management` / `authentication` 本次 **零触碰**。)

## Impact

- **代码规模**:这是**全项目最大的一次变更**,预计新增 ~60 个源文件跨四层 + 两个测试项目。比 `add-user-authentication` 略大(多了 SignalR + 两个额外聚合 + 更多 handler)。
- **新增 NuGet**:
  - `Gomoku.Domain`:**零**(铁律)。
  - `Gomoku.Application`:无新增(MediatR / FluentValidation 已有)。
  - `Gomoku.Infrastructure`:无新增(EF Core 已在)。
  - `Gomoku.Api`:`Microsoft.AspNetCore.SignalR`(随 AspNetCore.App 自带,无需包引用)。若 Serilog 还未启用,本次顺带加 `Serilog.AspNetCore` + `Serilog.Sinks.Console`(design 里决策);否则跳过。
- **数据库**:一条新 migration,新增 `Rooms` / `Games` / `Moves` / `ChatMessages` 四张表。现有 `Users` / `RefreshTokens` 不变。
- **HTTP 表面**:+7 REST 端点 + 1 个 SignalR hub(5 客户端方法 + 9 服务端事件)。
- **配置**:`appsettings.json` 新增 `Rooms` 节(可选超时 / 催促冷却参数,默认值在代码里也有);CORS 配置在**本次不做**(前端还未开始)。
- **后续变更将依赖**:`add-elo-system` 挂 `GameEnded` handler;`add-ai-opponent` 注入"虚拟玩家"占位 `WhitePlayerId`;`add-spectator-ui` 消费 `SpectatorJoined` / `MoveMade` 事件;`add-timers-and-resign` 扩展 `Room` 聚合加计时器状态。
