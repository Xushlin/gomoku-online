# room-and-gameplay 的规格变化

## MODIFIED Requirements

### Requirement: `Room.Leave` 让玩家 / 围观者离开房间

系统 SHALL 提供 `Room.Leave(UserId userId, DateTime now)`。规则:

- 若 `userId` 不在该房间(既非玩家、也非围观者):MUST 抛 `NotInRoomException`
- 若 `userId` 是围观者:从 `Spectators` 移除
- 若 `userId` 是玩家且 `Status == Waiting`(只有创建者这一种情况):创建者 MUST 抛 `HostCannotLeaveWaitingRoomException`,提示调用 `DELETE /api/rooms/{id}` 解散房间。
- 若 `userId` 是玩家且 `Status == Playing`:该玩家视为"离席",`Status` 保持 `Playing`,`Game` 不变,其余玩家仍可落子;**不**自动判负。
- 若 `Status == Finished`:玩家 / 围观者均可自由离开。

**「是玩家」的判据 SHALL 是"他占着任何一个座位"(`Room.IsPlayer`),MUST NOT 列举座位号。**
本条此前的实现写的是 `BlackPlayerId || WhitePlayerId`,于是三座位房间里 2 号座位上的人被当成
外人 —— 实测 `POST /api/rooms/{id}/leave` 返回 **404**,他离不开自己在的房间。要求正文一直是
「既非玩家、也非围观者」,所以那是一处实现不合规,不是规格改动。

#### Scenario: 围观者离开
- **WHEN** 围观者 `C` 调 `Room.Leave(c, now)`
- **THEN** `C ∉ Spectators`;其他字段不变

#### Scenario: 对局中的玩家离席
- **WHEN** 玩家 `Alice` 在 `Status == Playing` 时调 `Room.Leave(aliceId, now)`
- **THEN** `Status` 仍为 `Playing`,`Game` 状态不变,她仍占着原来那个座位(视为"挂起 / 离席")

#### Scenario: 三座位房间里最后一个座位上的玩家离席
- **WHEN** 一个三座位棋种的房间里,2 号座位上的玩家调 `Room.Leave`
- **THEN** MUST NOT 抛 `NotInRoomException`;他仍占着 2 号座位

#### Scenario: Waiting 状态下 Host 尝试离开
- **WHEN** 创建者在 `Status == Waiting` 时调 `Room.Leave(hostId, now)`
- **THEN** 抛 `HostCannotLeaveWaitingRoomException`,**消息提示"请通过 `DELETE /api/rooms/{id}` 解散房间"**

#### Scenario: 非成员离开
- **WHEN** 不在房间的用户调 `Room.Leave`
- **THEN** 抛 `NotInRoomException`

### Requirement: `Room.JoinAsSpectator` / `LeaveAsSpectator` 管理围观者集合

系统 SHALL 提供这两个方法:

- `JoinAsSpectator(UserId userId)`:
  - 若 `userId` **占着本房间任何一个座位** → MUST 抛 `PlayerCannotSpectateException`
  - 若 `userId ∈ Spectators` → 幂等成功(no-op)
  - 否则加入 `Spectators`
- `LeaveAsSpectator(UserId userId)`:
  - 若 `userId ∉ Spectators` → MUST 抛 `NotSpectatingException`
  - 否则移除

两者对 `Room.Status` 无限制(`Waiting` / `Playing` / `Finished` 均可围观)。

**判据 SHALL 是 `Room.IsPlayer`,MUST NOT 写成 `BlackPlayerId` / `WhitePlayerId`。**
本条此前的正文就是那样写的 —— 于是三座位房间里 2 号座位上的玩家**围观成功**(实测
`POST /api/rooms/{id}/spectate` 返回 **204**),而他同时坐在牌桌上。这不是一个宽松的选择:
围观之后 `IsSpectator == true`,`RoomView.For` 给他围观视角,围观频道的全部内容随之发给一个玩家
—— 正是 `fix-spectator-chat-leak` 要挡的那件事。「玩家不可围观」的意图一直在,漏的是座位数。

#### Scenario: 普通用户成为围观者
- **WHEN** 非玩家用户 `C` 调 `JoinAsSpectator(c)`
- **THEN** `C ∈ Spectators`

#### Scenario: 玩家尝试围观
- **WHEN** 任何一个占着座位的玩家调 `JoinAsSpectator`
- **THEN** 抛 `PlayerCannotSpectateException`

#### Scenario: 三座位房间的最后一个座位同样不能围观
- **WHEN** 一个三座位棋种的房间里,2 号座位上的玩家调 `JoinAsSpectator`
- **THEN** 抛 `PlayerCannotSpectateException`,且 `Spectators` 仍为空

#### Scenario: 重复围观幂等
- **WHEN** 已在围观者集合的用户再次调 `JoinAsSpectator`
- **THEN** 不抛异常,`Spectators` 不出现重复项
