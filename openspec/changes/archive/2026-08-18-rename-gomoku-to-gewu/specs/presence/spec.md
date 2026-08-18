# presence Specification Delta

## MODIFIED Requirements

### Requirement: `IConnectionTracker` 承担跨 SignalR 连接的用户在线状态追踪

Application 层 SHALL 在 `Gewu.Application/Abstractions/IConnectionTracker.cs` 定义接口
`IConnectionTracker`,从 `Gewu.Api.Hubs` 迁移(Clean Architecture:抽象靠内,实现靠外)。
接口成员:

- `ValueTask TrackAsync(string connectionId, UserId userId)` —— SignalR 连接建立时绑定。
- `ValueTask UntrackAsync(string connectionId)` —— 连接断开时清理。
- `ValueTask AssociateRoomAsync(string connectionId, RoomId roomId)` —— 连接加入房间。
- `ValueTask DissociateRoomAsync(string connectionId, RoomId roomId)` —— 连接从房间移除。
- **(本次新增)** `int GetOnlineUserCount()` —— 当前至少有一条活连接的不同 `UserId` 数。
- **(本次新增)** `bool IsUserOnline(UserId userId)` —— 指定用户是否至少有一条活连接。

实现 `ConnectionTracker` 留在 `Gewu.Api.Hubs/ConnectionTracker.cs`(Infrastructure 侧),
维护 `ConcurrentDictionary<UserId, int>` 引用计数:`TrackAsync` 递增,`UntrackAsync` 递减,
计数为 0 时移除 key(原子 TryRemove / TryUpdate 避免竞态)。同用户多标签 / 多设备多连接
算一个"在线",最后一条连接断开才变"离线"。

现有 `MatchHub` 调用点(`OnConnectedAsync` / `OnDisconnectedAsync`)无需改动,只改 `using`。

#### Scenario: 接口位置
- **WHEN** 审阅 `Gewu.Application/Abstractions/IConnectionTracker.cs`
- **THEN** 文件存在,含上述 6 个成员;`Gewu.Api.Hubs/IConnectionTracker.cs` MUST NOT 存在

#### Scenario: 多连接同用户只算一个 online
- **WHEN** Alice 在浏览器标签 1 + 标签 2 + 手机 App 各建一条 SignalR 连接(3 个 connectionId,同一 Alice.UserId)
- **THEN** `GetOnlineUserCount()` 结果对 Alice 的贡献是 1(不是 3);`IsUserOnline(alice.Id) == true`

#### Scenario: 最后一条断开后变 offline
- **WHEN** Alice 只有 1 条连接,Untrack 该连接
- **THEN** `IsUserOnline(alice.Id) == false`;`GetOnlineUserCount()` 不再计 Alice

#### Scenario: 并发 Track / Untrack 正确
- **WHEN** 多线程同时 Track / Untrack 同一 UserId(concurrent incr / decr)
- **THEN** 最终引用计数与实际活连接数一致;无"计数变 -1 永远 offline"或"key 永不移除"的卡死
