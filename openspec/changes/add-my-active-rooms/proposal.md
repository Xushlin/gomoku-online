## Why

前端登录后的落地页需要"继续对局"按钮 —— 跳回用户当前 Waiting 或 Playing 中的房间。没有这个端点,前端只能:

- 调 `GET /api/rooms`(全 Waiting / Playing 列表)→ 客户端自己 filter "我参与的" → 数据量大、网络浪费、需要服务端二次计算用户名。
- 或者前端**存 localStorage 记住 roomId** → 断网 / 换设备 / 换浏览器就丢。

补一个 `GET /api/users/me/active-rooms`,返回用户当前 Waiting / Playing 的房间摘要列表,服务端一次查询搞定。典型返回 0-2 条,不分页。

## What Changes

- **Application**:
  - `IRoomRepository` 新签名:
    ```
    Task<IReadOnlyList<Room>> GetActiveRoomsByUserAsync(UserId userId, CancellationToken cancellationToken);
    ```
    实现过滤 `Status in { Waiting, Playing }` 且 `BlackPlayerId == userId OR WhitePlayerId == userId`;按 `CreatedAt DESC` 排序;Include `Game` / `Moves` / `_spectators`(和现有 `GetActiveRoomsAsync` 一致,供 `RoomSummaryDto.SpectatorCount` 用)。
  - 新 feature `Features/Rooms/GetMyActiveRooms/`:
    - `GetMyActiveRoomsQuery(UserId UserId) : IRequest<IReadOnlyList<RoomSummaryDto>>`
    - Handler:调仓储 → 收集所有 UserId(Host / Black / White)→ `LookupUsernamesAsync` → 映射为 `RoomSummaryDto[]`(复用 `RoomMapping.ToSummary`)。
- **Infrastructure**:
  - `RoomRepository.GetActiveRoomsByUserAsync`:EF 查询:
    ```csharp
    return await _db.Rooms
        .Include(r => r.Game!).ThenInclude(g => g.Moves)
        .Include("_spectators")
        .Where(r => r.Status != RoomStatus.Finished)
        .Where(r => r.BlackPlayerId == userId
                 || (r.WhitePlayerId != null && r.WhitePlayerId == userId))
        .OrderByDescending(r => r.CreatedAt)
        .ToListAsync(ct);
    ```
- **Api**:
  - `UsersController` 新 action:`GET /api/users/me/active-rooms` → `GetMyActiveRoomsQuery(currentUserId)` → 200 + `IReadOnlyList<RoomSummaryDto>`。
  - 从 JWT `sub` 取 UserId,与 `/me` 端点同一模式。
- **Tests**:
  - `GetMyActiveRoomsQueryHandlerTests`(~4):
    - 成功:多房间(Alice 是黑 / 白 / 围观者的多种情形)→ 只返回 Alice 作为玩家的 Waiting/Playing 房间。
    - 无活动房间 → 空列表(不抛)。
    - 只返回 non-Finished(确认 repo 契约体现在 handler 输出)。
    - usernames 查找正确填充 Host/Black/White。

**显式不做**(留给后续):
- 别人的 active rooms(`/api/users/{id}/active-rooms`):侵犯隐私面("查 Bob 现在在哪个房间") + 对手信息可能用于作弊;若要社交加入归 `add-friend-spectate`。
- 围观中的房间也返回(作为"我在围观什么"):`add-my-spectating-rooms` 独立变更;围观关系和玩家关系语义差异大,端点分开更清晰。
- 分页:典型用户 ≤ 5 个活动房间,不值得。
- 按 Status ASC 排序(Waiting 在前 / Playing 在前):前端自己 sort 即可;服务端按 CreatedAt DESC 最直观。

## Capabilities

### Modified Capabilities

- **`room-and-gameplay`** —
  - 新 `IRoomRepository.GetActiveRoomsByUserAsync` 签名。
  - 新 query `GetMyActiveRoomsQuery` + handler。
  - 新 REST 端点 `GET /api/users/me/active-rooms`。

### New Capabilities

(无)

## Impact

- **代码规模**:~7 文件。
- **NuGet**:零。
- **HTTP 表面**:+1 端点。
- **SignalR**:零。
- **数据库**:零 schema 变化;新增一次 indexed query(按 `BlackPlayerId` / `WhitePlayerId` 过滤,已有索引)。
- **运行时**:典型 ≤ 5 行,ms 级。
- **后续变更依赖**:前端登录后的大厅 / 主页"继续对局"区域;`add-my-spectating-rooms` 可复用此模式。
