## Context

`add-rooms-and-gameplay` 给 `Room.Leave` 埋了一条"Waiting + Host → 抛异常并指向一个不存在的解散端点"的死胡同,预埋了"解散是独立 API"的设计意图,但**没有**交付那个独立 API。本变更**只**做这件事。

目的是让 Host 能放弃自己创建的、还没人加入的房间;不做任何与对局中断相关的事(那是 B 的范围)。作用域小到不值得一个 new capability,只是 `room-and-gameplay` 的一次小小修订。

## Goals / Non-Goals

**Goals**:
- 真人 Host 能通过一个 REST 端点撤销自己创建的 Waiting 房间。
- 房间被删除后,其中的围观者通过 SignalR 得到通知。
- 现有"Host 不能静默离开 Waiting"的规则保持不变;Host 的唯一合法退场是走 `Dissolve`。

**Non-Goals**:
- Playing / Finished 房间的清理。
- 软删除 / 审计日志 / 去向记录。
- 围观者 / 未来玩家看不到已解散房间的"优雅淡出"(本次直接物理删除 + 一次广播,不做前端状态机辅助)。
- 解散前的"二次确认"prompt(UI 职责)。
- AI 房间的解散(`CreateAiRoom` 后房间已 Playing,本次端点对其无效)。

## Decisions

### D1 — 只允许 Waiting 状态解散

Waiting 房间的全部状态就是"一个 Host UserId + 名字 + 可能的围观者"—— 丢了零损失。Playing / Finished 涉及 Game 实体、Moves 历史、ELO 暗示;这些路径都要和认输 / 超时 / 归档算得清清楚楚,不属于本次。

若 Host 调 `DELETE /api/rooms/{id}` 时房间在 Playing / Finished:返回 409 `RoomNotWaitingException`。

### D2 — 物理删除而非软删除

Waiting 房间无 Game、无 Move、无聊天内容(**按 spec 聊天需要至少一方玩家就位,但 Waiting 状态下聊天消息合法吗?** 查现有 spec:`PostChatMessage` 只要求发送者是玩家或围观者,对 Room.Status 无限制 —— 所以 Waiting 房间**可能**有聊天)。

即便有几条 Waiting 期间的围观者聊天,也没有审计价值(无对局、无结果)。物理删依赖 EF 已配置的级联:

- `RoomConfiguration`:`Game`、`_spectators`、`_chatMessages` 都有 `OnDelete(Cascade)`。
- `GameConfiguration`:`Moves` 有 `OnDelete(Cascade)`(Waiting 下 Game 为 null,不触发)。

`_db.Rooms.Remove(room)` + `SaveChanges` 一次清干净,零 migration。

### D3 — `Room.Dissolve` 是"纯校验" domain 方法,不改状态

聚合根的 `Dissolve` 方法只做:
- `senderId == HostUserId`?否 → `NotRoomHostException`。
- `Status == Waiting`?否 → `RoomNotWaitingException`(复用现有异常)。
- 通过 → 返回 void,**不修改任何字段**。

理由:物理删除发生在仓储。聚合状态没有"Dissolved"值要进入;若加一个 `Status.Dissolved` 则意味着"软删除",违反 D2。

**考虑过但弃用**:让 Room 暴露一个 `IsDissolved` 布尔 / `DissolvedAt` 时间戳 —— 同样走向软删除。

### D4 — 新异常 `NotRoomHostException`

放在 `Gomoku.Domain/Exceptions/RoomExceptions.cs`(现有 16 个异常的集中文件)。映射 403(与 `NotAPlayerException` 的 403 一致 —— 都是"身份不对"级别的拒绝)。

**为什么不复用 `NotAPlayerException`**:`NotAPlayerException` 的语义是"你甚至都不是玩家",`NotRoomHostException` 是"你是玩家但不是 Host"。两者语义不同,且 Dissolve 的调用者**完全可能是玩家**(本次不允许,但未来若允许"非 Host 玩家在某条件下也能解散"需要区分两种 403)。先分开,以后不怕。

### D5 — SignalR 事件 `RoomDissolved`

`IRoomNotifier.RoomDissolvedAsync(RoomId, CancellationToken)`:向 `room:{id}` group 广播客户端方法 `RoomDissolved`,payload `{ roomId: Guid }`。

**顺序**:Handler 必须 `SaveChangesAsync` **之后**才 Notify —— 若事务失败(比如乐观并发)则**不广播**。这是现有 `IRoomNotifier` 使用约定(`add-rooms-and-gameplay` 确立),本次完全遵循。

**group 清理**:SignalR 的 group 在最后一个成员离开后自动回收,无需显式。

**被解散房间的 SignalR 连接**:Hub 侧连接本身仍然活着(连到服务的 /hubs/gomoku),只是该 group 没了;前端收到 `RoomDissolved` 后应自行调 `LeaveRoom` 清掉 group 订阅,或简单地跳转回 room list —— 是前端职责,**后端不强制 `Groups.RemoveFromGroupAsync`**(那会要求枚举 connection id,复杂化无意义)。

### D6 — `IRoomRepository.DeleteAsync`

新签名:`Task DeleteAsync(Room room, CancellationToken cancellationToken);`

实现:`_db.Rooms.Remove(room);`,然后**不调** `SaveChanges`(交给 handler 的 `IUnitOfWork.SaveChangesAsync`,维持"仓储不提交"的约定)。

**考虑过但弃用**:`DeleteByIdAsync(RoomId)` —— 可以免加载整个聚合,直接 `ExecuteDeleteAsync`。但 Domain 校验要先运行(`Room.Dissolve` 依赖加载到的 Host 字段),所以无论如何要 Load;加个 ExecuteDelete 的意义不大。且 `DeleteAsync(Room)` 和现有 `AddAsync(Room)` 对称。

### D7 — API 端点

`DELETE /api/rooms/{id}` on `RoomsController`,`[Authorize]`,成功 `204 No Content`。

**为什么是 DELETE 而非 POST `/dissolve`**:
- 语义上就是"删除这个资源";
- REST 风格;
- 前端 `fetch('/api/rooms/xxx', { method: 'DELETE' })` 直观;
- 不需要 body —— `Room.Status` 就是唯一校验条件,不支持"删除原因"等附加字段。

### D8 — HTTP 错误映射

新增在全局中间件:

| 异常 | HTTP |
|---|---|
| `NotRoomHostException` | 403 |

现有映射复用:

| 异常 | HTTP |
|---|---|
| `RoomNotFoundException` | 404 |
| `RoomNotWaitingException` | 409 |

## Risks / Trade-offs

| 风险 | 影响 | 缓解 |
|---|---|---|
| Host 解散时有围观者还在聊天,聊天消息随房一起消失 | 围观体验略差 | Waiting 房间的聊天本就不算"正式记录";用户教育"解散即清空"由前端文案承担 |
| EF Cascade 失效(未来若有人改配置) | `Remove` 因 FK 抛异常,事务回滚 | 现有 spec 已明确 Cascade;加一个单元测试覆盖"解散带聊天 / 带围观者的 Waiting 房"以锁死行为 |
| 竞态:多个 DELETE 同时到 | 第二条进入时 room 已删,`FindByIdAsync` 返回 null → `RoomNotFoundException` 404 | 当作正常状态;前端收到 404 当作"已被别人操作完毕" |
| SignalR RoomDissolved 广播到空 group(围观者早已离开) | 无副作用 | SignalR 的 `SendAsync` 对空 group 是 no-op,不报错 |

## Migration Plan

无 migration。

启动时依赖:`AddRoomsAndGameplay` migration 已经建立了 `Rooms` / `Games` / `Moves` / `RoomSpectators` / `ChatMessages` 的 FK with Cascade;本次没有 schema 变化。

## Open Questions

无 —— 作用域小到位,所有选择都能从现有 spec 和前几次 design 的惯例推出。
