## 1. Domain — `IsBot` & `RegisterBot`

- [x] 1.1 在 `Gomoku.Domain/Users/User.cs` 加 `public bool IsBot { get; private set; }`;`User.Register` 在初始化对象时显式设 `IsBot = false`(写出来更直观,编译出来等价)。
- [x] 1.2 在 `User` 上加 `public const string BotPasswordHashMarker = "__BOT_NO_LOGIN__";`。
- [x] 1.3 在 `User` 上新增静态工厂 `RegisterBot(UserId id, Email email, Username username, DateTime createdAt)`,字段:`PasswordHash=BotPasswordHashMarker`、`Rating=1200`、计数器 0、`IsActive=true`、`IsBot=true`、`CreatedAt=createdAt`。XML 注释指明"不接受 passwordHash 参数 —— bot 永远不可登录"。
- [x] 1.4 Domain 单元测试 `Users/UserRegisterBotTests.cs`:3–5 个用例(全部字段初值、`IsBot==true`、`PasswordHash` 等于常量、不同时间戳 / UserId 场景)。

## 2. Domain — `BotDifficulty` / `IGomokuAi`

- [x] 2.1 建目录 `backend/src/Gomoku.Domain/Ai/`。
- [x] 2.2 `Ai/BotDifficulty.cs`:`public enum BotDifficulty { Easy = 0, Medium = 1 }` + XML 注释指出未来可能加 `Hard=2`,底层整数值稳定。
- [x] 2.3 `Ai/IGomokuAi.cs`:`Position SelectMove(Board board, Stone myStone)`,XML 注释明示"纯函数、不修改 Board、不使用外部状态;`myStone=Empty` 抛 `ArgumentOutOfRangeException`;满盘抛 `InvalidOperationException`"。

## 3. Domain — `EasyAi`

- [x] 3.1 `Ai/EasyAi.cs`:构造函数 `EasyAi(Random random)`;实现枚举空格 → `random.Next(count)` 挑一个。
- [x] 3.2 Domain 测试 `Ai/EasyAiTests.cs`:
  - 空盘黑方选点合法
  - 固定种子可复现(两个 `EasyAi(new Random(42))` 同盘选同一点)
  - 已有子的格子从不被选(1000 次迭代)
  - 单格棋盘必选该格
  - `myStone=Empty` 抛 `ArgumentOutOfRangeException`
  - 满盘抛 `InvalidOperationException`
  ~6 tests。

## 4. Domain — `MediumAi`

- [x] 4.1 `Ai/MediumAi.cs`:构造函数 `MediumAi(Random random)`。辅助 `EnumerateEmpty(Board)` 返回空格 `IEnumerable<Position>`;`TrialResult(Board, Position, Stone)` 做 `Clone() + PlaceStone` 取 `GameResult`;`score(Position p, Stone me, Board b)` 按 design D5 公式。三层优先级按 spec。
- [x] 4.2 Domain 测试 `Ai/MediumAiTests.cs`:
  - 己方 4 连时选连五点
  - 对手 4 连时堵连五点
  - 己方 / 对手都能连五(罕见),优先选己方
  - 空盘首步选 `(7,7)`(中心偏好)
  - 入参 `Board` 在调用前后字节级相同
  - `myStone=Empty` 抛异常
  ~8 tests。

## 5. Domain — Factory

- [x] 5.1 `Ai/GomokuAiFactory.cs`:`public static IGomokuAi Create(BotDifficulty, Random)`;switch → `EasyAi` / `MediumAi`;default 抛 `ArgumentOutOfRangeException`。
- [x] 5.2 Domain 测试 `Ai/GomokuAiFactoryTests.cs`:三种分支(Easy / Medium / invalid)各一个用例。~3 tests。

- [x] 5.3 验证:`dotnet test tests/Gomoku.Domain.Tests` 全绿(在 167 + ~17 = ~184 基础)。

## 6. Application — `BotAccountIds`

- [x] 6.1 `Gomoku.Application/Abstractions/BotAccountIds.cs`:静态类,`Easy` / `Medium` 两个 `static readonly Guid`(字面量 Guid);`public static Guid For(BotDifficulty)`;default 抛 `ArgumentOutOfRangeException`。

## 7. Application — `IUserRepository` 扩展签名

- [x] 7.1 在 `IUserRepository.cs` 追加:
  - `Task<User?> FindBotByDifficultyAsync(BotDifficulty, CancellationToken)`
  - `Task<IReadOnlyList<RoomId>> GetRoomsNeedingBotMoveAsync(CancellationToken)`
  XML 注释说明排序 / 过滤规则由实现保证;签名无 EF 类型。

## 8. Application — `CreateAiRoom` feature

- [x] 8.1 `Features/Rooms/CreateAiRoom/CreateAiRoomCommand.cs`:`record CreateAiRoomCommand(UserId HostUserId, string Name, BotDifficulty Difficulty) : IRequest<RoomStateDto>`。
- [x] 8.2 `Features/Rooms/CreateAiRoom/CreateAiRoomCommandValidator.cs`:`Name` 规则同 `CreateRoomCommandValidator`。
- [x] 8.3 `Features/Rooms/CreateAiRoom/CreateAiRoomCommandHandler.cs`:按 spec 流程;依赖 `IUserRepository` / `IRoomRepository` / `IUnitOfWork` / `IDateTimeProvider`;不依赖 Notifier(无需广播,因为此时连接尚未加入 SignalR group)。

## 9. Application — `ExecuteBotMove` feature

- [x] 9.1 `Features/Bots/ExecuteBotMove/ExecuteBotMoveCommand.cs`:`record ExecuteBotMoveCommand(UserId BotUserId, RoomId RoomId) : IRequest<Unit>`。
- [x] 9.2 `Features/Bots/ExecuteBotMove/ExecuteBotMoveCommandHandler.cs`:按 spec 流程;依赖 `IRoomRepository` / `IUserRepository` / `ISender`(嵌套 `MakeMoveCommand`)/ `IDateTimeProvider`(worker 可传,handler 读时刻用)。
  - 辅助映射:bot `UserId` → `BotDifficulty`(遍历 `BotAccountIds.Easy/Medium` 比较)。不是 seed 的两者之一 → `ArgumentException`。
- [x] 9.3 handler 内 `workerRandom`:注入 `IAiRandomProvider`(抽象,默认实现 `Random.Shared` 包装);测试注入固定种子。

## 10. Application — Login / Refresh 拒绝 bot

- [x] 10.1 `Features/Auth/Login/LoginCommandHandler.cs`:在 `PasswordHasher.Verify` 返回 `Success` 之后、`IsActive` 检查之前,新增 `if (user.IsBot) throw new InvalidCredentialsException(...)`。
- [x] 10.2 `Features/Auth/Refresh/RefreshTokenCommandHandler.cs`:加载到 `User` 后,新增 `if (user.IsBot) throw new InvalidRefreshTokenException(...)`。
- [x] 10.3 扩展 `LoginCommandHandlerTests`:一个新用例"IsBot 账号即便走到 Verify 也被拒"(用 TestData 构造一个 bot User + 恰好匹配的密码 —— 不可能,因 Hash 是 marker;改为 mock `IPasswordHasher.Verify` 返回 Success,测试的是 handler 本身的 IsBot 分支)。
- [x] 10.4 扩展 `RefreshTokenCommandHandlerTests`:一个新用例"按 token hash 查到的 User 是 IsBot"。

## 11. Application — `AiOptions`

- [x] 11.1 `Gomoku.Application/Abstractions/AiOptions.cs`:`public sealed class AiOptions { public int PollIntervalMs { get; set; } = 1500; public int MinThinkTimeMs { get; set; } = 800; }` + DataAnnotations 验证器或 `IValidateOptions` 拒绝非法值。

## 12. Application — DTO / Common(必要时)

- [x] 12.1 `Common/DTOs/CreateAiRoomRequest.cs` —— 实际上 Api 的 request body 类型放在 `Gomoku.Api/Contracts/` 更合适;若按现有项目风格 DTO 在 Application,创建于此。现有 `RoomStateDto` 已足以做返回。

## 13. Application 测试

- [x] 13.1 `Features/Rooms/CreateAiRoom/CreateAiRoomCommandHandlerTests.cs`:
  - 成功创建(host 真人,bot 存在):`RoomStateDto.Status==Playing`,`WhitePlayerId==BotAccountIds.Medium`;`SaveChangesAsync` 一次
  - Host 不存在:抛 `UserNotFoundException`
  - Bot 不存在:抛 `UserNotFoundException`
  - Host 本人是 bot:抛 `ValidationException`
- [x] 13.2 `Features/Rooms/CreateAiRoom/CreateAiRoomCommandValidatorTests.cs`:Name 空 / 过短 / 过长各一个。
- [x] 13.3 `Features/Bots/ExecuteBotMove/ExecuteBotMoveCommandHandlerTests.cs`:
  - 轮到 bot 走:`ISender.Send` 接收到一条 `MakeMoveCommand(botId, roomId, row, col)`
  - 不轮到 bot:抛 `NotYourTurnException`(worker 吞)
  - Room 不存在:抛 `RoomNotFoundException`
  - Room 已 Finished:抛 `RoomNotInPlayException`
- [x] 13.4 验证:`dotnet test tests/Gomoku.Application.Tests` 全绿(~59 + ~15 = ~74)。

## 14. Infrastructure — EF 映射与仓储

- [x] 14.1 `UserConfiguration.cs`:`builder.Property(u => u.IsBot).IsRequired().HasDefaultValue(false);`。
- [x] 14.2 `UserRepository.cs`:实现 `FindBotByDifficultyAsync`(按 `BotAccountIds.For(difficulty)` 查 `FindByIdAsync` 再校验 `IsBot==true`)、`GetRoomsNeedingBotMoveAsync`(JOIN `Rooms` + `Games` + 两侧 `Users`,过滤 Status=Playing 且 CurrentTurn 一侧 IsBot=true,`Select(r => r.Id)` 返回)。
- [x] 14.3 `UserRepository.GetTopByRatingAsync` 加 `.Where(u => !u.IsBot)`(在 OrderBy 之前)。

## 15. Infrastructure — Migration + Seed

- [x] 15.1 `dotnet ef migrations add AddBotSupport --project src/Gomoku.Infrastructure --startup-project src/Gomoku.Api --output-dir Persistence/Migrations`。
- [x] 15.2 在生成的 migration 里补 `HasData` 两行 bot User(因为 EF `HasData` 需要在 `OnModelCreating` 里声明才能被 migration 捕获,采用**替代方案**:migration 文件里手写 `migrationBuilder.InsertData("Users", ...)`,列名与 OwnsOne 展开的列对得上 —— 此处需小心 `Email_Value` / `Username_Value` 的列名)。
- [x] 15.3 `dotnet ef database update` 应用;SQLite 文件刷到 2 行 IsBot=1 的 User。
- [x] 15.4 回归检查:`dotnet build Gomoku.slnx` 0 警告 / 0 错。

## 16. Infrastructure — `AiMoveWorker`

- [x] 16.1 `BackgroundServices/AiMoveWorker.cs` : `BackgroundService`。构造函数注入 `IServiceScopeFactory` / `IOptions<AiOptions>` / `ILogger<AiMoveWorker>` / `IDateTimeProvider`。
- [x] 16.2 `ExecuteAsync(CancellationToken stopToken)` 按 spec 伪代码实现(每轮 `CreateScope`;try/catch 住非取消异常)。
- [x] 16.3 `Program.cs` 在 `AddInfrastructure` extension 里 `services.AddHostedService<AiMoveWorker>();` + `services.AddOptions<AiOptions>().BindConfiguration("Ai").ValidateDataAnnotations().ValidateOnStart();`。
- [x] 16.4 `appsettings.json` / `appsettings.Development.json` 追加 `"Ai": { "PollIntervalMs": 1500, "MinThinkTimeMs": 800 }`(至少在 Development 里)。

## 17. Infrastructure — 随机源抽象

- [x] 17.1 `Gomoku.Application/Abstractions/IAiRandomProvider.cs`:`Random Get();` —— 保证每次调用都返回"可用随机实例"。
- [x] 17.2 `Gomoku.Infrastructure/Ai/AiRandomProvider.cs`:简单包装 `Random.Shared`。
- [x] 17.3 在 `AddInfrastructure` 注册为 Singleton。

## 18. Api — Endpoint

- [x] 18.1 `Controllers/RoomsController.cs` 加 action `CreateAi`:
  ```
  [HttpPost("ai")]
  public async Task<ActionResult<RoomStateDto>> CreateAi(
      [FromBody] CreateAiRoomRequest req, CancellationToken ct)
  ```
  取 `UserId`(同既有 action 的 `GetCurrentUserId`),发 `CreateAiRoomCommand`,返回 `Created($"/api/rooms/{state.Id}", state)`。
- [x] 18.2 `Contracts/CreateAiRoomRequest.cs`(Api 层):`record CreateAiRoomRequest(string Name, BotDifficulty Difficulty)`。
- [x] 18.3 JsonOptions 确保 `JsonStringEnumConverter` 已全局注册(若未有,在 `Program.cs` 加 `.AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))`)。
- [x] 18.4 手工冒烟:`curl POST /api/rooms/ai` 拿到 201 + 期望 state。

## 19. 端到端冒烟

- [x] 19.1 启动 Api,注册真人 Alice。
- [x] 19.2 `POST /api/rooms/ai { name: "smoke", difficulty: "Easy" }` → 201 + state(WhitePlayerId == BotAccountIds.Easy)。
- [x] 19.3 SignalR 连 `/hubs/gomoku` → `JoinRoom(roomId)`。Alice 落黑 `(7,7)` → 收到 `MoveMade`;**等待 < 3s 内**应收到 bot 的 `MoveMade`(由 worker 发起)。
- [x] 19.4 打到分出胜负(对 EasyAi 人类应很容易赢):收到 `GameEnded`,`WinnerUserId == alice.Id`。
- [x] 19.5 `GET /api/users/me`:Alice 的 `Rating` 升、`Wins==1`。
- [x] 19.6 `GET /api/leaderboard`:Alice 在榜、bot **不在**榜。
- [x] 19.7 复跑一局选 Medium:bot 至少能堵住 "对手四连"(人故意摆个四连看 bot 是否 block)。
- [x] 19.8 写一个小 xUnit integration test(`tests/Gomoku.Api.Tests/` —— 如项目不存在**本次不新建**,留给独立整合测试变更;用 C# console harness 代替)。

## 20. 前置归档检查

- [x] 20.1 `dotnet build Gomoku.slnx`:0 警告 / 0 错。
- [x] 20.2 `dotnet test Gomoku.slnx`:全绿(预计 Domain ~184 + Application ~74 ≈ 258)。
- [x] 20.3 Domain csproj 仍 0 `PackageReference` / 0 `ProjectReference`(AI 类完全靠 BCL)。
- [x] 20.4 Application csproj 仍只依赖 `Gomoku.Domain` + MediatR + FluentValidation + Options;无 EF / Hosting / Hub。
- [x] 20.5 `grep -rn "DateTime\.UtcNow" src/Gomoku.Application src/Gomoku.Domain`:无新增命中。
- [x] 20.6 `openspec validate add-ai-opponent --strict`:valid。
- [x] 20.7 分支 `feat/add-ai-opponent`,按层分组 commit:Domain / Application / Infrastructure(含 migration)/ Api / docs-openspec(含 tasks 更新)—— 5 条 commit + 若冒烟发现问题另补 fix 若干。
