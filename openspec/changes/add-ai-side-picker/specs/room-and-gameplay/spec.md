## ADDED Requirements

### Requirement: `Room.SwapPlayers(now)` 在棋局未开局时交换黑白方

`Room` 聚合 SHALL 提供 `void SwapPlayers(DateTime now)` 公共方法,行为:

- 前置条件:`Status == RoomStatus.Playing` AND `Game!.Moves.Count == 0`(刚 `JoinAsPlayer` 完、第一手还没下)。任一条件不满足 MUST 抛 `InvalidOperationException`("Cannot swap players after the first move." 或等价描述)。
- 操作:**仅交换** `BlackPlayerId` 与 `WhitePlayerId` 两个字段。
- 不变量:`HostUserId` MUST NOT 改变(host 仍是房间创建者);`Game.CurrentTurn` MUST NOT 改变(始终是 `Stone.Black`,因为黑子先行的规则与"谁坐黑"无关)。
- 不发任何 SignalR 事件 —— 通常在 `CreateAiRoomCommandHandler` 内 + `JoinAsPlayer` 同事务里调用,事务提交后客户端首次拉房间状态拿到的就是已交换的状态。

#### Scenario: 合法窗口内交换
- **WHEN** 一房间刚 `Room.Create + JoinAsPlayer` 完成(Status=Playing,Moves 为空),调 `room.SwapPlayers(now)`
- **THEN** `BlackPlayerId` 和 `WhitePlayerId` 互换;`HostUserId` 不变;`Game.CurrentTurn == Stone.Black`(不变)

#### Scenario: 已有落子时拒绝
- **WHEN** 房间已经有至少 1 步落子,调 `room.SwapPlayers(now)`
- **THEN** 抛 `InvalidOperationException`;字段未改变

#### Scenario: Waiting 状态拒绝
- **WHEN** 一房间刚 `Room.Create` 完(Status=Waiting,WhitePlayerId=null),调 `room.SwapPlayers(now)`
- **THEN** 抛 `InvalidOperationException`(只有完整双方 Playing 状态才允许 swap)

#### Scenario: Finished 状态拒绝
- **WHEN** 房间 Status=Finished(已结束的对局),调 `room.SwapPlayers(now)`
- **THEN** 抛 `InvalidOperationException`
