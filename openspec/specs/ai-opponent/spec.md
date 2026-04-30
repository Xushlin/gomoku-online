# ai-opponent Specification

## Purpose

人机对战能力:把"机器人"作为一等公民接入既有对局流水线 —— bot 是带 `IsBot=true` 标志的真 `User`
(固定 Guid,由 migration seed 写入,永远不可登录),在 `Room` 聚合里占据和真人等价的
`BlackPlayerId` / `WhitePlayerId` 槽位。后台 `AiMoveWorker` 定期轮询"轮到 bot 走的 Room",为每个
命中房间嵌套 dispatch `MakeMoveCommand`,复用现有落子 handler 的完整事务、ELO 更新与 SignalR 广播路径
—— 下游 Hub / 聊天 / 催促 / 排行榜(**但 bot 不入榜**)全部无感。

本能力定义:难度枚举 `BotDifficulty`(`Easy=0` / `Medium=1`)、纯函数式决策接口 `IGomokuAi`
及其两个实现(EasyAi:均匀随机;MediumAi:自赢 → 堵五 → 中心距离 + 邻接打分)、一步式 AI 房间创建
端点 `POST /api/rooms/ai`、内部命令 `ExecuteBotMoveCommand` 与 worker 的轮询 / 思考延迟语义,以及
bot 账号的固定主键集合 `BotAccountIds`。

实现位于 `backend/src/Gomoku.Domain/Ai/`(纯领域算法)、`backend/src/Gomoku.Application/Features/Rooms/CreateAiRoom/`
与 `Features/Bots/ExecuteBotMove/`(应用层 handlers)、`backend/src/Gomoku.Infrastructure/BackgroundServices/`
(`AiMoveWorker`)、`backend/src/Gomoku.Infrastructure/Ai/`(`AiRandomProvider`),以及 `AddBotSupport`
migration(`Users.IsBot` 列 + 两行 bot seed)。
## Requirements
### Requirement: `BotDifficulty` 枚举表达 AI 难度

系统 SHALL 在 `Gomoku.Domain/Ai/BotDifficulty.cs` 定义 `enum BotDifficulty { Easy = 0, Medium = 1, Hard = 2 }`(**本次追加 `Hard`**)。底层整数值固定,以便序列化稳定性与未来加 `Master=3` 等扩展。

#### Scenario: 枚举值存在
- **WHEN** 审阅 `Gomoku.Domain/Ai/BotDifficulty.cs`
- **THEN** 存在三个值 `Easy=0`、`Medium=1`、**`Hard=2`**

---

### Requirement: `IGomokuAi` 是纯函数式 AI 决策接口

系统 SHALL 在 `Gomoku.Domain/Ai/IGomokuAi.cs` 定义:

```
Position SelectMove(Board board, Stone myStone);
```

实现 MUST 满足:
- 返回的 `Position` 落在 `board` 的空格(`Stone.Empty`)上;
- 不修改 `board`(调用方可认为传入实例在返回后与调用前等价);
- 不读时钟 / 磁盘 / 网络 / 静态可变状态;
- 对相同 `board` 快照 + 相同 `myStone` 与相同随机源,输出 MUST 可复现。

`myStone` 传入 `Stone.Empty` 时实现 MUST 抛 `ArgumentOutOfRangeException`。若 `board` 已经没有任何空格,实现 MUST 抛 `InvalidOperationException`(调用方在棋盘满之前已经见过 `GameResult.Draw` 并应停止调用)。

#### Scenario: 合法输出
- **WHEN** 对一个包含若干空格的 `Board` 和 `Stone.Black` 调 `SelectMove`
- **THEN** 返回的 `Position` 对应格子为 `Stone.Empty`;`board` 在返回前后内容完全一致

#### Scenario: 拒绝 Empty 棋色
- **WHEN** 传入 `myStone == Stone.Empty`
- **THEN** 抛 `ArgumentOutOfRangeException`

#### Scenario: 满棋盘
- **WHEN** `board` 已经 225 格全占,调 `SelectMove`
- **THEN** 抛 `InvalidOperationException`

---

### Requirement: `EasyAi` 在空格集合里均匀随机选点

系统 SHALL 在 `Gomoku.Domain/Ai/EasyAi.cs` 实现 `IGomokuAi`。构造函数接收 `Random random`,所有随机性 MUST 通过该 `Random` 产生;不得创建隐式 `new Random()`。策略:枚举当前 `board` 所有空格,`random.Next(count)` 选一个。

#### Scenario: 固定种子可复现
- **WHEN** 用 `new Random(42)` 构造两次 `EasyAi`,对同一个空棋盘和 `Stone.Black` 各调一次 `SelectMove`
- **THEN** 两次返回同一个 `Position`

#### Scenario: 从不选已有子位置
- **WHEN** 棋盘上已有 10 个子,对其调 1000 次 `SelectMove`(每次随机源推进)
- **THEN** 每次返回的 `Position` MUST 是 `Stone.Empty` 格;从不落在这 10 个已有子上

#### Scenario: 单格棋盘必选该格
- **WHEN** 棋盘只剩一个空格 `(r, c)`,调 `SelectMove`
- **THEN** 返回 `Position(r, c)`

---

### Requirement: `MediumAi` 按"自赢 → 堵五 → 启发分"三层优先级选点

系统 SHALL 在 `Gomoku.Domain/Ai/MediumAi.cs` 实现 `IGomokuAi`,按下列顺序选点:

1. **自赢**:枚举所有空格 `p`,若 `board.Clone()` 后 `PlaceStone(Move(p, myStone))` 的 `GameResult` 对应己方胜(连五),直接返回 `p`。若存在多个,在这些点里用注入的 `Random` 随机选一个。
2. **堵五**:若 1 无命中,枚举所有空格 `p`,若 `board.Clone()` 后 `PlaceStone(Move(p, opponentStone))` 的 `GameResult` 对应对手胜,直接返回 `p`(对这样的点自己落子即堵住对手的连五)。多个时同样随机选一个。
3. **启发分**:若 1、2 都不命中,对每个空格 `p` 计算得分 `score(p) = -ChebyshevDistance(p, center=(7,7)) + adjacency(p, myStone, board)`。`adjacency` 是 `p` 周围 8 邻域中己方同色子的数量。返回得分最高的空格;若并列,用注入的 `Random` 随机选一个。

构造函数接收 `Random random` 参数(用于并列打破),不创建隐式 `new Random()`。

对 `Stone.Empty` 作为 `myStone` MUST 抛 `ArgumentOutOfRangeException`。

#### Scenario: 自赢优先
- **WHEN** 黑方在 `(7,3)..(7,6)` 已经有 4 子连,调 `SelectMove(board, Black)`
- **THEN** 返回 `(7,2)` 或 `(7,7)` 之一(两者都能连五;MediumAi 用 Random 破平)

#### Scenario: 必堵对手连五
- **WHEN** 白方在 `(7,3)..(7,6)` 已经有 4 子连,黑方**不能**连五,调 `SelectMove(board, Black)`
- **THEN** 返回 `(7,2)` 或 `(7,7)` 之一(两者都能堵)

#### Scenario: 中心偏好
- **WHEN** 空棋盘、`myStone=Black`
- **THEN** 返回 `(7,7)`(距中心最近,邻接 0)—— 第一层第二层均无命中时靠启发分

#### Scenario: 从不选已有子位置
- **WHEN** 棋盘上已有若干子,反复调 `SelectMove`
- **THEN** 返回 `Position` 每次都是空格

#### Scenario: 不修改入参 Board
- **WHEN** 调 `SelectMove`
- **THEN** 入参 `board` 的每一格在返回前后完全一致(实现内部只对 `board.Clone()` 做试探)

---

### Requirement: `GomokuAiFactory` 按难度返回 `IGomokuAi` 实例

系统 SHALL 在 `Gomoku.Domain/Ai/GomokuAiFactory.cs` 定义:

```
public static IGomokuAi Create(BotDifficulty difficulty, Random random);
```

支持的分支(**本次追加 `Hard`**):
- `Easy` → 新 `EasyAi(random)`
- `Medium` → 新 `MediumAi(random)`
- **`Hard` → 新 `HardAi(random)`(默认 `searchDepth=2`)**
- 其它 → `ArgumentOutOfRangeException`

工厂本身不持有状态;每次 `Create` 返回一个新实例。

#### Scenario: Easy 分支
- **WHEN** `Create(BotDifficulty.Easy, new Random(1))`
- **THEN** 返回 `IGomokuAi` 实例,运行时类型是 `EasyAi`

#### Scenario: Medium 分支
- **WHEN** `Create(BotDifficulty.Medium, new Random(1))`
- **THEN** 返回 `IGomokuAi` 实例,运行时类型是 `MediumAi`

#### Scenario: Hard 分支
- **WHEN** `Create(BotDifficulty.Hard, new Random(1))`
- **THEN** 返回 `IGomokuAi` 实例,运行时类型是 `HardAi`

#### Scenario: 未定义枚举值
- **WHEN** `Create((BotDifficulty)99, new Random())`
- **THEN** 抛 `ArgumentOutOfRangeException`

---

### Requirement: `BotAccountIds` 固定机器人账号主键

Application 层 SHALL 在 `Gomoku.Application/Abstractions/BotAccountIds.cs` 暴露静态只读字段(**本次追加 `Hard`**):

```
public static readonly Guid Easy   = Guid.Parse("00000000-0000-0000-0000-00000000ea51");
public static readonly Guid Medium = Guid.Parse("00000000-0000-0000-0000-0000000bed10");
public static readonly Guid Hard   = Guid.Parse("00000000-0000-0000-0000-0000000000ad");
public static Guid For(BotDifficulty difficulty);
public static BotDifficulty? TryGetDifficulty(Guid userId);
```

`For` 方法:`Easy → Easy`,`Medium → Medium`,**`Hard → Hard`**,其它 → `ArgumentOutOfRangeException`。

`TryGetDifficulty` 方法:按 Guid 比较返回对应 `BotDifficulty`(三者之一)或 `null`。

**唯一**允许出现这些魔法 Guid 的代码位置:本文件 + `AddBotSupport` migration(Easy/Medium seed)+ `AddHardBotAccount` migration(Hard seed)的 `InsertData` / `HasData` 调用。Handler / Worker MUST 使用 `BotAccountIds.For(...)` / `TryGetDifficulty(...)` 间接访问。

#### Scenario: 三个 Guid 不冲突
- **WHEN** 比较 `BotAccountIds.Easy` / `Medium` / `Hard`
- **THEN** 三者两两不相等

#### Scenario: 按难度解析
- **WHEN** 调 `BotAccountIds.For(BotDifficulty.Hard)`
- **THEN** 返回等于 `BotAccountIds.Hard` 的 `Guid`

#### Scenario: 反向查 Hard
- **WHEN** 调 `BotAccountIds.TryGetDifficulty(BotAccountIds.Hard)`
- **THEN** 返回 `BotDifficulty.Hard`

### Requirement: `CreateAiRoomCommand` 一步创建房间并让机器人加入

Application 层 SHALL 新增:

```
public sealed record CreateAiRoomCommand(
    UserId HostUserId, string Name, BotDifficulty Difficulty, Stone HumanSide)
    : IRequest<RoomStateDto>;
```

Handler 流程 MUST 按顺序:

1. `FindByIdAsync(HostUserId)` 加载 Host;未找到抛 `UserNotFoundException`。
2. **断言 `host.IsBot == false`**;若为 true 抛 `ValidationException`("AI cannot host an AI room.")。
3. 按 `BotAccountIds.For(Difficulty)` 定位 bot UserId;`FindByIdAsync` 加载;未找到抛 `UserNotFoundException`(提示检查 migration seed)。
4. `Room.Create(new RoomId(Guid.NewGuid()), Name, HostUserId, _clock.UtcNow)`。
5. `room.JoinAsPlayer(botUserId, _clock.UtcNow)` —— 状态从 Waiting 进 Playing,`Game` 实例化。
6. **如果 `HumanSide == Stone.White`,调 `room.SwapPlayers(_clock.UtcNow)`**(本次新增)。结果:`BlackPlayerId == botUserId`,`WhitePlayerId == HostUserId`,host 仍是真人;`Game.CurrentTurn` 仍是 Black,即立刻轮到 bot 走第 1 步。如果 `HumanSide == Stone.Black`,跳过这步(默认行为)。
7. `IRoomRepository.AddAsync(room, ct)`。
8. `IUnitOfWork.SaveChangesAsync(ct)` —— **一次**事务内提交房间、Game 与潜在的 swap。
9. 拉 username 字典(Host + Bot),用 `room.ToState(usernames)` 组装 `RoomStateDto` 返回。

Validator(FluentValidation)独立校验 `Name` 规则(和现有 `CreateRoomCommandValidator` 一致:3–50 非空白)。`Difficulty` 与 `HumanSide` 由 enum 类型系统保证。`HumanSide` MUST 仅接受 `Stone.Black` 或 `Stone.White`(`Stone.Empty` 抛 ValidationException)。

#### Scenario: 成功创建 AI 房间(默认 humanSide=Black)
- **WHEN** 真人 Alice 发 `CreateAiRoomCommand(alice, "quick practice", Medium, Stone.Black)`
- **THEN** 返回 `RoomStateDto`,`Status == Playing`,`BlackPlayerId == alice`,`WhitePlayerId == BotAccountIds.Medium`,`Game.CurrentTurn == Black`,`Game.Moves` 空

#### Scenario: 真人选 White 反转座位
- **WHEN** 真人 Alice 发 `CreateAiRoomCommand(alice, "defense practice", Medium, Stone.White)`
- **THEN** 返回 `RoomStateDto`,`Status == Playing`,`BlackPlayerId == BotAccountIds.Medium`,`WhitePlayerId == alice`,`HostUserId == alice`,`Game.CurrentTurn == Black`(轮到 bot 先走),`Game.Moves` 空;后续 AI worker 轮询会触发 bot 的第 1 步

#### Scenario: 机器人不存在(migration 未应用)
- **WHEN** 库里不存在 `BotAccountIds.Easy` 对应 User,调 `CreateAiRoomCommand(alice, "x", Easy, Stone.Black)`
- **THEN** 抛 `UserNotFoundException`

#### Scenario: AI-vs-AI 被拒
- **WHEN** 某调用方传入 `HostUserId = BotAccountIds.Easy`(即以机器人身份 Host)
- **THEN** 抛 `ValidationException`

#### Scenario: HumanSide=Empty 被拒
- **WHEN** 调 `CreateAiRoomCommand(alice, "x", Easy, Stone.Empty)`
- **THEN** 抛 `ValidationException`(Empty 不是合法落座选择)

---

### Requirement: `ExecuteBotMoveCommand` 是 worker 的内部入口,不经 Hub / REST

Application 层 SHALL 新增:

```
public sealed record ExecuteBotMoveCommand(UserId BotUserId, RoomId RoomId)
    : IRequest<Unit>;
```

Handler 流程:

1. 加载 Room;若不存在抛 `RoomNotFoundException`(worker 吞掉,下次重试)。
2. **防御式校验**:
   - `room.Status == Playing`,否则抛 `RoomNotInPlayException`(worker 吞掉)。
   - `BotUserId ∈ {room.BlackPlayerId, room.WhitePlayerId}`,否则抛 `NotAPlayerException`。
   - 推断 bot 的 `Stone`,必须等于 `room.Game.CurrentTurn`,否则抛 `NotYourTurnException`。
3. 加载 bot 的 User,按其 Id 反推难度(`BotAccountIds.Easy` / `Medium`);若不是 seed 的两个之一抛 `ArgumentException`。
4. `GomokuAiFactory.Create(difficulty, workerRandom)`。
5. 从 `room.Game.Moves` replay `Board`;调 `ai.SelectMove(board, botStone)` 得 `Position pick`。
6. **通过 `ISender.Send(new MakeMoveCommand(BotUserId, RoomId, pick.Row, pick.Col), ct)`** —— 复用现有落子 handler 的全套事务、ELO 更新、SignalR 通知。
7. 返回 `Unit.Value`。

Handler **MUST NOT** 自己调 `Room.PlayMove`,也 **MUST NOT** 自己调 `IRoomNotifier`。所有副作用都通过嵌套的 `MakeMoveCommand` 路径产生。

#### Scenario: 机器人正确走一步
- **WHEN** bot 白方在轮次对,调 `ExecuteBotMoveCommand(botId, roomId)`
- **THEN** 内部 `ISender.Send(new MakeMoveCommand(botId, roomId, row, col))` 被调用恰好一次,`row/col` 来自 `ai.SelectMove`

#### Scenario: 不轮到 bot
- **WHEN** 轮到真人黑方时有并发把 worker 发了一发 `ExecuteBotMoveCommand` 给白方 bot
- **THEN** 抛 `NotYourTurnException`;worker 捕获后丢弃,不重试

#### Scenario: Room 已结束
- **WHEN** Room 已 Finished
- **THEN** 抛 `RoomNotInPlayException`

---

### Requirement: `IUserRepository` 新增两个支持 AI 的查询

Application 层 SHALL 在 `IUserRepository` 追加:

```
Task<User?> FindBotByDifficultyAsync(BotDifficulty difficulty, CancellationToken cancellationToken);
Task<IReadOnlyList<RoomId>> GetRoomsNeedingBotMoveAsync(CancellationToken cancellationToken);
```

- `FindBotByDifficultyAsync`:实现按 `BotAccountIds.For(difficulty)` 查 User;若对应记录不存在或 `IsBot=false`,返回 `null`。签名只含领域类型,不暴露 EF。
- `GetRoomsNeedingBotMoveAsync`:实现返回所有满足 `(Room.Status == Playing) AND (Room.Game.CurrentTurn 的玩家 Id 对应 User.IsBot == true)` 的 `RoomId` 列表,**不包含**其他房间字段(只要 Id,worker 再按 Id 加载完整聚合)。

返回类型 `IReadOnlyList<RoomId>` 保证 Application 层看不到 EF 细节。

#### Scenario: 只返回 AI 该走的房间
- **WHEN** 库里有 1 个纯真人对局(轮到黑)、1 个 AI 对局轮到白(bot)、1 个 AI 对局轮到黑(真人),调 `GetRoomsNeedingBotMoveAsync`
- **THEN** 只返回第 2 个房间的 `RoomId`

#### Scenario: 无 AI 对局
- **WHEN** 库里没有 `IsBot=true` 参与的 Playing Room
- **THEN** 返回空列表,不抛

---

### Requirement: `AiMoveWorker` 后台轮询驱动 AI 走子

Infrastructure 层 SHALL 新增 `BackgroundServices/AiMoveWorker : BackgroundService`。循环伪代码:

```
while (!stopToken.IsCancellationRequested)
{
    await Task.Delay(options.PollIntervalMs, stopToken);
    using var scope = sp.CreateScope();
    var users  = scope.Resolve<IUserRepository>();
    var rooms  = scope.Resolve<IRoomRepository>();
    var clock  = scope.Resolve<IDateTimeProvider>();
    var sender = scope.Resolve<ISender>();
    var pending = await users.GetRoomsNeedingBotMoveAsync(stopToken);
    foreach (var roomId in pending)
    {
        try
        {
            var room = await rooms.FindByIdAsync(roomId, stopToken);
            if (room is null || room.Status != Playing) continue;
            var botId = room.Game.CurrentTurn == Stone.Black ? room.BlackPlayerId : room.WhitePlayerId!.Value;
            var lastMoveAt = room.Game.Moves.LastOrDefault()?.PlayedAt ?? room.Game.StartedAt;
            if ((clock.UtcNow - lastMoveAt).TotalMilliseconds < options.MinThinkTimeMs) continue; // 下轮再看
            await sender.Send(new ExecuteBotMoveCommand(botId, roomId), stopToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "AiMoveWorker failed on room {RoomId}", roomId);
        }
    }
}
```

MUST 满足:
- 每次循环 `CreateScope` 以得到正确生命周期的 `DbContext` / Handlers;
- 异常(除 `OperationCanceledException`)不中断 worker;
- `MinThinkTimeMs` 检查用**同一个** `IDateTimeProvider.UtcNow` 源,以便测试可注入假时钟;
- worker 自身不直接访问 `DbContext`、不直接发 SignalR。

#### Scenario: 空载
- **WHEN** worker 运行但库里无 AI 对局
- **THEN** 每轮 `GetRoomsNeedingBotMoveAsync` 返回空;不发任何命令;不报错

#### Scenario: 思考时间未到
- **WHEN** 对手刚 `MakeMove` 200ms,worker 命中该房间,`MinThinkTimeMs=800`
- **THEN** 该轮跳过;bot 不落子

#### Scenario: 异常不中断 worker
- **WHEN** 某次对某房间 `ExecuteBotMoveCommand` 抛 `RoomNotFoundException`(房间被并发删除)
- **THEN** worker 记 Error 日志,继续处理下一房间,下轮仍正常跑

#### Scenario: 优雅关闭
- **WHEN** `stopToken` 触发(应用停止)
- **THEN** worker 的 `ExecuteAsync` 退出,不吃掉 `OperationCanceledException`

---

### Requirement: `AiOptions` 绑定 `appsettings.json` 的 `"Ai"` 段

Application 层 SHALL 定义 `AiOptions { PollIntervalMs, MinThinkTimeMs }`;Api 层 `Program.cs` MUST 通过 `builder.Services.Configure<AiOptions>(...)` 绑定。默认值 `PollIntervalMs=1500`、`MinThinkTimeMs=800`。

`PollIntervalMs < 100` / `MinThinkTimeMs < 0` MUST 在 Options 验证器里拒绝(`IValidateOptions<AiOptions>` 或 `ValidateDataAnnotations`)。

#### Scenario: 启动时读取
- **WHEN** `appsettings.json` 没有 `"Ai"` 段
- **THEN** `AiOptions` 使用默认值 `1500 / 800`

#### Scenario: 合法覆盖
- **WHEN** `appsettings.Development.json` 写 `"Ai": { "PollIntervalMs": 500, "MinThinkTimeMs": 300 }`
- **THEN** 运行时 `AiMoveWorker` 采用该值

#### Scenario: 非法值拒绝
- **WHEN** 配置 `"PollIntervalMs": 50`
- **THEN** 应用启动时失败(options validation 阻断),不进入 `app.Run()`

---

### Requirement: `POST /api/rooms/ai` 端点暴露 AI 房间创建

Api 层 SHALL 暴露 `POST /api/rooms/ai`(`[Authorize]`),请求 body `CreateAiRoomRequest { Name: string, Difficulty: BotDifficulty, HumanSide?: Stone }`(JSON 中 `Difficulty` 和 `HumanSide` 接受 `"Easy"` / `"Medium"` / `"Hard"` 与 `"Black"` / `"White"` 字符串,由 `JsonStringEnumConverter` 转换)。`HumanSide` **MUST 是可选字段**(`Stone?` 可空):缺省 / null 时 controller 默认填 `Stone.Black`(向后兼容,旧客户端继续工作)。行为:

- Controller 从 JWT `sub` 取 `UserId host`;
- 把 `request.HumanSide ?? Stone.Black` 当作 effective side;
- 发 `CreateAiRoomCommand(host, request.Name, request.Difficulty, effectiveSide)`;
- 成功 201 Created,`Location = /api/rooms/{id}`,body `RoomStateDto`。

MUST NOT 接受 URL 参数或 query string;MUST NOT 对未 `[Authorize]` 的调用放行。

#### Scenario: 成功(默认 Black)
- **WHEN** 登录用户 `POST /api/rooms/ai { name: "test", difficulty: "Easy" }`(不传 humanSide)
- **THEN** HTTP 201,响应体是 `RoomStateDto`,`BlackPlayerId == 调用方 userId`,`WhitePlayerId == BotAccountIds.Easy`,`Status == Playing`

#### Scenario: 成功(显式 humanSide=White)
- **WHEN** 登录用户 `POST /api/rooms/ai { name: "test", difficulty: "Easy", humanSide: "White" }`
- **THEN** HTTP 201,`BlackPlayerId == BotAccountIds.Easy`,`WhitePlayerId == 调用方 userId`,`HostUserId == 调用方 userId`

#### Scenario: 名字非法
- **WHEN** `name == ""`
- **THEN** HTTP 400(validator 拒绝)

#### Scenario: humanSide 字符串非法
- **WHEN** `POST /api/rooms/ai { ..., humanSide: "Empty" }` 或其它非 Black/White 值
- **THEN** HTTP 400(validator 拒绝;`Stone.Empty` 不允许)

#### Scenario: 未登录
- **WHEN** 不带 Bearer token 请求
- **THEN** HTTP 401

### Requirement: `AddBotSupport` migration 插入 2 个 bot 账号

Infrastructure 层 SHALL 在 `Persistence/Migrations/` 新增 `AddBotSupport` migration,内容包含:

- `ALTER TABLE Users ADD COLUMN IsBot INTEGER NOT NULL DEFAULT 0`(SQLite);对未来 SQL Server 是 `BIT NOT NULL DEFAULT 0`。
- `HasData` 插入两行 User:
  - Id = `BotAccountIds.Easy`,Username = `AI_Easy`,Email = `easy@bot.gomoku.local`,PasswordHash = `"__BOT_NO_LOGIN__"`,Rating=1200,其他计数器 0,IsActive=true,**IsBot=true**,CreatedAt = 2026-01-01T00:00:00Z(确定值以保证 migration 可重放)。
  - Id = `BotAccountIds.Medium`,Username = `AI_Medium`,其余同上。

MUST NOT 在任何真实运行时路径创建 bot(禁止"ensure-exists"的惰性初始化)。Bot 账号的存在**完全**由 migration 保证。

#### Scenario: 迁移后 User 表包含两行 bot
- **WHEN** 运行 `dotnet ef database update`
- **THEN** `SELECT COUNT(*) FROM Users WHERE IsBot = 1` 为 2;两行 Username 为 `AI_Easy` / `AI_Medium`;PasswordHash 为 `__BOT_NO_LOGIN__`

#### Scenario: 回滚
- **WHEN** 运行 `dotnet ef migrations remove`(尚未部署时)
- **THEN** `IsBot` 列消失,两行 bot 记录被一并回滚

---

### Requirement: 全局异常映射为本变更新增的异常覆盖

以下异常首次在本变更的路径中出现,MUST 被全局异常中间件映射:

| 异常 | HTTP | 出现路径 |
|---|---|---|
| `ValidationException`(AI-vs-AI 拒绝) | 400 | `CreateAiRoomCommandHandler` |
| `UserNotFoundException`(bot seed 缺失) | 404 | `CreateAiRoomCommandHandler` / `ExecuteBotMoveCommandHandler` |
| `NotYourTurnException`(worker 竞态) | 409 | `ExecuteBotMoveCommandHandler` —— 但实际不会冒泡到 HTTP,worker 内部吞 |
| `RoomNotInPlayException`(worker 竞态) | 409 | 同上 |

以上映射**已在 `add-rooms-and-gameplay` 中定义**;本变更不新增映射条目,仅声明覆盖面。

#### Scenario: AI-vs-AI 映射
- **WHEN** 恶意客户端构造了一个假 JWT(sub=BotAccountIds.Easy)并调 `POST /api/rooms/ai`
- **THEN** HTTP 400,ProblemDetails 指向 `ValidationException`

### Requirement: `HardAi` 实现 Minimax + α-β 的两层前瞻搜索

系统 SHALL 在 `Gomoku.Domain/Ai/HardAi.cs` 实现 `IGomokuAi`,代表"Hard"难度。构造签名:

```
public HardAi(Random random, int searchDepth = 2);
```

- `random`:用于分相同候选的破平;MUST 非 null 否则抛 `ArgumentNullException`。
- `searchDepth`:默认 2;MUST ≥ 1,否则抛 `ArgumentOutOfRangeException`。生产通过 `GomokuAiFactory` 使用默认值,暴露该参数仅为单元测试。

`SelectMove(Board, Stone myStone)` 行为:

1. `myStone == Stone.Empty` → 抛 `ArgumentOutOfRangeException`。
2. 棋盘完全为空 → 返回 `Position(7, 7)`。
3. 否则生成候选集(已有子 Chebyshev 距离 ≤ 2 的空格,去重);
4. 对每个候选 `c` 先试走自己一手:若直接连五,立即返回 `c`(与 MediumAi 的第一层一致);
5. 否则用 **Minimax + α-β** 在该候选下递归搜索 `searchDepth - 1` 层(对方先行),得到 `score`;
6. 返回 `score` 最大的候选;并列使用 `random` 破平。

MUST 为纯函数:不修改入参 `board`,不读时钟 / 磁盘 / 网络 / 静态状态;相同 `random` 种子 + 相同盘面 → 相同返回值。

#### Scenario: 空盘首手中心
- **WHEN** 空盘,`myStone = Black`,`HardAi(new Random(42))` 调 `SelectMove`
- **THEN** 返回 `Position(7, 7)`

#### Scenario: 能连五立即选
- **WHEN** 黑方 `(7,3)..(7,6)` 已 4 连、我方是黑、任一方空点可连五
- **THEN** 返回 `(7, 2)` 或 `(7, 7)`(两者皆可;`random` 破平)

#### Scenario: 对手能连五必堵
- **WHEN** 白方 `(7,3)..(7,6)` 已 4 连,我方是黑,黑方自己无连五机会
- **THEN** 返回 `(7, 2)` 或 `(7, 7)` —— 堵对手必胜点

#### Scenario: 识别并阻止对手活三
- **WHEN** 对手在 `(7,5)(7,6)(7,7)` 三连,两端 `(7,4)` 与 `(7,8)` 均空(活三),我方现在走棋
- **THEN** 返回的 `Position` MUST 是 `(7, 4)` 或 `(7, 8)` 之一(堵活三);**不**选盘面远处格子

#### Scenario: 从不选已有子位置
- **WHEN** 对有若干已占子的盘面反复调 `SelectMove`(不同随机源 推进)
- **THEN** 返回的 `Position` 每次都是空格;从不落在已有子上

#### Scenario: 不修改入参 Board
- **WHEN** 调 `SelectMove`
- **THEN** 入参 `board` 的每一格在返回前后完全一致(实现内部只对 `board.Clone()` 做试探)

#### Scenario: 固定种子复现
- **WHEN** 两个独立 `HardAi(new Random(42))` 对同一盘面调 `SelectMove`
- **THEN** 两次返回的 `Position` 相等

#### Scenario: 非法参数拒绝
- **WHEN** `random == null` 构造 HardAi;或 `searchDepth == 0`;或 `myStone == Empty`
- **THEN** 抛 `ArgumentNullException` / `ArgumentOutOfRangeException`

---

### Requirement: `HardAi` 的评估函数按模式 × 封闭度打分

`Evaluate(Board, myStone)` 内部 MUST 沿 4 方向(水平、垂直、主对角、反对角)扫描所有**长度 ≥ 2 的同色连续段**,识别"活 / 眠 / 四 / 三 / 二"组合。打分表(己方正分;对手分乘 `-1.1`,偏防守):

| 模式 | 含义 | 己方分 |
|---|---|---|
| 活四 | 长度 4,两端皆空 | +10000 |
| 冲四 | 长度 4,一端封(对方子或边界) | +1000 |
| 活三 | 长度 3,两端皆空 | +500 |
| 眠三 | 长度 3,一端封 | +100 |
| 活二 | 长度 2,两端皆空 | +50 |
| 眠二 | 长度 2,一端封 | +10 |

"连五"本身 MUST 在 Minimax 的**终局判定**里以 `±100000` 直接返回,不走 `Evaluate` 累加路径。

**不做**(显式 non-goal):跳步识别(`X.XX`、`XX.XX`)不在评估范围;Master 档再扩。

#### Scenario: 己方活三得正分
- **WHEN** `Evaluate` 在一个只有黑方 `(7,5)(7,6)(7,7)` 活三的盘面上,`myStone=Black`
- **THEN** 返回 ≥ +500(可能略高,因水平方向的活二等也累加;关键是**正号**)

#### Scenario: 对手活三得负分
- **WHEN** 同一盘面但 `myStone=White`(扫到黑的活三)
- **THEN** 返回 ≤ -500(`-1.1 * 500 = -550`)

#### Scenario: 双活三叠加
- **WHEN** 盘面同时有己方水平活三 + 主对角活三
- **THEN** 返回 ≥ +1000(两次 +500 叠加,外加两条活三重叠位置的活二等次级加成)

---

### Requirement: `HardAi` 的候选生成限于已有子 2 格邻域

`GenerateCandidates(Board)` MUST:

- 空盘返回 `[Position(7, 7)]`;
- 非空盘:枚举所有已占子的位置,取它们的 Chebyshev 距离 ≤ 2 的空格(即 5×5 方块内的空格),去重后返回。

距离选 2 而非 1,以覆盖"跳步活三"附近的关键点(例如对手 `X.XX` 中间的空格 `(c+1)` 与 `(c+2)`)。

#### Scenario: 空盘只一个候选
- **WHEN** 空盘调 `SelectMove`
- **THEN** 候选集等价于 `[(7,7)]`(实际通过特判直接返回,不经历完整候选生成);Hard 不会从 225 个空格随机选

#### Scenario: 单子盘面候选 = 5×5 - 1
- **WHEN** 盘面只有 `(7,7)` 一颗子
- **THEN** 候选集 MUST 是 `(5,5)..(9,9)` 25 格中除 `(7,7)` 外的 24 个空格,去重

#### Scenario: 远离战局的位置不在候选
- **WHEN** 盘面子集中在 `(7,7)` 附近
- **THEN** 候选 MUST NOT 包含 `(0,0)` / `(14,14)` 等远离所有已有子 3+ 格的位置

---

### Requirement: `BotDifficulty.Hard = 2` 作为新难度枚举值

`Gomoku.Domain/Ai/BotDifficulty.cs` MUST 追加 `Hard = 2`。现有 `Easy = 0` / `Medium = 1` 的底层整数值 MUST 保持不变,以维持 `add-ai-opponent` 定下的序列化稳定性。

`GomokuAiFactory.Create(BotDifficulty difficulty, Random random)` MUST 支持新分支:

- `Hard` → 新 `HardAi(random)` 实例(默认 `searchDepth=2`)。

其它分支(`Easy` / `Medium`)与默认 `ArgumentOutOfRangeException` 行为保持不变。

#### Scenario: 枚举值存在
- **WHEN** 审阅 `BotDifficulty` 枚举
- **THEN** 存在值 `Easy=0`、`Medium=1`、`Hard=2`

#### Scenario: Factory 的 Hard 分支
- **WHEN** `GomokuAiFactory.Create(BotDifficulty.Hard, new Random(1))`
- **THEN** 返回 `IGomokuAi`,运行时类型是 `HardAi`

---

### Requirement: `BotAccountIds.Hard` 固定 Guid + 数据库 seed

Application 层 `Gomoku.Application/Abstractions/BotAccountIds.cs` MUST 追加:

```
public static readonly Guid Hard = Guid.Parse("00000000-0000-0000-0000-0000000000ad");
```

`For(BotDifficulty)` 的 switch MUST 覆盖 `Hard => Hard`;`TryGetDifficulty(Guid)` MUST 对该 Guid 返回 `BotDifficulty.Hard`。

数据库 MUST 通过 `AddHardBotAccount` migration 在 `Users` 表插入一行:

| 列 | 值 |
|---|---|
| Id | `BotAccountIds.Hard`(`...00ad`) |
| Email | `hard@bot.gomoku.local` |
| Username | `AI_Hard` |
| PasswordHash | `User.BotPasswordHashMarker`(即 `__BOT_NO_LOGIN__`) |
| Rating | 1200 |
| GamesPlayed / Wins / Losses / Draws | 0 |
| IsActive | true |
| IsBot | true |
| CreatedAt | `2026-01-01T00:00:00Z` |
| RowVersion | 固定 16 字节(migration 写一个常量 byte array;后续 RecordGameResult 会自然推进) |

Migration Down MUST `DeleteData` 该行。

#### Scenario: 三 bot 并存
- **WHEN** 运行 `dotnet ef database update` 后查询 `SELECT Username_Value FROM Users WHERE IsBot=1 ORDER BY Username_Value`
- **THEN** 返回三行:`AI_Easy`、`AI_Hard`、`AI_Medium`

#### Scenario: 反向查询 Hard
- **WHEN** 调 `BotAccountIds.TryGetDifficulty(BotAccountIds.Hard)`
- **THEN** 返回 `BotDifficulty.Hard`(非 null)

#### Scenario: Hard AI 房间创建
- **WHEN** 登录用户 `POST /api/rooms/ai { name: "x", difficulty: "Hard" }`
- **THEN** HTTP 201,`RoomStateDto.White.Username == "AI_Hard"`,`Status == "Playing"`

