## 1. Domain — 枚举、值对象、异常

- [x] 1.1 `backend/src/Gomoku.Domain/Rooms/` 建子目录。
- [x] 1.2 `Rooms/RoomId.cs`:`public readonly record struct RoomId(Guid Value)` + `NewId()` 工厂。
- [x] 1.3 `Rooms/RoomStatus.cs`:`public enum RoomStatus { Waiting=0, Playing=1, Finished=2 }` + XML 注释。
- [x] 1.4 `Rooms/ChatChannel.cs`:`public enum ChatChannel { Room=0, Spectator=1 }` + XML 注释。
- [x] 1.5 在 `Domain/Exceptions/` 新增 9 个领域异常(空构造消息由调用侧传):`InvalidRoomNameException`、`InvalidRoomStatusTransitionException`、`RoomNotWaitingException`、`RoomNotInPlayException`、`RoomFullException`、`AlreadyInRoomException`、`HostCannotLeaveWaitingRoomException`、`PlayerCannotSpectateException`、`NotInRoomException`、`NotSpectatingException`、`NotAPlayerException`、`NotYourTurnException`、`NotOpponentsTurnException`、`UrgeTooFrequentException`、`InvalidChatContentException`、`PlayerCannotPostSpectatorChannelException`。每个都带 `(string)` 与 `(string, Exception)` 构造。

## 2. Domain — Move / ChatMessage 子实体

- [x] 2.1 `Rooms/Move.cs`:字段 `Id:Guid`、`GameId:Guid`、`Ply:int`、`Row:int`、`Col:int`、`Stone:Stone`、`PlayedAt:DateTime`。私有无参构造(EF)+ `internal` 构造 `(Guid gameId, int ply, Position pos, Stone stone, DateTime playedAt)`。
- [x] 2.2 `Rooms/ChatMessage.cs`:字段 `Id:Guid`、`RoomId:RoomId`、`SenderUserId:UserId`、`SenderUsername:string`、`Content:string`、`Channel:ChatChannel`、`SentAt:DateTime`。`internal` 构造,EF 用的私有无参构造。

## 3. Domain — Game 子实体

- [x] 3.1 `Rooms/Game.cs`:字段 `Id:Guid`、`RoomId:RoomId`、`StartedAt`、`EndedAt?`、`Result:GameResult?`、`WinnerUserId:UserId?`、`CurrentTurn:Stone`、`Moves: IReadOnlyCollection<Move>`、`RowVersion:byte[]`。`internal` 构造 `(RoomId, DateTime startedAt)`,初始 `CurrentTurn = Black`。
- [x] 3.2 `Game.ReplayBoard()` 只读方法:`var board = new Board(); foreach move in Moves order by Ply { board.PlaceStone(new DomainMove(pos, stone)); } return board;`。用于 handler / 测试取当前盘面。
- [x] 3.3 `Game.RecordMove(Position, Stone, DateTime now)` `internal`:append `Move`、翻转 `CurrentTurn`、返回 `GameResult`(内部 replay 然后 `Board.PlaceStone` 判;也可在 `Room.PlayMove` 里做,`Game` 只做状态更新 —— 按 design D4 让 `Room.PlayMove` 统一协调)。**决定**:`Game` 不自己调 `Board`,只 record move 和 turn;落子合法性由 `Room.PlayMove` 在把这个方法调出前检查。
- [x] 3.4 `Game.FinishWith(GameResult, UserId? winnerId, DateTime endedAt)` `internal`:设置 `EndedAt` / `Result` / `WinnerUserId`。

## 4. Domain — Room 聚合根

- [x] 4.1 `Rooms/Room.cs`:字段私有集合 `_spectators: List<UserId>`、`_chatMessages: List<ChatMessage>`;公共属性 `Spectators` / `ChatMessages` 返回 `IReadOnlyCollection<>` 直接暴露 list(不 `AsReadOnly()`,让 EF 填充)。其他字段见 spec。
- [x] 4.2 `Room.Create(RoomId, string name, UserId host, DateTime createdAt)` 静态工厂:trim name,校验 3–50,非法抛 `InvalidRoomNameException`。设 `BlackPlayerId = host`,`Status = Waiting`,`Game = null`。
- [x] 4.3 `Room.JoinAsPlayer(UserId, DateTime now)`:按 spec 4 个分支;成功时 `WhitePlayerId = userId`、`Status = Playing`、`Game = new Game(Id, now)`。
- [x] 4.4 `Room.Leave(UserId, DateTime now)`:按 spec 分支(围观者 / 对局中玩家离席 / Waiting 时 Host 禁离 / 非成员);**不**改变 `Game`。
- [x] 4.5 `Room.JoinAsSpectator(UserId)` / `Room.LeaveAsSpectator(UserId)`:按 spec 规则(幂等 / 排除玩家 / 非围观者不能离开)。
- [x] 4.6 `Room.PlayMove(UserId, Position, DateTime now)`:按 spec 的 8 步,返回 `MoveOutcome` record `(Move appendedMove, GameResult result)`。内部 replay board → `Board.PlaceStone` → append move → flip turn → 如 result != Ongoing 调 `Game.FinishWith` 并 `Status = Finished`。
- [x] 4.7 `Room.PostChatMessage(UserId, string senderUsername, string rawContent, ChatChannel, DateTime now)`:按 spec 4 步,返回新 `ChatMessage`。
- [x] 4.8 `Room.UrgeOpponent(UserId, DateTime now, int cooldownSeconds)`:按 spec 5 步,返回 `UrgeOutcome record (UserId urgedUser)`。
- [x] 4.9 所有公共方法带 XML `<summary>`;`internal` 构造函数给 EF 用。检查 Domain 层仍然零 NuGet。

## 5. Domain 测试

- [x] 5.1 `Rooms/RoomIdTests.cs`:构造、值相等、`NewId` 不重复。
- [x] 5.2 `Rooms/RoomCreateTests.cs`:合法创建字段断言;名称非法(空 / 短 / 长)各 1 个 test。
- [x] 5.3 `Rooms/RoomJoinLeaveTests.cs`:第二位成功加入、重复加入、房间满、围观者升级为玩家、围观者离开、对局中玩家离席不改 Game、Waiting 时 Host 不能离开、非成员离开、成为围观者、玩家禁止围观。
- [x] 5.4 `Rooms/RoomPlayMoveTests.cs`:合法落子、非 Playing 抛 `RoomNotInPlayException`、非玩家落子、轮次错误、棋盘越界冒泡 `InvalidMoveException`、连五后 `Status=Finished` 且 `WinnerUserId` 正确、平局场景(reuse gomoku-domain 平局 pattern)、`Game.Moves` 的 `Ply` 严格递增。
- [x] 5.5 `Rooms/RoomChatTests.cs`:成员发房间频道成功、围观者发围观频道成功、玩家禁发围观频道、非成员发消息、空 / 超长 content、`SenderUsername` 被 snapshot。
- [x] 5.6 `Rooms/RoomUrgeTests.cs`:正常催促(对手回合)、自己的回合抛 `NotOpponentsTurnException`、冷却内抛 `UrgeTooFrequentException`、`Status != Playing` 抛 `RoomNotInPlayException`、围观者催促抛 `NotAPlayerException`。
- [x] 5.7 `Rooms/GameReplayTests.cs`:空 Moves 的 `ReplayBoard()` 返回空 Board;按一组合法 Moves replay 后 `board.GetStone(pos)` 正确。
- [x] 5.8 全部测试运行通过:`dotnet test tests/Gomoku.Domain.Tests`。

## 6. Application — 抽象契约

- [x] 6.1 `Abstractions/IRoomRepository.cs`:`FindByIdAsync(RoomId, CT)`、`GetActiveRoomsAsync(CT)`、`AddAsync(Room, CT)`。签名只含领域类型。
- [x] 6.2 `Abstractions/IRoomNotifier.cs`:spec 中列出的 8 个方法,返回 `Task`。参数用 DTO(下面定义)。
- [x] 6.3 `Abstractions/IConnectionTracker.cs`:spec 未强制,但 design D12 提到;为了 Application 能让 Hub 调用(其实只有 Api 需要),把它放在 **Infrastructure** 或 **Api**?**决定**:放 Api 层(纯 Hub 辅助,不跨层)。此任务移到 Task 16.

## 7. Application — DTO

- [x] 7.1 `Common/DTOs/UserSummaryDto.cs`:`(Guid Id, string Username)`。
- [x] 7.2 `Common/DTOs/MoveDto.cs`:`(int Ply, int Row, int Col, Stone Stone, DateTime PlayedAt)`。
- [x] 7.3 `Common/DTOs/GameEndedDto.cs`:`(GameResult Result, Guid? WinnerUserId, DateTime EndedAt)`。
- [x] 7.4 `Common/DTOs/RoomSummaryDto.cs`:`(Guid Id, string Name, RoomStatus Status, UserSummaryDto Host, UserSummaryDto? Black, UserSummaryDto? White, int SpectatorCount, DateTime CreatedAt)`。
- [x] 7.5 `Common/DTOs/RoomStateDto.cs`:完整状态 `(Guid Id, string Name, RoomStatus Status, UserSummaryDto Host, UserSummaryDto? Black, UserSummaryDto? White, IReadOnlyList<UserSummaryDto> Spectators, GameSnapshotDto? Game, IReadOnlyList<ChatMessageDto> ChatMessages, DateTime CreatedAt)`。
- [x] 7.6 `Common/DTOs/GameSnapshotDto.cs`:`(Guid Id, Stone CurrentTurn, DateTime StartedAt, DateTime? EndedAt, GameResult? Result, Guid? WinnerUserId, IReadOnlyList<MoveDto> Moves)`。
- [x] 7.7 `Common/DTOs/ChatMessageDto.cs`:`(Guid Id, Guid SenderUserId, string SenderUsername, string Content, ChatChannel Channel, DateTime SentAt)`。
- [x] 7.8 `Common/DTOs/UrgeDto.cs`:`(Guid FromUserId, string FromUsername, DateTime SentAt)`。
- [x] 7.9 `Common/Mapping/RoomMapping.cs`:`Room → RoomSummaryDto` / `Room → RoomStateDto` 扩展方法(需要一个 `IUserLookup` 或已 populated 的 User snapshot —— 为了避免 N+1,handler 先批量查 `User` by ids,map 时传入 dict)。

## 8. Application — Features: Room lifecycle

- [x] 8.1 `Features/Rooms/CreateRoom/CreateRoomCommand.cs`:`(string Name) : IRequest<RoomSummaryDto>`;实际 `UserId` 从 Controller 解出 JWT sub 后**在 Hub/Controller 里放进 command**(增加 `UserId HostUserId` 字段)或通过 ambient abstraction。**决定**:command 显式带 `HostUserId`。
- [x] 8.2 `CreateRoomCommandValidator.cs`:`Name` trim 后 3–50。
- [x] 8.3 `CreateRoomCommandHandler.cs`:`Room.Create` → `repo.AddAsync` → `SaveChanges` → 调 `IUserRepository.FindByIdAsync(Host)` 取 UserSummary → 组装 `RoomSummaryDto` 返回。
- [x] 8.4 `Features/Rooms/JoinRoom/JoinRoomCommand.cs`:`(UserId, RoomId) : IRequest<RoomStateDto>`。Handler:`FindByIdAsync` → `RoomNotFoundException` 或 `Room.JoinAsPlayer(userId, now)` → 查 Users(host/black/white)并组装 `RoomStateDto` → `SaveChanges` → `IRoomNotifier.RoomStateChangedAsync` + `PlayerJoinedAsync`。
- [x] 8.5 `Features/Rooms/LeaveRoom/LeaveRoomCommand.cs` + Handler:`Room.Leave` → `SaveChanges` → 按情况调 `PlayerLeftAsync` / `SpectatorLeftAsync` + `RoomStateChangedAsync`。
- [x] 8.6 `Features/Rooms/JoinAsSpectator/JoinAsSpectatorCommand` + Handler:`Room.JoinAsSpectator` → `SaveChanges` → 调 `IRoomNotifier.SpectatorJoinedAsync` + `RoomStateChangedAsync`。
- [x] 8.6b `Features/Rooms/LeaveAsSpectator/LeaveAsSpectatorCommand` + Handler:`Room.LeaveAsSpectator` → `SaveChanges` → 调 `IRoomNotifier.SpectatorLeftAsync` + `RoomStateChangedAsync`(D18 对称广播)。
- [x] 8.7 `Features/Rooms/GetRoomList/GetRoomListQuery.cs` + Handler:返回 `IReadOnlyList<RoomSummaryDto>`,调 `GetActiveRoomsAsync`,批量查 Users,Map。
- [x] 8.8 `Features/Rooms/GetRoomState/GetRoomStateQuery.cs` + Handler:`FindByIdAsync` → 不存在抛 `RoomNotFoundException` → Map RoomStateDto(含完整 Moves)。

## 9. Application — Features: Gameplay

- [x] 9.1 `Features/Rooms/MakeMove/MakeMoveCommand.cs`:`(UserId, RoomId, int Row, int Col) : IRequest<MoveDto>`。
- [x] 9.2 `MakeMoveCommandValidator.cs`:`Row`、`Col` 在 `[0..14]`。(Position 构造再兜底)
- [x] 9.3 `MakeMoveCommandHandler.cs`:
  - `FindByIdAsync` → 不存在 404
  - `Room.PlayMove(userId, new Position(row, col), now)` → 返回 `MoveOutcome`
  - `SaveChangesAsync`(让 EF 乐观并发起作用)
  - 映射 `MoveDto` 并调 `IRoomNotifier.RoomStateChangedAsync` + `MoveMadeAsync`;若 `outcome.Result != Ongoing`:调 `GameEndedAsync`。
  - 返回 `MoveDto`。`DbUpdateConcurrencyException` 不 catch,让中间件映射 409。

## 10. Application — Features: Chat / Urge

- [x] 10.1 `Features/Rooms/SendChatMessage/SendChatMessageCommand.cs`:`(UserId, RoomId, string Content, ChatChannel Channel) : IRequest<ChatMessageDto>`。
- [x] 10.2 `SendChatMessageCommandValidator.cs`:`Content` 非空、trim 后 1–500;`Channel` 必须是枚举定义值。
- [x] 10.3 `SendChatMessageCommandHandler.cs`:`FindByIdAsync` → 查 sender User 取 username → `Room.PostChatMessage(...)` → `SaveChanges` → `IRoomNotifier.ChatMessagePostedAsync(roomId, channel, dto)`。
- [x] 10.4 `Features/Rooms/UrgeOpponent/UrgeOpponentCommand.cs`:`(UserId, RoomId) : IRequest<UrgeDto>`。
- [x] 10.5 `UrgeOpponentCommandHandler.cs`:注入 `IOptions<RoomsOptions>` 获取 `UrgeCooldownSeconds` → `Room.UrgeOpponent(senderId, now, cooldownSec)` → `SaveChanges` → 查 sender User → `IRoomNotifier.OpponentUrgedAsync(roomId, outcome.UrgedUser, dto)`。

## 11. Application — RoomsOptions & DI

- [x] 11.1 `Abstractions/RoomsOptions.cs`:`MaxRoomNameLength=50`、`UrgeCooldownSeconds=30`、`MaxChatContentLength=500`、`FinishedRoomRetentionMinutes=30`。
- [x] 11.2 在 `AddApplication()` 里通过 MediatR assembly 扫描自动注册新 handler(已通过现有逻辑);无需额外改动。
- [x] 11.3 确认 Application 仍 **不依赖** EF Core / Infrastructure;`csproj` 无变化。

## 12. Application 测试

- [x] 12.1 通用:新建 `AuthFixtures.cs` 辅助(上个变更已用 Moq 设置 handler 依赖,这次复用同样 pattern)。为 Rooms 建一个 `RoomBuilder` 测试辅助(快速构造已 JoinAsPlayer 完成的 Room)。
- [x] 12.2 `Features/Rooms/CreateRoom/CreateRoomCommandHandlerTests.cs`:成功创建返回 DTO、`AddAsync` + `SaveChangesAsync` 各调一次。
- [x] 12.3 `Features/Rooms/JoinRoom/JoinRoomCommandHandlerTests.cs`:成功加入 → 通知 `RoomStateChanged` + `PlayerJoined`;房间不存在 → 404;房间已满 → 409 等。
- [x] 12.4 `Features/Rooms/LeaveRoom/LeaveRoomCommandHandlerTests.cs`:玩家离席成功调 `PlayerLeftAsync` + `RoomStateChangedAsync`;围观者离开成功调 `SpectatorLeftAsync` + `RoomStateChangedAsync`。
- [x] 12.5 `Features/Rooms/JoinAsSpectator/...HandlerTests.cs`:成功调 `SpectatorJoinedAsync` + `RoomStateChangedAsync`。
- [x] 12.5b `Features/Rooms/LeaveAsSpectator/...HandlerTests.cs`:成功调 `SpectatorLeftAsync` + `RoomStateChangedAsync`。
- [x] 12.6 `Features/Rooms/MakeMove/MakeMoveCommandHandlerTests.cs`:
  - 成功落子(未胜) → 调了 `RoomStateChanged` + `MoveMade`,**未**调 `GameEnded`
  - 成功落子并胜 → 三个事件都调
  - 非 Playing / 非玩家 / 非己回合 → 抛领域异常,未调 notifier
  - 并发模拟:repo 第二次 `SaveChangesAsync` 抛 `DbUpdateConcurrencyException`(Mock) → handler 让其冒泡,未调 notifier
- [x] 12.7 `Features/Rooms/SendChatMessage/...HandlerTests.cs`:房间频道 / 围观频道成功;玩家发围观频道禁止;超长内容由 validator 拦下(另一 Validator 测试)。
- [x] 12.8 `Features/Rooms/UrgeOpponent/...HandlerTests.cs`:成功催促、冷却内、自己回合、非玩家、非 Playing。
- [x] 12.9 `Features/Rooms/GetRoomList/...HandlerTests.cs`:`GetActiveRoomsAsync` 返回 3 个 → DTO 数量 3。
- [x] 12.10 `Features/Rooms/GetRoomState/...HandlerTests.cs`:成功返回含 Moves 的完整 DTO。

## 13. Infrastructure — EF 映射

- [x] 13.1 `Persistence/Configurations/RoomConfiguration.cs`:PK `Id` 用 `RoomId` 转换(参考 `UserIdConverter`,新增 `RoomIdConverter`);`Name` `HasMaxLength(50)` IsRequired;`HostUserId` / `BlackPlayerId?` / `WhitePlayerId?` 用 `UserIdConverter` + 可空版本;`Status` int;`CreatedAt`;`LastUrgeAt?`;`LastUrgeByUserId?`。
- [x] 13.2 `Room.Spectators` 映射:用 `OwnsMany` 或单独表 `RoomSpectators`?**决定**:`OwnsMany`  不适合(Spectators 是 UserId 集合不是 owned entity)。改为**单独联结表** `RoomSpectators(RoomId, UserId, JoinedAt)`。写一个额外子实体 `RoomSpectator { RoomId, UserId, JoinedAt }`,但 Domain 里只暴露 `IReadOnlyCollection<UserId>` —— 通过 EF 的 `HasMany<RoomSpectator>` + Domain 层 `_spectators` 派生映射。**简化决定**:把 `Spectators` 直接映射成单独表里的 `UserId` 列表,不暴露连接表实体;用 EF Core 9+ 的 **primitive collection with conversion** 或 owned type。具体方式由实施者尝试。本任务细化:
  - 尝试 `builder.OwnsMany<Guid>("_spectators", c => c.ToTable("RoomSpectators"))`(可能需要改 `_spectators` 字段类型)
  - 若不行,引入小 `RoomSpectator` 内部实体,Domain 暴露投影 getter
- [x] 13.3 `Game` 一对一:`Room.Game`  `HasOne(r => r.Game).WithOne().HasForeignKey<Game>(g => g.RoomId)`。
- [x] 13.4 `Persistence/Configurations/GameConfiguration.cs`:`Id` PK,`RoomId` FK 唯一,`StartedAt/EndedAt?`,`Result?` int,`WinnerUserId?` UserIdConverter,`CurrentTurn` int,`RowVersion` `.IsRowVersion()`。
- [x] 13.5 `Persistence/Configurations/MoveConfiguration.cs`:PK,`GameId` FK,`Ply`,`Row/Col`,`Stone` int,`PlayedAt`;`(GameId, Ply)` 唯一索引。
- [x] 13.6 `Persistence/Configurations/ChatMessageConfiguration.cs`:PK,FK `RoomId`,`SenderUserId`,`SenderUsername` `HasMaxLength(20)`,`Content` `HasMaxLength(500)`,`Channel` int,`SentAt`;`(RoomId, SentAt)` 索引。
- [x] 13.7 `Persistence/Converters/RoomIdConverter.cs`:与 `UserIdConverter` 同构。
- [x] 13.8 `Persistence/GomokuDbContext.cs`:新增 `DbSet<Room> Rooms`、`DbSet<Game> Games`、`DbSet<Move> Moves`、`DbSet<ChatMessage> ChatMessages`。

## 14. Infrastructure — Repository

- [x] 14.1 `Persistence/Repositories/RoomRepository.cs`:`FindByIdAsync` 用 `Include(r => r.Game).ThenInclude(g => g.Moves)` + `Include(r => r.ChatMessages)` + spectator 导航;`GetActiveRoomsAsync` 只 include 顶层 + 玩家的 User 信息(或让 Application 层自己 join),返回 `Where(r => r.Status != RoomStatus.Finished)`;`AddAsync`。
- [x] 14.2 Migration:`dotnet ef migrations add AddRoomsAndGameplay --project src/Gomoku.Infrastructure --startup-project src/Gomoku.Api --output-dir Persistence/Migrations`。人工审阅生成的表结构与索引。
- [x] 14.3 `AddInfrastructure()` 里 `AddScoped<IRoomRepository, RoomRepository>()`。
- [x] 14.4 把 `IOptions<RoomsOptions>` 绑定 `appsettings.json` 的 `"Rooms"` 节:`services.Configure<RoomsOptions>(configuration.GetSection("Rooms"))`。

## 15. Api — Controllers(REST)

- [x] 15.1 `Controllers/RoomsController.cs`:`[ApiController][Route("api/rooms")][Authorize]`。7 个 action:`Create`、`List`、`Get`、`Join`、`Leave`、`Spectate`、`Unspectate`。每个从 `User.FindFirst("sub")` 解出 `UserId`,组装 command/query 发给 `ISender`。
- [x] 15.2 Controller action 带 `CancellationToken`;`Create` 返回 201(`CreatedAtAction`);`Leave` / `Unspectate` / `Spectate` 返回 204。

## 16. Api — SignalR Hub + Notifier

- [x] 16.1 `Hubs/GomokuHub.cs`:`[Authorize] public sealed class GomokuHub : Hub`。注入 `ISender`、`IConnectionTracker`。客户端方法 `JoinRoom`、`LeaveRoom`、`MakeMove`、`SendChat`、`Urge`。全部从 `Context.UserIdentifier` 解 `UserId`。
- [x] 16.2 `Hubs/IConnectionTracker.cs` + `Hubs/ConnectionTracker.cs` 单例实现(进程内字典,见 design D12)。
- [x] 16.3 `Hubs/SignalRRoomNotifier.cs` 实现 `IRoomNotifier`:`IHubContext<GomokuHub>` 推送按 design D7 / D15 规则到 `room:{roomId}` / `room:{roomId}:spectators` / `Clients.User(...)`。
- [x] 16.4 `Hubs/GomokuHub.OnConnectedAsync` / `OnDisconnectedAsync`:调 `IConnectionTracker.Track/Untrack`。`JoinRoom(roomId)`:`Groups.AddToGroupAsync("room:" + roomId)`;若当前用户是该房间围观者(通过 `IRoomRepository` 查一次),额外 `AddToGroupAsync("room:" + roomId + ":spectators")`;否则仅加主群。
- [x] 16.5 Hub 方法中调用 `ISender.Send(command)`,领域异常让其冒泡(SignalR 会作为 `HubException` 发回客户端 —— 在开发环境开启详细错误:`AddSignalR(o => o.EnableDetailedErrors = true)` 仅 Development)。

## 17. Api — DI、JWT、Middleware

- [x] 17.1 `Program.cs` 加 `builder.Services.AddSignalR()`;`app.MapHub<GomokuHub>("/hubs/gomoku")`。
- [x] 17.2 `Program.cs` 改 `AddJwtBearer`:`Events.OnMessageReceived` 若路径 `StartsWithSegments("/hubs")` 则从 query 取 `access_token`。
- [x] 17.3 注册 `IRoomNotifier` / `IConnectionTracker`:`AddScoped<IRoomNotifier, SignalRRoomNotifier>()` + `AddSingleton<IConnectionTracker, ConnectionTracker>()`。
- [x] 17.4 `ExceptionHandlingMiddleware` 新增 switch 分支覆盖本次全部新异常(spec 的两张映射表)+ `DbUpdateConcurrencyException` → 409,`type` 用 `"https://gomoku-online/errors/concurrent-move"`。
- [x] 17.5 `UrgeTooFrequentException` 映射 429(新行)。

## 18. Api — Configuration

- [x] 18.1 `appsettings.json` 新增 `"Rooms": { ... }` 节(默认值见 RoomsOptions)。
- [x] 18.2 `Gomoku.Api.csproj` 确认无需新增 NuGet(SignalR 随 Web SDK 自带)。

## 19. 端到端冒烟与文档

- [x] 19.1 `HOW_TO_RUN.md` 加"房间 + 实时对战"章节:用两份 bash 脚本或两个浏览器 tab,分别登录 Alice 和 Bob,REST 创建 / 加入房间,SignalR 客户端(`dotnet-signalr` 工具或 `npm install @microsoft/signalr` 一次性 node 脚本)演示双方落子直到 BlackWin。
- [x] 19.2 跑端到端:Alice 注册 → 创建房间 → Bob 注册 → 加入房间 → 双方 SignalR `JoinRoom` → Alice `MakeMove(7,7)` → Bob `MakeMove(6,6)` → ... 直到连五 → 收到 `GameEnded`。第三个用户 Carol `spectate` → 收到所有广播。
- [x] 19.3 验证催促:Alice 在 Bob 回合时 `Urge` → Bob 客户端收 `UrgeReceived`;冷却内再次触发 → 429。
- [x] 19.4 验证聊天:`Room` 频道广播到所有人;围观者发 `Spectator` 频道只围观者收到。
- [x] 19.5 人工确认 `gomoku.db` 的 `Rooms` / `Games` / `Moves` / `ChatMessages` / `RoomSpectators` 表数据正确。

## 20. 归档前置检查

- [x] 20.1 `dotnet build Gomoku.slnx` 0 警告 0 错。
- [x] 20.2 `dotnet test Gomoku.slnx` 全部通过。Domain 预计 +~40 tests、Application 预计 +~30 tests。
- [x] 20.3 `grep -r "DateTime.UtcNow" src/Gomoku.Application src/Gomoku.Domain` 零命中。
- [x] 20.4 `grep -r "async void\|\.Result\b\|\.Wait(" src/Gomoku.Application src/Gomoku.Domain` 零命中。
- [x] 20.5 Domain 层 csproj 0 PackageReference / 0 ProjectReference。
- [x] 20.6 Application 层 csproj 仍只 `ProjectReference Gomoku.Domain`,无 EF Core / SignalR / Infrastructure。
- [x] 20.7 `openspec validate add-rooms-and-gameplay` 通过。
- [x] 20.8 按 CLAUDE.md 自检清单;分支 `feat/add-rooms-and-gameplay`;按层分组 commit(5–6 个 commit + 1 chore)。
