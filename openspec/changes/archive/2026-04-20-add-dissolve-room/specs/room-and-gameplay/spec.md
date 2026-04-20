## ADDED Requirements

### Requirement: `Room.Dissolve` 允许 Host 销毁 Waiting 房间

系统 SHALL 在 `Room` 聚合根上提供 `Dissolve(UserId senderId)` 方法。规则:

- 若 `senderId != HostUserId` → MUST 抛 `NotRoomHostException`。
- 若 `Status != Waiting` → MUST 抛 `RoomNotWaitingException`(复用现有异常)。
- 两项校验通过时,方法 MUST 不修改 `Room` 的任何字段 —— 物理删除由仓储层(`IRoomRepository.DeleteAsync`)完成,聚合只"祝福"这次删除。

本方法**不**接收 `DateTime now`:无状态变更,无须记录时间戳。

#### Scenario: Host 解散 Waiting 房间
- **WHEN** Host 对自己创建的 Waiting 房间调 `Dissolve(hostId)`
- **THEN** 方法返回,不抛异常,`Room` 字段保持不变

#### Scenario: 非 Host 尝试解散
- **WHEN** 非 Host 的 `UserId`(例如围观者、未来玩家、任意其他用户)调 `Dissolve`
- **THEN** 抛 `NotRoomHostException`

#### Scenario: Playing 房间不得解散
- **WHEN** Host 在 `Status == Playing` 时调 `Dissolve(hostId)`
- **THEN** 抛 `RoomNotWaitingException`

#### Scenario: Finished 房间不得解散
- **WHEN** Host 在 `Status == Finished` 时调 `Dissolve(hostId)`
- **THEN** 抛 `RoomNotWaitingException`(同样用现有异常,不新增 `RoomAlreadyFinishedException`)

#### Scenario: 带围观者 / 聊天的 Waiting 房间仍可解散
- **WHEN** Waiting 房间有 2 个围观者与若干聊天消息,Host 调 `Dissolve(hostId)`
- **THEN** 方法不抛异常;后续仓储层物理删除会级联清除围观者与聊天(由 EF Cascade 保证)

---

### Requirement: `IRoomRepository.DeleteAsync` 标记删除房间聚合

Application 层 SHALL 在 `IRoomRepository` 上新增:

```
Task DeleteAsync(Room room, CancellationToken cancellationToken);
```

实现 MUST:
- 仅把聚合从上下文中标记为删除(`DbContext.Rooms.Remove(room)` 或等价),MUST NOT 调 `SaveChangesAsync`;
- 依赖 EF 配置的 `OnDelete(Cascade)`(Room → Game / Spectators / ChatMessages,Game → Moves),不在代码里手工遍历子实体删除。

签名 MUST NOT 出现 `IQueryable` / `Expression` / EF Core 类型。

#### Scenario: 仓储删除不提交
- **WHEN** 调用 `DeleteAsync(room, ct)` 后,且同一 handler 尚未调 `IUnitOfWork.SaveChangesAsync`
- **THEN** 数据库中该房间仍存在;`SaveChangesAsync` 被调用后才真正消失

#### Scenario: 级联删除生效
- **WHEN** `DeleteAsync` + `SaveChangesAsync` 一整次事务后,针对被删房间 Id 查询 Games / Moves / RoomSpectators / ChatMessages
- **THEN** 上述子表对应行**全部消失**

---

### Requirement: `IRoomNotifier.RoomDissolvedAsync` 广播房间解散事件

Application 层 SHALL 在 `IRoomNotifier` 上新增:

```
Task RoomDissolvedAsync(RoomId roomId, CancellationToken cancellationToken);
```

Api 层实现 MUST 把客户端方法 `RoomDissolved` 发到 `room:{roomId.Value}` SignalR group,payload 形如 `{ RoomId: Guid }`。

MUST 由 handler 在 `SaveChangesAsync` **之后**调用(遵守现有"事务成功后再推事件"的约束)。

#### Scenario: 广播成功后组清理
- **WHEN** 被解散房间 `{id}` 的 SignalR group 内有 1 个围观者 connection 订阅
- **THEN** 该连接收到一次 `RoomDissolved({RoomId: id})` 事件;之后连接侧主动 `LeaveRoom` 或前端自行处理即可;服务端**不主动**从 group 中移除 connection

#### Scenario: 广播到空 group
- **WHEN** 被解散房间没有任何订阅(无围观者连着)
- **THEN** 调用 MUST 不抛异常,无副作用(SignalR 对空 group 是 no-op)

---

### Requirement: `DELETE /api/rooms/{id}` 端点触发解散

Api 层 SHALL 暴露 `DELETE /api/rooms/{id}`(`[Authorize]`)。Controller 从 JWT `sub` 取 `UserId sender`;发 `DissolveRoomCommand(sender, new RoomId(id))`;成功 `204 No Content`。

MUST NOT 接受 body;MUST NOT 接受 query 参数。

#### Scenario: 成功
- **WHEN** Host 以合法 Bearer token 调 `DELETE /api/rooms/{id}`,对应房间是其创建的 Waiting 房
- **THEN** HTTP 204,响应体为空;随后 `GET /api/rooms/{id}` 返回 404

#### Scenario: 非 Host
- **WHEN** 非 Host 用户 `DELETE /api/rooms/{id}`
- **THEN** HTTP 403,`ProblemDetails.title` 指向 `NotRoomHostException`

#### Scenario: Playing 房间
- **WHEN** Host 对 Playing 房间调 `DELETE /api/rooms/{id}`
- **THEN** HTTP 409,`ProblemDetails` 指向 `RoomNotWaitingException`

#### Scenario: 未登录
- **WHEN** 无 Bearer token 调 `DELETE /api/rooms/{id}`
- **THEN** HTTP 401(由 JWT 中间件处理)

#### Scenario: 房间不存在(或已被并发删除)
- **WHEN** `DELETE /api/rooms/{id}`,但该 Id 不存在
- **THEN** HTTP 404,`ProblemDetails` 指向 `RoomNotFoundException`

---

### Requirement: 新增异常 `NotRoomHostException` 与其 HTTP 映射

系统 SHALL 在 `Gomoku.Domain/Exceptions/RoomExceptions.cs` 新增 `NotRoomHostException`(sealed,继承 `Exception`,提供 `(string message)` 构造器)。

Api 层全局异常中间件 MUST 映射:

| 异常 | HTTP |
|---|---|
| `NotRoomHostException` | 403 |

(现有 `RoomNotFoundException` → 404、`RoomNotWaitingException` → 409 保持不变,本 Requirement 不重申。)

#### Scenario: 映射生效
- **WHEN** 非 Host 用户触发 `NotRoomHostException`(例如通过 `DELETE /api/rooms/{id}`)
- **THEN** 响应 HTTP 403,`ProblemDetails.title` 指向 `NotRoomHostException`,`ProblemDetails.detail` 包含抛出时的 message

## MODIFIED Requirements

### Requirement: `Room.Leave` 让玩家 / 围观者离开房间

系统 SHALL 提供 `Room.Leave(UserId userId, DateTime now)`。规则:

- 若 `userId` 不在该房间(既非玩家、也非围观者):MUST 抛 `NotInRoomException`
- 若 `userId` 是围观者:从 `Spectators` 移除
- 若 `userId` 是玩家且 `Status == Waiting`(只有创建者这一种情况):创建者 MUST 抛 `HostCannotLeaveWaitingRoomException`,提示调用 `DELETE /api/rooms/{id}` 解散房间(**本次修订**:现在该错误消息指向一个**真实存在**的解散端点,不再是死胡同)。
- 若 `userId` 是玩家且 `Status == Playing`:该玩家视为"离席",`Status` 保持 `Playing`,`Game` 不变,棋局对手仍可落子;本次**不**自动判负(见 design Non-Goals;认输 / 超时判负留给 `add-timeout-resign`)。
- 若 `Status == Finished`:玩家 / 围观者均可自由离开。

#### Scenario: 围观者离开
- **WHEN** 围观者 `C` 调 `Room.Leave(c, now)`
- **THEN** `C ∉ Spectators`;其他字段不变

#### Scenario: 对局中的玩家离席
- **WHEN** 玩家 `Alice` 在 `Status == Playing` 时调 `Room.Leave(aliceId, now)`
- **THEN** `Status` 仍为 `Playing`,`Game` 状态不变,`BlackPlayerId` 仍为 `aliceId`(视为"挂起 / 离席",判负逻辑留给后续变更)

#### Scenario: Waiting 状态下 Host 尝试离开
- **WHEN** 创建者在 `Status == Waiting` 时调 `Room.Leave(hostId, now)`
- **THEN** 抛 `HostCannotLeaveWaitingRoomException`,**消息提示"请通过 `DELETE /api/rooms/{id}` 解散房间"**;Host 应用该端点替代 Leave

#### Scenario: 非成员离开
- **WHEN** 不在房间的用户调 `Room.Leave`
- **THEN** 抛 `NotInRoomException`
