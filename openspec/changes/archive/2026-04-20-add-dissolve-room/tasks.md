## 1. Domain — 新异常

- [x] 1.1 `Gomoku.Domain/Exceptions/RoomExceptions.cs` 追加 `NotRoomHostException`(sealed, 继承 Exception,`(string message)` 构造)。XML 注释指出"调用方不是 Host,映射 HTTP 403"。

## 2. Domain — `Room.Dissolve`

- [x] 2.1 在 `Room.cs` 加 `public void Dissolve(UserId senderId)`:
  - `senderId != HostUserId` → `NotRoomHostException`
  - `Status != Waiting` → `RoomNotWaitingException`
  - 通过即返回,不改字段
  - XML 注释明示"只校验,物理删除由仓储层完成"

## 3. Domain 测试

- [x] 3.1 `Gomoku.Domain.Tests/Rooms/RoomDissolveTests.cs`:
  - Host + Waiting → 不抛
  - 非 Host(另一 UserId) + Waiting → `NotRoomHostException`
  - Host + Playing → `RoomNotWaitingException`
  - Host + Finished → `RoomNotWaitingException`(构造 Finished:走正常 `JoinAsPlayer` + `PlayMove` 打到连五)
  - Host + Waiting(带围观者 2 个 + 带聊天 1 条)→ 不抛
  - Waiting 时 White 玩家不存在,所以"其他玩家解散"的场景退化为"非 Host 解散",覆盖同上

- [x] 3.2 `dotnet test tests/Gomoku.Domain.Tests --filter "FullyQualifiedName~RoomDissolveTests"` 全绿。

## 4. Application — 抽象扩展

- [x] 4.1 `IRoomRepository` 追加 `Task DeleteAsync(Room room, CancellationToken cancellationToken);`。XML 注释说"仅标记删除,不 SaveChanges"。
- [x] 4.2 `IRoomNotifier` 追加 `Task RoomDissolvedAsync(RoomId roomId, CancellationToken cancellationToken);`。XML 注释说"向 room:{id} group 广播,payload 仅含 roomId"。

## 5. Application — Feature

- [x] 5.1 `Features/Rooms/Dissolve/DissolveRoomCommand.cs`:`record DissolveRoomCommand(UserId SenderUserId, RoomId RoomId) : IRequest<Unit>`。
- [x] 5.2 `Features/Rooms/Dissolve/DissolveRoomCommandHandler.cs`:
  - Load room(null → `RoomNotFoundException`)
  - `room.Dissolve(request.SenderUserId)`
  - `await _rooms.DeleteAsync(room, ct)`
  - `await _uow.SaveChangesAsync(ct)`
  - `await _notifier.RoomDissolvedAsync(request.RoomId, ct)`
  - 返回 `Unit.Value`

## 6. Application 测试

- [x] 6.1 `Features/Rooms/DissolveRoomCommandHandlerTests.cs`:
  - 成功:Host 解散 Waiting → `DeleteAsync` 一次、`SaveChangesAsync` 一次、`RoomDissolvedAsync` 一次
  - 房间不存在 → `RoomNotFoundException`
  - 非 Host 调用 → `NotRoomHostException`(Domain 透传)
  - Room 在 Playing → `RoomNotWaitingException`(Domain 透传);且 `DeleteAsync` / `SaveChanges` / Notifier 均**未**被调用
- [x] 6.2 `dotnet test tests/Gomoku.Application.Tests` 全绿。

## 7. Infrastructure

- [x] 7.1 `RoomRepository.cs` 实现 `DeleteAsync(Room room, CancellationToken ct)`:`_db.Rooms.Remove(room); return Task.CompletedTask;`(同步操作包成 Task)。
- [x] 7.2 `SignalRRoomNotifier.cs` 实现 `RoomDissolvedAsync`:`_hub.Clients.Group($"room:{roomId.Value}").SendAsync("RoomDissolved", new { RoomId = roomId.Value }, ct);`。
- [x] 7.3 `dotnet build src/Gomoku.Infrastructure` 0 错。

## 8. Api

- [x] 8.1 `Controllers/RoomsController.cs` 加 action:
  ```csharp
  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> Dissolve(Guid id, CancellationToken ct)
  {
      await _sender.Send(new DissolveRoomCommand(GetCurrentUserId(), new RoomId(id)), ct);
      return NoContent();
  }
  ```
- [x] 8.2 `Middleware/ExceptionHandlingMiddleware.cs`:加 `NotRoomHostException` → 403 条目;复用现有 `ProblemDetails` 生成。

## 9. 端到端冒烟

- [x] 9.1 启动 Api,注册真人 Alice。
- [x] 9.2 `POST /api/rooms { name: "to-dissolve" }` → 201 + RoomSummaryDto(id)。
- [x] 9.3 `DELETE /api/rooms/{id}` with Alice token → 204。
- [x] 9.4 `GET /api/rooms/{id}` → 404 `RoomNotFoundException`。
- [x] 9.5 `GET /api/rooms` → 不含刚刚的房间。
- [x] 9.6 (可选)跑一个 SignalR harness 验证 `RoomDissolved` 事件被广播。本变更小,9.6 改为"手工 curl + 观察日志"即可。

## 10. 归档前置检查

- [x] 10.1 `dotnet build Gomoku.slnx`:0 警告 / 0 错。
- [x] 10.2 `dotnet test Gomoku.slnx`:全绿(预计 Domain 189 + 6 = 195;Application 77 + 4 = 81)。
- [x] 10.3 Domain csproj 仍 0 PackageReference / 0 ProjectReference。
- [x] 10.4 Application csproj 无 EF / Hosting / Hub 新增依赖。
- [x] 10.5 `openspec validate add-dissolve-room --strict`:valid。
- [x] 10.6 分支 `feat/add-dissolve-room`,按层分组 commit(Domain / Application / Infrastructure / Api / docs-openspec 五条)。
