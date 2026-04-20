## Why

`Room.Leave` 在 Waiting 状态下对 Host 会抛 `HostCannotLeaveWaitingRoomException`,消息写着 "dissolve it instead";但**"解散"的端点从未实现** —— 是一条死胡同。现在玩家创建了房间却没人加入,他想"撤销",系统只能拒绝他离开,等房间"自然老去"也没有清理路径,前端要么隐藏"离开"按钮,要么暴露一个会报错的按钮。

这次变更补上缺失的另一端:一个真正能让 Host 销毁自己创建的 Waiting 房间的入口。作用域**严格**限定在 Waiting —— Playing 状态下的"中途认输 / 超时判负"语义复杂得多(需要判胜、ELO 结算、对手通知),留给 `add-timeout-resign` 覆盖。本次变更只要堵住"创建了一个房、没人来,无法撤销"的缺口。

顺手把围观 Waiting 房的观众也处理了:房间消失前广播一个 `RoomDissolved` SignalR 事件,让他们的前端知道"这个房间不要再渲染了"。

## What Changes

- **Domain**:
  - 新异常 `NotRoomHostException`(放 `RoomExceptions.cs`):操作要求调用方是 Host,但不是。Api 层映射 403。
  - 新方法 `Room.Dissolve(UserId senderId)`:
    - 若 `senderId != HostUserId` → 抛 `NotRoomHostException`。
    - 若 `Status != Waiting` → 抛 `RoomNotWaitingException`(复用现有异常,HTTP 409)。
    - 校验通过则**不修改聚合状态**(物理删除在仓储层完成;Domain 方法只"祝福"这次删除,保证不变量)。
- **Application**:
  - 新命令 `DissolveRoomCommand(UserId SenderUserId, RoomId RoomId) : IRequest<Unit>`。
  - `DissolveRoomCommandHandler`:Load → `Room.Dissolve(senderId)`(校验) → `IRoomRepository.DeleteAsync` → `SaveChangesAsync` → `IRoomNotifier.RoomDissolvedAsync(roomId)`。
  - `IRoomRepository` 新方法 `Task DeleteAsync(Room room, CancellationToken ct)`。
  - `IRoomNotifier` 新方法 `Task RoomDissolvedAsync(RoomId roomId, CancellationToken ct)`。
- **Infrastructure**:
  - `RoomRepository.DeleteAsync`:`_db.Rooms.Remove(room)` —— Game / Moves / Spectators / ChatMessages 已在 EF config 设 `OnDelete(Cascade)`,自动随根删除,**无需 migration**。
  - `SignalRRoomNotifier.RoomDissolvedAsync`:向 `room:{id}` group 广播 `RoomDissolved` 事件(payload: `{ roomId }`)。
- **Api**:
  - `RoomsController` 新 action:`DELETE /api/rooms/{id}` → `DissolveRoomCommand`,成功 `204 No Content`。
  - `ExceptionHandlingMiddleware` 新条目:`NotRoomHostException` → 403。
- **Tests**:
  - `Gomoku.Domain.Tests/Rooms/RoomDissolveTests.cs`:Host + Waiting 成功;非 Host 抛;Playing 抛 `RoomNotWaitingException`;Finished 抛;Host + 曾有围观者也允许(围观者集合不拦)。~6 tests。
  - `Gomoku.Application.Tests/Features/Rooms/DissolveRoomCommandHandlerTests.cs`:成功路径(Delete + Save + Notifier 各一次);Room 不存在抛 `RoomNotFoundException`;非 Host 抛 `NotRoomHostException`;Playing 抛(Domain 异常透传)。~4 tests。

**显式不做**(留给后续变更):
- Playing / Finished 状态的"解散"—— Playing 的语义应该是"认输或超时判负",归 `add-timeout-resign`;Finished 房间的清理留给"定时归档" worker,不属于 Host 操作。
- 软删除(`DissolvedAt` 时间戳 + 查询过滤):Waiting 房间无 Game、无 Move、无聊天内容,**无审计价值**,硬删更简单;将来确需审计可由 EF Interceptor 加。
- 解散同时把房间从 `GetActiveRoomsAsync` 列表"优雅淡出":已在 SignalR 侧广播 `RoomDissolved`,前端的在列表上主动筛掉该 Id 即可;下次 `GET /api/rooms` 天然不返回已删除的房间。
- AI 房间的 Host 解散:`CreateAiRoom` 后房间直接进 Playing,Host 解散路径通不过"Status == Waiting"检查 —— 用户体感就是"AI 局开了就开了,只能下完或等 B 的认输能力"。不属本次范围。

## Capabilities

### New Capabilities

(无)

### Modified Capabilities

- **`room-and-gameplay`** — `Room.Leave` 对 Waiting Host 的错误信息终于"言之有物"(指向真端点);`Room` 聚合加 `Dissolve` 方法;新端点 `DELETE /api/rooms/{id}`;新 SignalR 事件 `RoomDissolved`;异常 `NotRoomHostException` 及其 HTTP 映射。

## Impact

- **代码规模**:~12 新文件(含测试)+ 少量现有文件修改。是目前为止最小的一次变更(比 `add-elo-system` 还小)。
- **NuGet**:零。
- **HTTP 表面**:+1 端点 `DELETE /api/rooms/{id}`。
- **SignalR 表面**:+1 服务端事件 `RoomDissolved(roomId)`。
- **数据库**:零 schema 变化;运行时会真删行,依赖现有 Cascade 配置。
- **后续变更将依赖**:`add-timeout-resign` 在 Playing 状态下的"提前结束"语义会用到类似的"房间删除 + 广播"骨架,但走的是 Finished 而非删除。
