# in-room-chat Specification Delta

## MODIFIED Requirements

### Requirement: 催促事件仅推给被催玩家

`UrgeOpponentCommand` Handler 成功后 MUST 调 `IRoomNotifier.OpponentUrgedAsync(roomId, urgedUserId, payload)`。SignalR 实现 MUST 用 `IHubContext<MatchHub>.Clients.User(urgedUserId.ToString()).SendAsync("UrgeReceived", payload)` —— **只发给被催那一方**,不广播给房间。

`payload` 至少包含 `{ fromUserId, fromUsername, sentAt }`。

#### Scenario: 仅被催方收到
- **WHEN** 黑方成功催促白方
- **THEN** `Clients.User(whitePlayerId).SendAsync("UrgeReceived", ...)` 被调一次;`Clients.Group("room:{roomId}").SendAsync` 不被触发
