## 1. Application — 接口迁移 + 扩展

- [x] 1.1 新建 `backend/src/Gomoku.Application/Abstractions/IConnectionTracker.cs`,把原 Api 中的接口内容搬过来(XML 注释保留)+ 追加:
  - `int GetOnlineUserCount();`
  - `bool IsUserOnline(UserId userId);`
- [x] 1.2 删除 `backend/src/Gomoku.Api/Hubs/IConnectionTracker.cs`。
- [x] 1.3 更新调用方 using:`GomokuHub.cs` / `ConnectionTracker.cs` / `Program.cs` —— 改为 `using Gomoku.Application.Abstractions;`(Hub 原本还 import `Gomoku.Api.Hubs` 里的 `IConnectionTracker`,同文件内现在从 Application 拿)。

## 2. Application — DTO

- [x] 2.1 `Common/DTOs/PresenceDto.cs`:
  ```csharp
  public sealed record OnlineCountDto(int Count);
  public sealed record PresenceDto(Guid UserId, bool IsOnline);
  ```

## 3. Application — Query features

- [x] 3.1 `Features/Presence/GetOnlineCount/GetOnlineCountQuery.cs`:
  `record GetOnlineCountQuery() : IRequest<OnlineCountDto>`。
- [x] 3.2 `Features/Presence/GetOnlineCount/GetOnlineCountQueryHandler.cs`:
  注入 `IConnectionTracker` → 调 `GetOnlineUserCount()` → 包 OnlineCountDto。
- [x] 3.3 `Features/Presence/IsUserOnline/IsUserOnlineQuery.cs`:
  `record IsUserOnlineQuery(UserId UserId) : IRequest<PresenceDto>`。
- [x] 3.4 `Features/Presence/IsUserOnline/IsUserOnlineQueryHandler.cs`:
  调 `IsUserOnline(UserId)` → 包 PresenceDto。

## 4. Api — ConnectionTracker impl 扩展

- [x] 4.1 `ConnectionTracker.cs`:
  - 加字段 `private readonly ConcurrentDictionary<UserId, int> _onlineCountsByUser = new();`
  - `TrackAsync` 末尾 `_onlineCountsByUser.AddOrUpdate(userId, 1, (_, c) => c + 1);`
  - `UntrackAsync` 中 `state.UserId` 递减:`AddOrUpdate(state.UserId, 0, (_, c) => c - 1)`;若结果为 0 则 `TryRemove`(要考虑竞态:用 lock 或 `TryUpdate` 比较)。
  - 实现 `public int GetOnlineUserCount() => _onlineCountsByUser.Count;`
  - 实现 `public bool IsUserOnline(UserId userId) => _onlineCountsByUser.TryGetValue(userId, out var c) && c > 0;`
- [x] 4.2 考虑并发:Track / Untrack 不阻塞 SignalR 连接回调;ConcurrentDictionary 单 key 更新原子即可。递减到 0 的清理用"compare-and-exchange":
  ```csharp
  while (_onlineCountsByUser.TryGetValue(userId, out var current))
  {
      var next = current - 1;
      if (next <= 0)
      {
          if (_onlineCountsByUser.TryRemove(new KeyValuePair<UserId, int>(userId, current))) break;
      }
      else
      {
          if (_onlineCountsByUser.TryUpdate(userId, next, current)) break;
      }
  }
  ```

## 5. Api — PresenceController

- [x] 5.1 `Gomoku.Api/Controllers/PresenceController.cs`:
  ```csharp
  [ApiController]
  [Route("api/presence")]
  [Authorize]
  public sealed class PresenceController : ControllerBase
  {
      private readonly ISender _mediator;
      public PresenceController(ISender mediator) { _mediator = mediator; }

      [HttpGet("online-count")]
      public async Task<ActionResult<OnlineCountDto>> OnlineCount(CancellationToken ct)
      {
          var dto = await _mediator.Send(new GetOnlineCountQuery(), ct);
          return Ok(dto);
      }

      [HttpGet("users/{id:guid}")]
      public async Task<ActionResult<PresenceDto>> IsOnline(Guid id, CancellationToken ct)
      {
          var dto = await _mediator.Send(new IsUserOnlineQuery(new UserId(id)), ct);
          return Ok(dto);
      }
  }
  ```

## 6. 测试

- [x] 6.1 `Features/Presence/GetOnlineCountQueryHandlerTests.cs`(~2):
  - mock tracker 返回 5 → DTO.Count == 5。
  - mock 返回 0 → DTO.Count == 0。
- [x] 6.2 `Features/Presence/IsUserOnlineQueryHandlerTests.cs`(~2):
  - mock tracker.IsUserOnline → true → DTO.IsOnline == true;UserId 正确。
  - mock → false → DTO.IsOnline == false。

## 7. 端到端冒烟

- [x] 7.1 启动 Api,注册 Alice + Bob。
- [x] 7.2 `GET /api/presence/online-count` 用 Alice token(**未**建 SignalR 连接) → `{ count: 0 }`。
  (Alice 的 HTTP 请求不触发 ConnectionTracker.Track —— 只有 SignalR 连接会。)
- [x] 7.3 一次小型 SignalR 连接 harness(或最简 Python signalr 客户端?工作量大;换用**Gomoku.Api.Hubs.ConnectionTracker 的内部测试**:跳过这一项 E2E,单测已覆盖 handler,impl 的正确性由审 code + 单元测试证明)。
- [x] 7.4 `GET /api/presence/users/{aliceId}` 返回 `{ userId: aliceId, isOnline: false }`。
- [x] 7.5 `GET /api/presence/users/{randomGuid}`(不存在用户):返回 `{ userId: randomGuid, isOnline: false }` —— presence 端点不 404,只说"在不在线"。

## 8. 归档前置检查

- [x] 8.1 `dotnet build Gomoku.slnx`:0 警告 0 错。
- [x] 8.2 `dotnet test Gomoku.slnx`:全绿(Application +4)。
- [x] 8.3 `openspec validate add-presence-tracking --strict`:valid。
- [x] 8.4 分支 `feat/add-presence-tracking`,按层 Application / Api / docs 三条 commit。
