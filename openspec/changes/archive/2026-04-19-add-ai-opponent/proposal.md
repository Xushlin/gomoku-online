## Why

CLAUDE.md 从第一天起就把"人机对战,多难度"列为核心功能;但至今 `Room` 聚合只接纳两位真人 `UserId`,`GomokuHub` 也只路由真人客户端的 `MakeMove`。已归档的四次变更(`add-domain-core` / `add-user-authentication` / `add-rooms-and-gameplay` / `add-elo-system`)把对局链路打通了,但"机器人怎么下子"完全空白。

这次变更的目标是**最小代价地把机器人接入现有对局流程**:把 AI "扮成" 一个真人 —— 用同一个 `UserId` 类型、同一套 `Room.JoinAsPlayer` / `Room.PlayMove` / `IRoomNotifier` 路径,下游 SignalR / ELO / 聊天**零改动**。代价是给 `User` 加一个 `IsBot` 标志和 2 个 seed 的 bot 账号。收益是:

1. 前端要做的事只有"多一个创建 AI 房间的按钮";
2. Hub / 聊天 / 催促 / ELO 都自动继续工作(bot 也会吃到 ELO,只是不上排行榜);
3. 判胜逻辑 0 重写 —— AI 走的每一步都走完整的 `Room.PlayMove`,错步会照常抛 `InvalidMoveException`。

AI 决策方式按用户方向:服务端有一个 `AiMoveWorker` 后台服务,**定期轮询**"有机器人参与、轮到机器人走的" Room,为每个这样的 Room 调 `MakeMoveCommand`。轮询胜出事件驱动的理由(design.md D4):实现上只有一个 `IHostedService`、无需在 `MakeMoveCommandHandler` 里埋 bot 分支、且天然产生"思考延迟"的观感。

## What Changes

- **Domain**:
  - `User` 聚合新增 `IsBot: bool` 只读属性;`User.Register` 保持人类默认(`IsBot=false`);新增静态工厂 `User.RegisterBot(UserId, Email, Username, DateTime)` —— 不要 `passwordHash`,内部用一个永远不可登录的占位常量。
  - 新命名空间 `Gomoku.Domain.Ai/`:
    - `BotDifficulty` 枚举(`Easy=0`、`Medium=1`)。
    - `IGomokuAi` 接口:`Position SelectMove(Board board, Stone myStone)`。
    - `EasyAi` 实现:在空格集合里**均匀随机**取一点(构造时注入 `Random` 以便测试固定种子)。
    - `MediumAi` 实现:三层启发式 —— ① 自己能连五的空点立即选 ② 对手下一子能连五的空点必堵 ③ 其余用 `(中心距离 + 己方已有相邻同色子数)` 打分取最高。
    - `GomokuAiFactory`:按 `BotDifficulty` 返回对应 `IGomokuAi`(构造时注入 `Func<Random>`)。
  - **零 NuGet 新增**,Domain 仍保持 0 第三方依赖。
- **Application**:
  - `IUserRepository` 新增 `Task<User?> FindBotByDifficultyAsync(BotDifficulty difficulty, CancellationToken ct)` —— 由实现层决定是"按 Username 查"还是"按固定 Guid 查"。签名只含领域概念。
  - `IUserRepository.GetRoomsNeedingBotMoveAsync(CancellationToken ct)` —— 返回"`Status=Playing` 且 `CurrentTurn` 的玩家 `IsBot=true`"的房间(不含 Moves 以外的冗余字段)。**这是"查询"而非 EF 细节泄漏**。
  - 新 feature `Features/Rooms/CreateAiRoom/`:`CreateAiRoomCommand(UserId hostId, string name, BotDifficulty difficulty)` + handler + validator。流程:创建房间 → 加载目标 bot → `Room.JoinAsPlayer(botUserId, now)` → SaveChanges 一次 → 返回 `RoomStateDto`。**若当前用户是 bot(防御式),抛 `ValidationException`**。
  - 新 feature `Features/Bots/ExecuteBotMove/`:`ExecuteBotMoveCommand(UserId botUserId, RoomId roomId)` + handler。handler 加载 Room → replay Board → 用 `GomokuAiFactory` 取 AI → 调 `SelectMove` → 发 `MakeMoveCommand` 经 `ISender` 再下发(复用现有落子事务 + 通知)。**这个 command 是给 worker 用的内部命令,不经 REST/SignalR**。
  - `LoginCommandHandler` 扩展:通过密码校验前**如果 `user.IsBot == true`,抛 `InvalidCredentialsException`**(不泄漏"这是机器人账号"语义)。
  - `GetLeaderboardQueryHandler` 通过 `IUserRepository.GetTopByRatingAsync` 的 bot 过滤自动生效(见下)。
- **Infrastructure**:
  - `UserConfiguration`:新增 `IsBot bool NOT NULL DEFAULT 0` 列。
  - `UserRepository.GetTopByRatingAsync` **新增一条 `Where(u => !u.IsBot)`** 过滤(修订 `elo-rating` spec)。
  - `UserRepository.FindBotByDifficultyAsync` / `GetRoomsNeedingBotMoveAsync` 实现。
  - 新 migration `AddBotSupport`:加 `IsBot` 列 + seed 两个 bot 账号(`AI-Easy` 与 `AI-Medium`,固定 Guid,email `easy@bot.gomoku.local` / `medium@bot.gomoku.local`,`PasswordHash="__BOT_NO_LOGIN__"`,`IsActive=true`,`IsBot=true`)。
  - 新 `BackgroundServices/AiMoveWorker : BackgroundService`:启动后每 `AiOptions.PollIntervalMs`(默认 1500ms)循环,调 `IUserRepository.GetRoomsNeedingBotMoveAsync`,为每个命中 room 发 `ExecuteBotMoveCommand`。异常被吞进 Serilog 日志,worker 本身不终止。
  - 新 `AiOptions { PollIntervalMs, MinThinkTimeMs }`,绑定 `appsettings.json` 的 `"Ai"` 段。
- **Api**:
  - `RoomsController` 新增 `POST /api/rooms/ai` → `CreateAiRoomCommand`,成功 201 + `RoomStateDto`。
  - `Program.cs` 在 `AddInfrastructure()` 注册 `AiMoveWorker` + `AiOptions`,`appsettings.json` 增 `"Ai"` 段。
- **Tests**:
  - `Gomoku.Domain.Tests/Ai/EasyAiTests.cs`:空盘选点合法、单格盘必选该格、固定种子可重现、**从不选已有子的格子**。
  - `Gomoku.Domain.Tests/Ai/MediumAiTests.cs`:① 能连五就连五 ② 对手将连五时必堵 ③ 两者都不成立时打分选中心偏好 ④ 不选越界点。
  - `Gomoku.Domain.Tests/Users/UserRegisterBotTests.cs`:bot 账号字段初始值、`IsBot==true`、`PasswordHash` 为占位常量。
  - `Gomoku.Application.Tests/Features/Rooms/CreateAiRoomCommandHandlerTests.cs`:成功创建 + bot 加入为白方 + 状态 Playing;bot 不存在抛 UserNotFound;非 Host 用户尝试(validator 防御)。
  - `Gomoku.Application.Tests/Features/Bots/ExecuteBotMoveCommandHandlerTests.cs`:轮到 bot 时调 ISender.Send(MakeMoveCommand) 一次;不轮到 bot 时不发 (worker 端已过滤但 handler 再次防御);Room.Finished 时不发。
  - `Gomoku.Application.Tests/Features/Auth/LoginCommandHandlerTests.cs` 扩展:IsBot 账号凭据正确也拒(抛 `InvalidCredentialsException`)。

**显式不做**(留给后续变更):
- `Hard` 难度(Minimax / α-β 搜索 / 复杂威胁识别 `活四` `冲四` `双三`):下一轮 `add-ai-opponent-hard`。
- bot 参与 `Urge`(催促 bot 无意义,bot 也不催人 —— 其回合靠 worker 被动触发):`Room.UrgeOpponent` 今天对 bot 已经"能催但没观感",本变更不禁止,也不专门广播 —— **MediumAi 的速度就是其速度**。
- bot 参与聊天(发"GG"之类):留给`add-ai-chatter`。
- 按不同难度 bot 起始不同 Rating:两 bot 都 1200 起,跟随 ELO 正常更新,但**不进排行榜**(D7)。
- AI-vs-AI 机器人对战(会和轮询 worker 形成有趣的自动对局循环,不属于用户场景):显式 `CreateAiRoomCommand` 的 `hostId` 必须是非 bot 用户(validator 兜底)。
- "旁观 AI 对局"的围观模式:和现有 `JoinAsSpectator` 完全一致,bot 局不特殊化。
- Bot 账号自己登录 / 拿 token 调 REST:`LoginCommandHandler` 直接拒(D8)。

## Capabilities

### New Capabilities

- **`ai-opponent`** — 虚拟玩家的身份模型(`User.IsBot`)、难度枚举、AI 决策接口与其两个实现、AI 房间创建端点、后台轮询 worker 的职责与时序。

### Modified Capabilities

- **`user-management`** — `User` 聚合加 `IsBot` 字段;新增 `User.RegisterBot` 工厂;`IUserRepository` 新增两个查询方法签名;数据库迁移加列 + seed。
- **`elo-rating`** — `GetTopByRatingAsync` / `/api/leaderboard` 排行榜 MUST 过滤掉 `IsBot=true` 的用户(避免 bot 出现在榜单)。ELO 计算本身对 bot **不做特殊处理**(bot 跟人一样吃胜负积分,避免 bot 永远锁定 1200 导致"刷 bot 上分"套利)。
- **`authentication`** — `LoginCommand` 在 IsBot 账号上 MUST 返回 `InvalidCredentialsException`(401),即使密码哈希对得上;语义与"账号不存在 / 密码错"保持一致,防止侧信道枚举 bot 账号。

## Impact

- **代码规模**:~25 新文件 + 1 迁移 + 约 ~500 行生产代码(Domain AI 逻辑占大头)、~300 行测试。比 `add-rooms-and-gameplay` 小,比 `add-elo-system` 大。
- **NuGet**:零新增。
- **HTTP 表面**:+1 端点 `POST /api/rooms/ai`。
- **SignalR 表面**:**零变化**。bot 走 Hub 广播的是现有 `MoveMade` / `GameEnded` 事件 —— 前端无感。
- **数据库**:`Users` 表多 1 列 `IsBot`(默认 0,对老行向后兼容);seed 2 行。
- **运行时**:新增 1 个 `BackgroundService` 线程,默认每 1.5s 查一次库;空载时查询返回空列表 → 可忽略 I/O。
- **后续变更将依赖**:前端加"创建 AI 房间"按钮 + 难度选择器;`add-ai-opponent-hard` 追加 `BotDifficulty.Hard`、`HardAi`、seed 第 3 个 bot 账号。
