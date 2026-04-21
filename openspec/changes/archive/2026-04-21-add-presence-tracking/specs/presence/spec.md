## ADDED Requirements

### Requirement: `IConnectionTracker` 承担跨 SignalR 连接的用户在线状态追踪

Application 层 SHALL 在 `Gomoku.Application/Abstractions/IConnectionTracker.cs` 定义接口
`IConnectionTracker`,从 `Gomoku.Api.Hubs` 迁移(Clean Architecture:抽象靠内,实现靠外)。
接口成员:

- `ValueTask TrackAsync(string connectionId, UserId userId)` —— SignalR 连接建立时绑定。
- `ValueTask UntrackAsync(string connectionId)` —— 连接断开时清理。
- `ValueTask AssociateRoomAsync(string connectionId, RoomId roomId)` —— 连接加入房间。
- `ValueTask DissociateRoomAsync(string connectionId, RoomId roomId)` —— 连接从房间移除。
- **(本次新增)** `int GetOnlineUserCount()` —— 当前至少有一条活连接的不同 `UserId` 数。
- **(本次新增)** `bool IsUserOnline(UserId userId)` —— 指定用户是否至少有一条活连接。

实现 `ConnectionTracker` 留在 `Gomoku.Api.Hubs/ConnectionTracker.cs`(Infrastructure 侧),
维护 `ConcurrentDictionary<UserId, int>` 引用计数:`TrackAsync` 递增,`UntrackAsync` 递减,
计数为 0 时移除 key(原子 TryRemove / TryUpdate 避免竞态)。同用户多标签 / 多设备多连接
算一个"在线",最后一条连接断开才变"离线"。

现有 `GomokuHub` 调用点(`OnConnectedAsync` / `OnDisconnectedAsync`)无需改动,只改 `using`。

#### Scenario: 接口位置
- **WHEN** 审阅 `Gomoku.Application/Abstractions/IConnectionTracker.cs`
- **THEN** 文件存在,含上述 6 个成员;`Gomoku.Api.Hubs/IConnectionTracker.cs` MUST NOT 存在

#### Scenario: 多连接同用户只算一个 online
- **WHEN** Alice 在浏览器标签 1 + 标签 2 + 手机 App 各建一条 SignalR 连接(3 个 connectionId,同一 Alice.UserId)
- **THEN** `GetOnlineUserCount()` 结果对 Alice 的贡献是 1(不是 3);`IsUserOnline(alice.Id) == true`

#### Scenario: 最后一条断开后变 offline
- **WHEN** Alice 只有 1 条连接,Untrack 该连接
- **THEN** `IsUserOnline(alice.Id) == false`;`GetOnlineUserCount()` 不再计 Alice

#### Scenario: 并发 Track / Untrack 正确
- **WHEN** 多线程同时 Track / Untrack 同一 UserId(concurrent incr / decr)
- **THEN** 最终引用计数与实际活连接数一致;无"计数变 -1 永远 offline"或"key 永不移除"的卡死

---

### Requirement: `GetOnlineCountQuery` 返回在线用户数

Application 层 SHALL 在 `Features/Presence/GetOnlineCount/` 定义:

```
public sealed record GetOnlineCountQuery() : IRequest<OnlineCountDto>;
public sealed record OnlineCountDto(int Count);
```

Handler 调 `IConnectionTracker.GetOnlineUserCount()` 并包成 DTO。无 validator(无参数)。

#### Scenario: 成功返回
- **WHEN** handler 执行时 tracker 返回 42
- **THEN** `OnlineCountDto.Count == 42`

#### Scenario: 无人在线
- **WHEN** tracker 返回 0
- **THEN** `OnlineCountDto.Count == 0`;不抛异常

---

### Requirement: `IsUserOnlineQuery` 判定指定用户在线

Application 层 SHALL 在 `Features/Presence/IsUserOnline/` 定义:

```
public sealed record IsUserOnlineQuery(UserId UserId) : IRequest<PresenceDto>;
public sealed record PresenceDto(Guid UserId, bool IsOnline);
```

Handler 调 `IConnectionTracker.IsUserOnline(UserId)`;包 `PresenceDto`(Guid + bool)。

无 validator —— 路由 `{id:guid}` 约束拦非法字符串;且对 unknown UserId,tracker MUST 返回
`false`(不抛异常)—— presence 端点是"在不在线"二值,不是"用户存在吗"(后者用 `/api/users/{id}`)。

#### Scenario: 在线
- **WHEN** tracker.IsUserOnline(alice.Id) → true
- **THEN** `PresenceDto(alice.Id.Value, true)`

#### Scenario: 不在线
- **WHEN** tracker.IsUserOnline(bob.Id) → false
- **THEN** `PresenceDto(bob.Id.Value, false)`

#### Scenario: 未知 UserId 也返回 false(不 404)
- **WHEN** 查询一个数据库里**不存在**的 Guid
- **THEN** tracker 返回 false,handler 返回 `PresenceDto(unknownGuid, false)`;HTTP 200

---

### Requirement: `GET /api/presence/online-count` 返回在线人数

Api 层 SHALL 暴露 `GET /api/presence/online-count`(`[Authorize]`):

- Controller `PresenceController.OnlineCount` 派 `GetOnlineCountQuery`;
- 成功 HTTP 200 + `OnlineCountDto`。

#### Scenario: 登录用户拉在线人数
- **WHEN** 登录用户 Alice 调 `GET /api/presence/online-count`
- **THEN** HTTP 200;body `{ "count": N }`(N 等于当前 tracker 返回值)

#### Scenario: 未登录 401
- **WHEN** 不带 Bearer token
- **THEN** HTTP 401

---

### Requirement: `GET /api/presence/users/{id}` 返回指定用户在线状态

Api 层 SHALL 暴露 `GET /api/presence/users/{id:guid}`(`[Authorize]`):

- Controller `PresenceController.IsOnline` 派 `IsUserOnlineQuery(new UserId(id))`;
- 成功 HTTP 200 + `PresenceDto`。

#### Scenario: 真人在线
- **WHEN** Alice 已建 SignalR 连接,另一登录用户 Bob 调 `GET /api/presence/users/{alice.Id}`
- **THEN** HTTP 200;`{ "userId": "...", "isOnline": true }`

#### Scenario: 未知 userId 返回 isOnline=false(不 404)
- **WHEN** 传入一个不存在的 Guid
- **THEN** HTTP 200;`{ "userId": "same-guid", "isOnline": false }` —— presence 语义是"是否在线",不检查用户是否存在

#### Scenario: 未登录 401
- **WHEN** 不带 Bearer token
- **THEN** HTTP 401
