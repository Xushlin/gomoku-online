## 1. Domain — `GameEndReason` 枚举 + `Game.EndReason` 字段

- [x] 1.1 `Gomoku.Domain/Enums/GameEndReason.cs`:`public enum GameEndReason { Connected5 = 0, Resigned = 1, TurnTimeout = 2 }` + XML 注释。
- [x] 1.2 `Gomoku.Domain/Rooms/Game.cs` 加 `public GameEndReason? EndReason { get; private set; }`;`FinishWith` 签名追加参数 `GameEndReason reason`;所有 `FinishWith` 调用点(`Room.PlayMove` 内部)传 `GameEndReason.Connected5`。

## 2. Domain — 新异常 + `GameEndOutcome`

- [x] 2.1 `RoomExceptions.cs` 追加 `TurnNotTimedOutException`(HTTP 409,消息指出"回合尚未超过阈值")。
- [x] 2.2 `Gomoku.Domain/Rooms/Room.cs`(文件级)加 `public sealed record GameEndOutcome(GameResult Result, UserId? WinnerUserId);`(跟现有 `MoveOutcome` 同文件,对称);XML 注释说明是 `Resign` / `TimeOutCurrentTurn` 的返回值。

## 3. Domain — `Room.Resign`

- [x] 3.1 `public GameEndOutcome Resign(UserId userId, DateTime now)`:
  - `Status != Playing` → `RoomNotInPlayException`
  - `Game is null`(防御)→ `RoomNotInPlayException`
  - `userId != BlackPlayerId && userId != WhitePlayerId` → `NotAPlayerException`
  - 推导 opponent 棋色与 UserId;设 `Game.FinishWith(opponentResult, opponentUserId, GameEndReason.Resigned, now)`;`TransitionStatus(Finished)`
  - 返回 `GameEndOutcome(opponentResult, opponentUserId)`

## 4. Domain — `Room.TimeOutCurrentTurn`

- [x] 4.1 `public GameEndOutcome TimeOutCurrentTurn(DateTime now, int turnTimeoutSeconds)`:
  - `Status != Playing` / `Game is null` → `RoomNotInPlayException`
  - `turnTimeoutSeconds < 1` → `ArgumentOutOfRangeException`
  - `lastActivity = Game.Moves.OrderBy(Ply).LastOrDefault()?.PlayedAt ?? Game.StartedAt`
  - `(now - lastActivity).TotalSeconds < turnTimeoutSeconds` → `TurnNotTimedOutException`
  - 当前回合棋色 = `Game.CurrentTurn`,loser 对应 UserId;opponent 色胜
  - `Game.FinishWith(opponentResult, opponentUserId, GameEndReason.TurnTimeout, now)`
  - `TransitionStatus(Finished)`
  - 返回 `GameEndOutcome(opponentResult, opponentUserId)`

## 5. Domain 测试

- [x] 5.1 `Gomoku.Domain.Tests/Rooms/RoomResignTests.cs`(~6):
  - Black 认输 → `WhiteWin`,`WinnerUserId == whiteId`,`Game.EndReason == Resigned`,`Status == Finished`
  - White 认输 → `BlackWin`
  - 不是自己回合也能认输(White 在黑方回合认输)
  - `Status == Waiting` 调 → `RoomNotInPlayException`
  - `Status == Finished` 调 → `RoomNotInPlayException`
  - 非玩家调 → `NotAPlayerException`
- [x] 5.2 `Gomoku.Domain.Tests/Rooms/RoomTimeOutTests.cs`(~7):
  - Black 超时(`currentTurn=Black`,`now - lastMoveAt = 61s`,`timeout=60`)→ `WhiteWin`,`EndReason == TurnTimeout`,`Status == Finished`
  - White 超时(构造黑先走一步,白 61s 不动)→ `BlackWin`
  - 尚未超时(59s) → `TurnNotTimedOutException`
  - 边界:`(now - lastActivity).TotalSeconds == timeout` 恰好 → **成功**(用 `>=`)
  - `Status != Playing` → `RoomNotInPlayException`
  - `turnTimeoutSeconds == 0` → `ArgumentOutOfRangeException`
  - 无 Moves(对局刚开始 10s,`lastActivity = StartedAt`)→ 若 `now - StartedAt >= timeout` 成功
- [x] 5.3 `Gomoku.Domain.Tests/Rooms/GameFinishWithEndReasonTests.cs`(~3):
  - `FinishWith(WhiteWin, id, Resigned, now)` → `EndReason == Resigned`,其他字段一致
  - 进行中 `EndReason == null`
  - 各 reason 的传入 / 读出一致
- [x] 5.4 更新 `RoomMakeMoveTests`(现有)—— 只需确认 `Game.EndReason == Connected5` 在连五路径后为真;添加 1 个断言。
- [x] 5.5 `dotnet test tests/Gomoku.Domain.Tests` 全绿。

## 6. Application — 抽象扩展

- [x] 6.1 `IRoomRepository` 追加 `Task<IReadOnlyList<RoomId>> GetRoomsWithExpiredTurnsAsync(DateTime now, int turnTimeoutSeconds, CancellationToken cancellationToken);`。XML 注释指明返回 `Status=Playing` 且 `max(moves.PlayedAt, game.StartedAt) + timeout <= now` 的房间 Id。

## 7. Application — `GameOptions`

- [x] 7.1 `Gomoku.Application/Abstractions/GameOptions.cs`:
  ```
  public sealed class GameOptions
  {
      [Range(10, 3600)]
      public int TurnTimeoutSeconds { get; set; } = 60;
      [Range(1000, 60000)]
      public int TimeoutPollIntervalMs { get; set; } = 5000;
  }
  ```

## 8. Application — 共享 helper

- [x] 8.1 `Features/Rooms/Common/GameEloApplier.cs`:抽 `internal static async Task ApplyAsync(Room room, GameResult result, IUserRepository users, CancellationToken ct)`,搬 `MakeMoveCommandHandler.ApplyEloAsync` 的 30 行实现。
- [x] 8.2 `MakeMoveCommandHandler` 改为调用 `GameEloApplier.ApplyAsync`;删除 private `ApplyEloAsync`;确认现有 tests 仍通过(行为不变)。

## 9. Application — Resign feature

- [x] 9.1 `Features/Rooms/Resign/ResignCommand.cs`:`record ResignCommand(UserId UserId, RoomId RoomId) : IRequest<GameEndedDto>`。
- [x] 9.2 `Features/Rooms/Resign/ResignCommandHandler.cs`:
  - Load room(null → `RoomNotFoundException`)
  - `var outcome = room.Resign(UserId, _clock.UtcNow);`
  - `await GameEloApplier.ApplyAsync(room, outcome.Result, _users, ct);`
  - `await _uow.SaveChangesAsync(ct);`
  - 构造 `GameEndedDto(outcome.Result, outcome.WinnerUserId?.Value, room.Game!.EndedAt!.Value, room.Game.EndReason!.Value);`(含 `EndReason`)
  - 先 `RoomStateChangedAsync`,再 `GameEndedAsync`(同 MakeMoveCommandHandler 的时序)
  - 返回 `GameEndedDto`

## 10. Application — TurnTimeout feature(内部)

- [x] 10.1 `Features/Rooms/TurnTimeout/TurnTimeoutCommand.cs`:`record TurnTimeoutCommand(RoomId RoomId) : IRequest<Unit>`。
- [x] 10.2 `Features/Rooms/TurnTimeout/TurnTimeoutCommandHandler.cs`:
  - 依赖 `IRoomRepository` / `IUserRepository` / `IUnitOfWork` / `IRoomNotifier` / `IDateTimeProvider` / `IOptions<GameOptions>`
  - Load room(null → `RoomNotFoundException`)
  - `var outcome = room.TimeOutCurrentTurn(_clock.UtcNow, _opts.Value.TurnTimeoutSeconds);`
  - 后续同 ResignCommandHandler(ELO + SaveChanges + 通知)
  - 返回 `Unit.Value`
  - **不**校验调用方身份 —— 本命令只被 worker 发,worker 无 UserId 上下文

## 11. DTO 扩展

- [x] 11.1 `Common/DTOs/RoomDtos.cs` 里 `GameSnapshotDto` 追加:
  - `DateTime TurnStartedAt`
  - `int TurnTimeoutSeconds`
  - `GameEndReason? EndReason`
- [x] 11.2 `GameEndedDto` 追加 `GameEndReason EndReason`(非 nullable,结束时必有)。
- [x] 11.3 `RoomMapping.ToState`:
  - 注入 `GameOptions`(通过新参数 `int turnTimeoutSeconds` 或 `IOptions<GameOptions>` —— **选前者**,保持 mapping pure);
  - `TurnStartedAt = game.Moves.OrderBy(m => m.Ply).LastOrDefault()?.PlayedAt ?? game.StartedAt`;
  - 传入 GameSnapshotDto;
  - 调用点(5 个 handler)都要传 `GameOptions.TurnTimeoutSeconds`;增开一个扩展方法 `ToState(room, usernames, turnTimeoutSeconds)`,老签名保留为 `ToState(room, usernames)` + 转发调用(默认 60)以防调用方漏传 —— 审核后决定是否保留 overload。

## 12. Application 测试

- [x] 12.1 `Features/Rooms/ResignCommandHandlerTests.cs`(~4):
  - 成功路径(Black 认输):`Room.Resign` 效果(通过聚合验证)+ Domain 测试已覆盖,此处验证 handler 级别:`IUserRepository.FindByIdAsync` 对两位 User 各调一次(ELO 加载)、`SaveChangesAsync` 一次、`RoomStateChangedAsync` + `GameEndedAsync` 顺序调用、`GameEndedDto.EndReason == Resigned`
  - Room 不存在 → RoomNotFoundException
  - 非玩家调用 → NotAPlayerException(Domain 透传)
  - Finished 调用 → RoomNotInPlayException(透传)
- [x] 12.2 `Features/Rooms/TurnTimeoutCommandHandlerTests.cs`(~4):
  - 成功路径:mock `_clock.UtcNow` 为 "超时后时刻";断言 ELO / Save / Notify 都调;`EndReason == TurnTimeout`
  - Room 不存在 → `RoomNotFoundException`(worker 捕获后吞)
  - Not yet timed out → `TurnNotTimedOutException`(worker 捕获后吞);Save / Notify 均未调
  - Finished → `RoomNotInPlayException`
- [x] 12.3 `Features/Rooms/MakeMoveCommandHandlerTests.cs` 已存在,确认重构后(改为 GameEloApplier)tests 仍绿。

## 13. Infrastructure — migration

- [x] 13.1 `dotnet ef migrations add AddGameEndReason --project src/Gomoku.Infrastructure --startup-project src/Gomoku.Api --output-dir Persistence/Migrations`
- [x] 13.2 修改生成的 migration:
  - Up:`migrationBuilder.AddColumn<int>("EndReason", "Games", nullable: true);`
  - Up 后附加 SQL 回填:`migrationBuilder.Sql("UPDATE Games SET EndReason = 0 WHERE Result IS NOT NULL;");`
  - Down:`migrationBuilder.DropColumn("EndReason", "Games");`
- [x] 13.3 `GameConfiguration` 加 `builder.Property(g => g.EndReason);`(可选列,EF 会识别为 nullable int)。
- [x] 13.4 `dotnet ef database update` 在本地 SQLite 应用;确认 `Games.EndReason` 列存在,老 Finished 行值为 0。

## 14. Infrastructure — 仓储扩展 + worker

- [x] 14.1 `RoomRepository.GetRoomsWithExpiredTurnsAsync(DateTime now, int turnTimeoutSeconds, CancellationToken ct)`:
  ```csharp
  var cutoff = now.AddSeconds(-turnTimeoutSeconds);
  return await _db.Rooms
      .Where(r => r.Status == RoomStatus.Playing && r.Game != null)
      .Where(r => (r.Game!.Moves.Max(m => (DateTime?)m.PlayedAt) ?? r.Game!.StartedAt) <= cutoff)
      .Select(r => r.Id)
      .ToListAsync(ct);
  ```
  (测试 EF 能否翻译这个 Max 表达式 —— 若不行,改写成 navigation 子查询;实在不行,做两步:先拿 Playing rooms,再内存过滤 —— 保持功能正确,量大再优化。)
- [x] 14.2 `BackgroundServices/TurnTimeoutWorker.cs`(参照 AiMoveWorker 的模板):
  - 依赖 `IServiceScopeFactory` / `IOptions<GameOptions>` / `ILogger<TurnTimeoutWorker>` / `IDateTimeProvider`
  - `ExecuteAsync`:循环 `Task.Delay(opts.TimeoutPollIntervalMs, stopToken)` → CreateScope → 调 `GetRoomsWithExpiredTurnsAsync(clock.UtcNow, opts.TurnTimeoutSeconds, stopToken)` → 对每个 RoomId `ISender.Send(new TurnTimeoutCommand(roomId), stopToken)` → try/catch 除 OperationCanceledException 以外全吞 + LogError
- [x] 14.3 DI:`services.AddHostedService<TurnTimeoutWorker>()` + `AddOptions<GameOptions>().Bind("Game").ValidateDataAnnotations().ValidateOnStart()`。

## 15. Api — endpoint + 映射 + mapping

- [x] 15.1 `RoomsController` 加 action:
  ```csharp
  [HttpPost("{id:guid}/resign")]
  public async Task<ActionResult<GameEndedDto>> Resign(Guid id, CancellationToken ct)
  {
      var dto = await _mediator.Send(new ResignCommand(GetUserId(), new RoomId(id)), ct);
      return Ok(dto);
  }
  ```
- [x] 15.2 `ExceptionHandlingMiddleware`:`TurnNotTimedOutException` 加入 `RoomNotWaitingException` / `RoomNotInPlayException` 等的 409 合集。
- [x] 15.3 `appsettings.json` 追加 `"Game": { "TurnTimeoutSeconds": 60, "TimeoutPollIntervalMs": 5000 }`(顶层段,**保留**已有 `"Ai"` 段)。
- [x] 15.4 `Program.cs`:注册 GameOptions / TurnTimeoutWorker(在 `AddInfrastructure` 里或 `Program.cs` 本身,保持和 AiMoveWorker 风格对称)。

## 16. 端到端冒烟

- [x] 16.1 启动 Api,注册 Alice / Bob。
- [x] 16.2 **认输路径**:Alice 创建房间,Bob join,黑方(Alice)走 1–2 步。Bob 调 `POST /api/rooms/{id}/resign` → 200 + `GameEndedDto { Result: BlackWin, EndReason: Resigned }`。`GET /api/users/me`(Alice):Rating 有涨幅,`Wins == 1`。
- [x] 16.3 **超时路径**(用 `TurnTimeoutSeconds=3`、`TimeoutPollIntervalMs=1000` 临时跑 dev):Alice 创建房间,Bob join,黑方(Alice)走 1 步,等 4 秒。服务日志应见 TurnTimeoutWorker 吃到该房间,SignalR 推 `GameEnded { Result: WhiteWin, EndReason: TurnTimeout }`。
- [x] 16.4 **DTO 校验**:`GET /api/rooms/{id}`(Playing 中)返回的 `game.turnStartedAt` / `game.turnTimeoutSeconds` 非空;Finished 后 `endReason` 非空。

## 17. 归档前置检查

- [x] 17.1 `dotnet build Gomoku.slnx`:0 警告 / 0 错。
- [x] 17.2 `dotnet test Gomoku.slnx`:全绿(Domain 195 + 约 16 = ~211;Application 81 + 约 8 = ~89)。
- [x] 17.3 Domain csproj 仍 0 PackageReference / 0 ProjectReference。
- [x] 17.4 Application csproj 无 EF / Hosting / Hub 新依赖(只新增 DataAnnotations,已存在)。
- [x] 17.5 `openspec validate add-timeout-resign --strict`:valid。
- [x] 17.6 分支 `feat/add-timeout-resign`,按层分组 commit(Domain / Application / Infrastructure / Api / docs-openspec 五条)。
