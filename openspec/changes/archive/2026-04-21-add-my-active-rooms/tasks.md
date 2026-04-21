## 1. Application — IRoomRepository 扩展

- [x] 1.1 `IRoomRepository.cs` 追加:
  ```
  Task<IReadOnlyList<Room>> GetActiveRoomsByUserAsync(UserId userId, CancellationToken cancellationToken);
  ```
  XML:Status != Finished;BlackPlayerId==userId OR WhitePlayerId==userId;按 CreatedAt DESC;Include Game/Moves/_spectators。

## 2. Application — feature

- [x] 2.1 `Features/Rooms/GetMyActiveRooms/GetMyActiveRoomsQuery.cs`:
  `record GetMyActiveRoomsQuery(UserId UserId) : IRequest<IReadOnlyList<RoomSummaryDto>>`。
- [x] 2.2 `Features/Rooms/GetMyActiveRooms/GetMyActiveRoomsQueryHandler.cs`:
  - 调 repo;
  - 收集所有 UserId(Host + Black + White,用 `room.CollectUserIds()`);
  - `LookupUsernamesAsync`;
  - 映射 `rooms.Select(r => r.ToSummary(usernames))` 返回。

## 3. Infrastructure

- [x] 3.1 `RoomRepository.GetActiveRoomsByUserAsync` 实现(见 proposal)。

## 4. Api

- [x] 4.1 `UsersController` 新 action:
  ```csharp
  [HttpGet("me/active-rooms")]
  public async Task<ActionResult<IReadOnlyList<RoomSummaryDto>>> MyActiveRooms(CancellationToken ct)
  {
      var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
          ?? User.FindFirst("sub")?.Value
          ?? throw new UnauthorizedAccessException("Missing sub claim.");
      var userId = new UserId(Guid.Parse(sub));
      var rooms = await _mediator.Send(new GetMyActiveRoomsQuery(userId), ct);
      return Ok(rooms);
  }
  ```
  路由 `me/active-rooms` 与 `{id:guid}/games` 并存;`me` 在 `/me` 后的 `/active-rooms` 不被 `{id:guid}` 拦(me 不是 guid)。

## 5. 测试

- [x] 5.1 `GetMyActiveRoomsQueryHandlerTests`(~4):
  - 一个 Alice 参与的 Playing + 一个 Finished + 一个别人的 Playing + 一个 Waiting(Alice host);repo 只返回 Alice 的 Playing + Waiting(2 条);handler 映射正确。
  - Alice 无活动房间 → 空列表。
  - UserId 的 Alice 作为 White 玩家的房间 也被返回(仓储契约测试)。
  - Host/Black/White 的 username 正确填充(usernames lookup 调用)。

## 6. 端到端冒烟

- [x] 6.1 Alice 注册、创建 2 个房间(Waiting)、另外 join 一个 Bob 的房间(Playing 为 White)。
- [x] 6.2 `GET /api/users/me/active-rooms`(Alice token):返回 3 条摘要。
- [x] 6.3 未登录 → 401。
- [x] 6.4 Alice 的对局 Bob 认输 → 该房间变 Finished → 再调 /me/active-rooms 返回 2 条(Finished 不在)。

## 7. 归档前置检查

- [x] 7.1 `dotnet build Gomoku.slnx`:0 警告 0 错。
- [x] 7.2 `dotnet test Gomoku.slnx`:全绿(Application +4)。
- [x] 7.3 `openspec validate add-my-active-rooms --strict`:valid。
- [x] 7.4 分支 `feat/add-my-active-rooms`,按层 Application / Infrastructure / Api / docs 四条 commit。
