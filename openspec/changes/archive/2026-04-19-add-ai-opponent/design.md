## Context

前四次变更已经把"真人 vs 真人"整条流水线打穿 —— 房间 / 对局 / 聊天 / 围观 / 催促 / ELO / 排行榜都是工作态。现在要把"机器人"插进来,最关键的设计选择是:**机器人到底是不是一个 User**?如果是,下游所有现有代码(SignalR group、`Room.JoinAsPlayer`、`Room.PlayMove`、ELO 计算、DTO、聊天权限)几乎不需要改。如果不是(引入独立的 `Bot` 实体 / `PlayerSlot` 联合类型),上层路径几乎要全重写。

这份 design 先把"bot 就是 User"确立为 D1,随后顺着"bot 怎么决策 / 怎么被触发 / 怎么和 ELO 和榜单共存"往下拆。

## Goals / Non-Goals

**Goals**:
- 人类玩家能通过一个新端点一键创建 AI 房间,立即进入 Playing 状态。
- Bot 在轮到自己的回合时**在数秒内**自动下子,下子过程经过完整 `Room.PlayMove` 校验 / ELO 结算 / SignalR 广播链路。
- 围观者 / 聊天 / 催促等现有能力在 bot 局里**继续按现规则工作**,不做特殊分支。
- 提供 2 个难度(`Easy` / `Medium`),策略差异足以被玩家感知但实现朴素。
- Domain 层保持 0 第三方依赖、同步纯函数、无 IO。

**Non-Goals**:
- `Hard` 难度(深度搜索):留给下一次变更。
- AI 的"性格"(聊天、催促反应、GG):完全不做。
- AI 房间的 TTL / 空房清理:现有 room 清理逻辑本就没有,bot 局也不特殊化。
- Bot 参与排行榜 / 前三名图标:明确过滤。
- 并发限流:后台 worker 单线程串行处理命中的房间,首版不并行;有必要时后续变更加。
- 难度自适应 / MMR 匹配:完全不做。

## Decisions

### D1 — 机器人 = User(带 `IsBot` 标志)

用户聚合加 `IsBot: bool`。Bot 账号走和真人一样的 Users 表、一样的 `UserId`、一样的 Domain 方法。Room.BlackPlayerId / WhitePlayerId 照旧是 `UserId`,**对下游几乎零改动**。

**考虑过但弃用**:
- **独立的 `Bot` 聚合 + Room 用联合类型 `PlayerSlot = Human(UserId) | Bot(BotId)`**:最"纯粹"但 Room、所有 Command / Query / DTO / SignalR 事件都要换类型,改动爆炸。现有 ELO 更新代码也要按 Human/Bot 分叉。
- **预留 `UserId` 值的"魔法 Guid"**(比如全零 Guid 表示 bot):避免数据库列,但要在 Application / Infrastructure 多处特判,侵入性比 `IsBot` 更严重。

**代价**:Login handler、Leaderboard query 都要显式过滤 `IsBot`。可接受。

### D2 — Bot 账号在 migration seed,固定 Guid

`AddBotSupport` migration 把两个 bot 的 `Guid` 值硬编码到 `modelBuilder.Entity<User>().HasData(...)`,作为编译期常量暴露在 `Gomoku.Application.Abstractions.BotAccountIds` 静态类:

```csharp
public static class BotAccountIds
{
    public static readonly Guid Easy   = Guid.Parse("00000000-0000-0000-0000-00000000ea51"); // "easy"
    public static readonly Guid Medium = Guid.Parse("00000000-0000-0000-0000-0000000bed10"); // "medium"
}
```

Worker / CreateAiRoomHandler 都按这个静态类找 bot。用户名取 `AI-Easy` / `AI-Medium` —— 但注意现有 `Username` 值对象规则只允许 `[a-zA-Z0-9_]` + BMP CJK,**不允许连字符**,所以真实落盘是 `AI_Easy` / `AI_Medium`。

**考虑过但弃用**:
- **Bot 账号启动时 `ensure-exists`**:要在 `Program.cs` 启动路径查库 + 可能插入,单元测试与 migration 本身都要加判空路径,复杂度不值得。
- **Bot 账号按难度懒创建**:第一次 CreateAiRoom 才建,节省 2 行记录但每次 CreateAiRoom 都要查+可能写,延迟 & 并发控制都麻烦。

`PasswordHash` 字段用占位常量 `"__BOT_NO_LOGIN__"` —— 这不是任何合法 PasswordHasher V3 输出,天然不可登录。Login handler 还加 IsBot 显式拒绝(D8),双保险。

### D3 — 新端点 `POST /api/rooms/ai`,不复用 `/join`

路径:`POST /api/rooms/ai`,body `{ name: string, difficulty: "Easy"|"Medium" }`,成功 201 + `RoomStateDto`(**不是 `RoomSummaryDto`**,因为 Status 一上来就是 Playing,前端拿到就可以渲染棋盘)。

为什么不是"`POST /api/rooms` 造房间 + bot 自动 `POST /api/rooms/{id}/join`":
1. 两步对前端是无谓的往返。
2. 现有 `/join` 端点从 JWT 取 `userId`;要让 bot 调用 `/join` 要么 bot 走 JWT(D8 禁止)、要么加特殊 Controller action("以 bot 身份 join this room"),都不如一步到位。
3. 服务端原子地"建房 + 加入"比先后两个命令更难进入"房间建了但 bot 没加入"的中间态。

### D4 — 后台 `IHostedService` 轮询 vs. 事件驱动

选**轮询**,按用户方向:

| 维度 | 轮询 (D4 选) | MoveMade 事件触发 |
|---|---|---|
| 代码定位 | 一个独立 HostedService | MakeMoveHandler 里加 "if 对手是 bot then 调度" 分支 |
| 观感 | 天然"思考时间"(轮询间隔 + `MinThinkTimeMs`) | 瞬发,必须额外延迟 |
| 机器人**首步**如何触发 | bot 是白方,白方先手从不会到,但黑方 bot 的首步 需要 "CreateAiRoom 后即启动第一步"—— 轮询自然覆盖 | CreateAiRoomHandler 结束时要显式 "如果 bot 是黑方,立即触发";加分支 |
| 失败重试 | 下一轮自然重试 | 要引入重试队列 |
| 并发 | 单线程 worker 无并发问题;多房间串行 | 每次 MoveMade 都发一个 async,bot 间并发不可控 |
| 观察性 | 轮询一次就能做一次 "这轮我看到了 N 个 bot 回合" 的日志 | 散落在各个 handler |

轮询的缺点是**空载开销**:没 bot 局时也每 1.5s 查一次 `WHERE Status=Playing AND (Black.IsBot OR White.IsBot)`。实测在 dev SQLite 下这种查询 ms 级,不是问题;加上索引后生产也无压力(Status 已有索引)。

### D5 — AI 决策纯在 Domain,不产生副作用

```csharp
public interface IGomokuAi
{
    Position SelectMove(Board board, Stone myStone);
}
```

- **入参**:一份 `Board` 快照(调用方 replay 出来,或对外面 Board 调 `.Clone()`)+ 自己的棋色。
- **出参**:一个合法 `Position`(空格、在盘内)。
- **副作用**:零。不修改 Board、不读时钟、不读取存储、不 IO。
- **随机性**:`EasyAi` 构造时接收 `Random`(`GomokuAiFactory` 默认注入 `new Random()`,测试注入固定种子)。`MediumAi` 确定性 + 在并列最高分里用 `Random` 破平。

这让 AI 能被**表驱动测试**穷举典型盘面,和 EloRating 的测试风格一致。

### D6 — AI 决策只在 Application 层触发

AI 选点本身是 Domain 的事,但"从 Room 加载 Moves、replay Board、喂给 AI、把结果变成 MakeMoveCommand"是应用层职责。因此 `ExecuteBotMoveCommandHandler` 住在 `Application/Features/Bots/`;它不直接调 Domain 的 `Room.PlayMove`(那会绕过 `MakeMoveCommand` 的 validator、通知、事务),而是**通过 `ISender.Send(new MakeMoveCommand(...))` 再发一次**,复用现有全套路径。

### D7 — Bot 不进排行榜,但吃 ELO

**排行榜**:`UserRepository.GetTopByRatingAsync` 加 `Where(u => !u.IsBot)`。`GetLeaderboardQueryHandler` 代码不变 —— 过滤在仓储层,DTO 里的 Rank 依旧从 1 起。

**ELO 是否更新**:**照正常规则更新**。不给 bot 特权。

考虑过"bot Rating 永远冻结 1200":最符合"bot 不参与竞争"的直觉,但带来两个问题:
1. `User.RecordGameResult` 要分叉,或 handler 要判 IsBot → `EloRating.Calculate` 也要判 → **Domain 纯度受损**。
2. bot Rating 不更新会形成**套利**:人打赢 bot 总加分(K=40 时 +20),打输也只失一次,**刷 bot 上分**。

选"正常更新" + "不上榜"的组合:刷 bot 会把 bot Rating 拉下来,进而让人继续打赢的加分贴近 0(ELO 期望贴近 1) —— 自带反套利。

### D8 — Bot 账号不可登录

`LoginCommandHandler` 在**密码校验通过之后**(或干脆之前 —— 语义上都等价,因为 `__BOT_NO_LOGIN__` hash 永远不匹配)再检查 `if (user.IsBot) throw new InvalidCredentialsException(...)`。返回一模一样的"账号或密码错"文案,避免攻击者枚举 bot 账号存在性。

同样,**`/api/auth/refresh`** 走的是"按 token hash 查 User"路径。Bot 没有 token(`User.IssueRefreshToken` 不会被调),理论上不可达;为稳妥起见在 `RefreshTokenCommandHandler` 也加 `if (user.IsBot) throw` —— 零成本防御。

`/me` / 任何其他 `[Authorize]` 端点理论上 bot 用户 Id 都可能被 JWT 携带;但 bot 没有 refresh token 能拿到 access token,链路断在第一步。

### D9 — 思考延迟 = 轮询间隔 + `MinThinkTimeMs` 防抖

现有 `Room.Game` 有每步 `PlayedAt`。Worker 命中"轮到 bot 的 Room"时计算 `now - lastPlayedAt`:
- 若对局第一步(`Moves.Count == 0` 且 bot 是黑)或 Moves 非空但最后一步是 bot 自己走的(理论上不会发生,因为如果轮到 bot,最后一步必是对手的),按默认路径走。
- 若 `now - lastPlayedAt < MinThinkTimeMs`(默认 800ms),跳过这次 poll。下次 poll 时再检。

合上 `PollIntervalMs=1500`,观感是对手下完棋后 **0.8–2.3s** 之间 bot 回应,不机械。

### D10 — Worker 的异常处理

Worker 里每个 Room 的 `ExecuteBotMoveCommand` 单独 try/catch:
- **`DbUpdateConcurrencyException`**:被真人同时落子抢先 —— 吞掉,下一轮自然 retry。
- **`InvalidMoveException`**:AI 选了非法点(Bug)—— 记 Error 日志,**把该 room 标记为 `BotErrorRoom` 暂时性黑名单**(当前轮次内),避免死循环往同一房间写同一非法子;下次 worker 启动恢复。
- **`NotYourTurnException`** / **`RoomNotInPlayException`**:查询到 worker 执行之间对手"认输"(本变更无)或 EF 缓存过期 —— 吞掉。
- **`OperationCanceledException`**(stopToken 触发):冒泡让 `BackgroundService` 正常退出。
- **其他**:Error 日志 + 继续下一 room,不终止 worker。

首版不做 BotErrorRoom(因为 EasyAi / MediumAi 在 `SelectMove` 的实现上能保证永远不选已有子的格子;测试覆盖)。真有 bug 先让它 log 刷屏,下次变更再限流。

### D11 — 并发:两位 bot 同房?

**不允许 AI-vs-AI**。`CreateAiRoomCommand` validator 断言 `HostUserId` 对应的 User `IsBot == false`。这意味着房间最多有一个 bot,worker 不会遇到"两个 bot 同时在一个 Room 等对方"。

技术上,如果有人手动插入两个 bot 的 Room,worker 的轮询会把该 Room 的 bot 交替驱动到终局 —— 是个有趣的副作用,但用户场景不需要。

### D12 — AI 难度的代码位置

- `Gomoku.Domain/Ai/BotDifficulty.cs` —— 枚举,Domain 类型
- `Gomoku.Domain/Ai/IGomokuAi.cs` / `EasyAi.cs` / `MediumAi.cs` —— 算法
- `Gomoku.Domain/Ai/GomokuAiFactory.cs` —— `Create(BotDifficulty)` 返回接口实现
- `Gomoku.Application.Abstractions.BotAccountIds` —— 把难度和 Guid 绑定的**唯一**地方

新增 `Hard` 的增量:Domain 加 `HardAi.cs`、枚举加 `Hard=2`、Factory 加 case、`BotAccountIds.Hard`、migration seed 第三行 —— 不需要碰 worker 或 handler。

## Risks / Trade-offs

| 风险 | 影响 | 缓解 |
|---|---|---|
| Worker 轮询空载查询 | I/O 浪费 | `Where Status=Playing` 命中 Status 索引;SQLite 实测可忽略 |
| Worker 在 EF 事务里和 handler 争锁 | 可能偶发 `DbUpdateConcurrencyException` | 加 try/catch,下轮 retry;和现有"人 vs 人并发落子"的机制一致 |
| MediumAi 的启发式对人类太弱 | 挫败感 | 不是目标;Hard 难度交给下一轮。前端文案要明确 "Medium 是一档偏练习" |
| bot seed 的固定 Guid 如果被本地库早就用于真人 | migration `HasData` 冲突 | 选的 Guid 前缀全 0,生产不可能冲突;dev 库若冲突删 SQLite 文件重来 |
| bot 账号的 `Username` 冲突 | 真人用户可能已经占用了 `AI_Easy` | Username 规则允许该形式;migration `HasData` 运行时若冲突会 FK/唯一约束抛,属 seed 失败 —— 实测未触发;生产通过**首发 migration 即含 bot seed** 规避,不做运行时 ensure-exists |
| Worker 启动时 seed 尚未应用 | 查不到 bot → `CreateAiRoomCommand` 抛 UserNotFound | Program.cs `app.Services.GetRequiredService<GomokuDbContext>().Database.Migrate()` 在 `app.Run()` 前调用(现有已如此);worker 用 `IUserRepository` 查不到再抛,日志提示"是否 migration 未跑完" |

## Migration Plan

- 既有 `InitialIdentity` 加 `AddRoomsAndGameplay` 两个 migration 不动。
- 新 migration `AddBotSupport`:
  - `AddColumn Users.IsBot BOOL NOT NULL DEFAULT 0`
  - `HasData` 插两行 bot User
- 首次启动 `Database.Migrate()` 自动应用。老库追加一列,默认值 0,现有真人不受影响。
- 回滚:`dotnet ef migrations remove` 能正常摘除,因为 `AddBotSupport` 之后没有其它 migration。

## Open Questions

1. **难度选项**:首版是 `Easy + Medium`(D12 推荐),还是**只先做 `Easy`**(更快落地,4–6 个测试就够)?
2. **Bot 是否吃 ELO**:D7 推荐"照吃、不上榜"。如果更偏向"bot Rating 永远冻结 1200",请明说 —— 我会改 handler 分支(Domain 不改)。
3. **思考延迟**:`MinThinkTimeMs=800` + `PollIntervalMs=1500`(D9)的手感(0.8–2.3s 回应)是否合适,还是想更快(500 + 1000)/ 更慢(1500 + 3000)?
