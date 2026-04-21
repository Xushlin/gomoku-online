## ADDED Requirements

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
