## Why

前端要做"谁在线 / Alice 在玩吗 / 大厅上方显示在线人数"—— 现在零支持。

好消息:`add-ai-opponent` 为 SignalR 接入时已经装了 `IConnectionTracker`(connection-id → userId 映射),Hub 的 `OnConnectedAsync` / `OnDisconnectedAsync` 已经在调 `TrackAsync` / `UntrackAsync`。**只差**暴露两个 REST 查询:"在线人数" 和 "这个 userId 在不在线"。

本轮做的事:
1. 把 `IConnectionTracker` 接口从 `Gomoku.Api.Hubs` 迁到 `Gomoku.Application.Abstractions`(Clean Arch 修正 —— impl 留在 Api)。
2. 接口扩展两个只读方法:`GetOnlineUserCount()`、`IsUserOnline(UserId)`。实现层维护一个 `ConcurrentDictionary<UserId, int>` 引用计数(同一用户多标签 / 多设备都算一个"在线",最后一个断开时移除)。
3. 新 Application feature:`GetOnlineCountQuery` / `IsUserOnlineQuery`,走 MediatR pipeline(享受 LoggingBehavior 的自动 enter/exit 日志)。
4. 新 `PresenceController`:`GET /api/presence/online-count` + `GET /api/presence/users/{id}`。新 capability `presence`。

## What Changes

- **Application**:
  - **迁移**:`IConnectionTracker` 接口从 `Gomoku.Api.Hubs` 移到 `Gomoku.Application.Abstractions`(Clean Arch:抽象靠内,实现靠外)。既有类型成员:
    - `TrackAsync(connId, UserId)` / `UntrackAsync(connId)` / `AssociateRoomAsync(connId, RoomId)` / `DissociateRoomAsync(connId, RoomId)` —— **不动**。
  - **接口扩展**:追加
    - `int GetOnlineUserCount()` —— 返回当前至少有一条活连接的不同 UserId 数。
    - `bool IsUserOnline(UserId userId)` —— 当前该用户是否至少有一条活连接。
  - 新 DTOs `Common/DTOs/PresenceDto.cs`:
    - `OnlineCountDto(int Count)`;
    - `PresenceDto(Guid UserId, bool IsOnline)`。
  - 新 feature `Features/Presence/GetOnlineCount/`:`GetOnlineCountQuery() : IRequest<OnlineCountDto>` + handler(调 `IConnectionTracker.GetOnlineUserCount()` 包 DTO)。
  - 新 feature `Features/Presence/IsUserOnline/`:`IsUserOnlineQuery(UserId UserId) : IRequest<PresenceDto>` + handler。
  - 这两 feature 无 validator(无输入参数校验需求;路由 guid 约束已拦非法)。
- **Api**:
  - `ConnectionTracker` impl 更新:维护 `ConcurrentDictionary<UserId, int>` 引用计数 —— `Track` 递增、`Untrack` 递减(为 0 时 remove)。两个新方法 `GetOnlineUserCount` / `IsUserOnline` 用该 dict。
  - `Hubs/IConnectionTracker.cs` 文件**删除**(接口已搬家,保留只读 using 语句指向 Application)。
  - `GomokuHub` / `Program.cs` 的 `using` 指向新位置。
  - 新 `PresenceController`:`[Authorize]`、`GET /api/presence/online-count` + `GET /api/presence/users/{id:guid}`;各派一个 MediatR query。
- **Tests**:
  - `Gomoku.Application.Tests/Features/Presence/GetOnlineCountQueryHandlerTests`:mock `IConnectionTracker.GetOnlineUserCount()` 返回 N → 返回 DTO.Count == N。
  - `Gomoku.Application.Tests/Features/Presence/IsUserOnlineQueryHandlerTests`:mock online=true/false → DTO 正确。
  - 不单独测 `ConnectionTracker` 的引用计数(并发语义难稳定覆盖);E2E smoke 走 SignalR 连 / 断开覆盖真实行为。

**显式不做**(留给后续):
- "在线用户完整列表"端点(`GET /api/presence/online-users`):对大用户量隐私 / payload 大,不做默认;若将来需要限"当前好友列表交集"式,`add-friend-list` 再覆盖。
- 在线状态区分 "in-game / idle / browsing":需要把 connection tracker 与 Room Hub 状态关联成三态。本次只要"连着就算 online"。
- 实时"用户上下线 push"(SignalR 主动推事件):相当于给全平台广播个人活动,量级敏感;前端轮询 `/online-count` 足够看"活跃度"。
- Redis 后端的 `IConnectionTracker` 实现(水平扩展时必需):单实例用内存字典够。
- 前端 WebSocket 断线重连期间的"摇摆":连接一断 UserCount 立刻 -1;客户端重连瞬间又 +1;2s 抖动可接受。若将来要"5 分钟宽容期",加一层 delayed-untrack。

## Capabilities

### New Capabilities

- **`presence`** — 在线用户存在性追踪。基于 SignalR 连接生命周期的内存引用计数,暴露"在线人数"与"某用户是否在线"的只读查询。

### Modified Capabilities

(无)

## Impact

- **代码规模**:~10 新 / 迁移 / 修改文件。
- **NuGet**:零。
- **HTTP 表面**:+2 端点。
- **SignalR**:零新协议;仅 `ConnectionTracker` 内部簿记增量。
- **数据库**:零(presence 纯内存)。
- **运行时**:每次连 / 断各一次 O(1) `ConcurrentDictionary` 更新;查询端点 O(1)。
- **后续变更依赖**:前端大厅在线人数显示、用户主页"在线"徽章;`add-friend-list` 的"好友在线列表";水平扩展时的 `IConnectionTracker` Redis 实现。
