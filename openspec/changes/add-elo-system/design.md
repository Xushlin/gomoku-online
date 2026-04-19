## Context

战绩字段等了两个变更都没人写;这次把闭环补上,顺手把排行榜接口也给前端准备好。规模小,决策主要集中在两个地方:**ELO 算法怎么落**(K 因子、浮点舍入、两方非对称)和**落地时机**(同事务 vs 事件驱动)。其余都是标准 CQRS handler + 一个 repo 方法 + 一个 controller。

现状:`User.Rating` 默认 1200,`GamesPlayed` / `Wins` / `Losses` / `Draws` 默认 0,`User` 聚合**没有**任何写入这些字段的领域方法(上个变更刻意留空给本次)。`MakeMoveCommandHandler` 在 `Result != Ongoing` 时只发 `GameEnded` 事件,不改任何 User 状态。

## Goals / Non-Goals

**Goals**

- 对局一结束,双方的 `Rating` / `GamesPlayed` / 对应计数器在同事务里更新完毕。玩家下完一盘棋立即刷新 `/api/users/me` 就能看到新 Rating。
- `EloRating.Calculate` 是**纯函数 + 确定性**:相同入参必产相同出参,便于表驱动测试。
- K 因子按"个人 `GamesPlayed`"分段(业界常见做法),让新手调整快、老手稳定。
- 排行榜可用:`GET /api/leaderboard` 返回 top 100,Rank 字段由服务端分配。

**Non-Goals**

- 历史 Rating 曲线(每局快照)—— 要加表,留给独立变更。
- 段位 / 图标 / 称号 —— 后端只返回数字,前端自己渲染。
- 并发硬化(乐观并发令牌给 `User`)—— 当前接受极小概率的"后写覆盖先写"。
- 对 Draw 做特殊激励 / 惩罚;按 ELO 原本的 0.5 期望分正常处理。
- 禁手 / 规则变种下的 K 调整;等变种功能自己的变更里再改。
- 排行榜分页 / 时间范围 / 好友榜;本次就一个 top-100 平面列表。
- SignalR 推 "RatingChanged" 事件(见 Open Questions)。

## Decisions

### D1. ELO 公式:标准 HS ELO,K 因子按己方 `GamesPlayed` 分段

```
Expected_A = 1 / (1 + 10^((R_B - R_A) / 400))
S_A = outcomeA switch { Win => 1.0, Draw => 0.5, Loss => 0.0 }
R_A_new = R_A + K_A * (S_A - Expected_A)
R_B_new = R_B + K_B * ((1 - S_A) - (1 - Expected_A))
        = R_B + K_B * (-(S_A - Expected_A))   // 对称负号
```

- **K_A / K_B 各自按自己的 `gamesPlayed` 决定**:`<30 → 40`, `<100 → 20`, `≥100 → 10`。这意味着两方不对称 —— 新手输给老手,新手掉 40*(...)、老手赚 10*(...),两者**不相等**。这是业界标准做法(正规赛事也不是完全对称),不 treat 为 bug。
- **舍入**:`Math.Round(double, MidpointRounding.AwayFromZero)` 转 `int`,避免 banker's rounding 让 .5 偏向偶数。这对 ELO 这种对半值频繁出现的场景有可感知差异。
- **无下限约束**:`Rating` 理论可跌到任意值(负数极罕见但不拒绝)。本次不加 floor,需要时再独立加变更。
- **备选**:
  - "两方 K 强制取较小者"(对称版本)—— 否决,违背业界惯例,新手成长太慢。
  - "两方 K 强制取较大者"—— 否决,老手会被新手随机波动拉得很惨。
  - "固定 K=32"—— 否决,新手 30 盘内的成长体感太弱;复杂度没省多少。

### D2. 更新时机:**MakeMoveCommand handler 在对局结束时同事务更新**

- Handler 在看到 `outcome.Result != GameResult.Ongoing` 后:
  1. `var black = await _users.FindByIdAsync(room.BlackPlayerId, ct)`
  2. `var white = await _users.FindByIdAsync(room.WhitePlayerId!.Value, ct)`
  3. `var outcomeForBlack = ResultToOutcome(result)` —— 黑胜 / 白胜 / 平映射到 `GameOutcome`
  4. `var (newBlackRating, newWhiteRating) = EloRating.Calculate(black.Rating, black.GamesPlayed, white.Rating, white.GamesPlayed, outcomeForBlack)`
  5. `black.RecordGameResult(outcomeForBlack, newBlackRating)` / `white.RecordGameResult(Opposite(outcomeForBlack), newWhiteRating)`
  6. **同一** `await _uow.SaveChangesAsync(ct)` —— Room.Game.Finished / Room.Status=Finished / 两位 User 的战绩 + Rating 一起提交
- **为什么不用 MediatR `INotification` + 异步 handler**:
  1. 事务一致性:消息发布 + User 更新不在同一事务,失败补偿复杂。
  2. 多查一次 User,多一次 SaveChanges。
  3. 异步触发让前端有概率看到"Room.Finished 但战绩未更新"的中间态。
  4. 代码量并不少。
- **代价**:MakeMoveCommandHandler 更胖了(+两行 FindById、+一行 Calculate、+两行 Record);但对局结束的 handler 本就是协调节点,胖一点可接受。

### D3. `EloRating` 放 Domain,作为静态纯函数

- `Gomoku.Domain/EloRating/EloRating.cs`:`public static class EloRating`,方法 `Calculate(int ratingA, int gamesA, int ratingB, int gamesB, GameOutcome outcomeA) : (int, int)`。
- 零依赖、零分配(返回值是 struct tuple),纯算术 + 一次 `Math.Pow` + 两次 `Math.Round`。
- 为什么放 Domain:ELO 是 gomoku 玩法的一部分,是领域知识。Application 保持无算法。
- **备选**:Application 里 `EloCalculator`(服务/静态)。否决 —— Domain 已有 `Board.PlaceStone` 这种"领域计算",`EloRating` 性质一致。

### D4. `User.RecordGameResult(GameOutcome, int newRating)` 封装三件事为一步

- 方法内部:
  ```csharp
  GamesPlayed++;
  switch (outcome)
  {
      case GameOutcome.Win: Wins++; break;
      case GameOutcome.Loss: Losses++; break;
      case GameOutcome.Draw: Draws++; break;
      default: throw new ArgumentOutOfRangeException(...);
  }
  Rating = newRating;
  ```
- 单方法保证三个状态变更的原子性 —— 不可能出现"GamesPlayed 增了但 Wins 没增"的中间态;聚合根保护的不变量之一是**三个计数器之和 == GamesPlayed**(本次不在代码里 `Debug.Assert`,测试里验证)。
- **为什么一个方法而非 `RecordWin` / `RecordLoss` / `RecordDraw` 三个**:handler 里会根据一个变量分支调用三次相同模板,不如一个参数化方法清晰。

### D5. `GameOutcome` 枚举:三值,`Loss = 0` 不是默认的失误

- `public enum GameOutcome { Loss = 0, Win = 1, Draw = 2 }`。
- 底层值用显式数字,避免依赖 C# 枚举默认 0 → Loss 的偶然巧合;未来若序列化 / 持久化也稳定。
- **不存库**,只在内存中流动(handler → EloRating → User)。

### D6. 排行榜排序顺序 `(Rating DESC, Wins DESC, GamesPlayed ASC)`

- 主排序按 Rating:直接对应"积分高者排前"。
- 次排序按 Wins:同 Rating 情况下,胜场多者排前 —— 更积极参与。
- 三排按 GamesPlayed ASC:同 Rating / 同 Wins 情况下,总场次少者排前 —— "以少胜多"更显本事。
- Rank 字段 **按排好的顺序从 1 开始分配**,**不处理同分并列**(两位同 Rating / 同 Wins / 同 GamesPlayed 也分前后,按任意 tiebreaker —— EF 会用主键作为最后一道)。本次不做"并列第 X 名"的展示逻辑,前端需要时自己二次分组。
- 默认 limit = 100,由 handler 常量。**不支持 query 参数**,保持契约简单;`add-leaderboard-pagination` 再细化。

### D7. handler 路径在"Game 进入 Finished 之前"还是"之后"?

- **之前**:先算新 Rating 再改 Game.Result? —— 不对,Rating 计算依赖**对局结果**,必须在 `Room.PlayMove` 返回 `MoveOutcome` 后才知道 Result。
- **之后**:`Room.PlayMove` 成功返回 → handler 检查 `outcome.Result` → 做 ELO 更新。这是实现顺序。
- 事务边界仍是**一个 SaveChanges**:EF 把 Room(含 Game)和两位 User 的变更合并到一个 SQLite `BEGIN ... COMMIT`。失败 → 全部回滚。

### D8. Rating **不作为 SignalR 推送的一部分**

- `GameEnded` 事件 payload 保持 `{ Result, WinnerUserId, EndedAt }` 不变。
- 不引入 "RatingChanged" 事件。前端在 `GameEnded` 到来后主动 `GET /api/users/me` 刷新自己的数据,或等下一个 `RoomState` 事件(届时 `UserSummaryDto` 若携带 Rating 字段会自然带上)。
- **`UserSummaryDto` 是否要加 `Rating` 字段?**:当前是 `(Guid Id, string Username)`。Rating 是展示用,前端排行榜 / 房间列表里显示很合理。
  - **本次决定:不加**。理由:`UserSummaryDto` 被多处复用(`Host` / `Black` / `White` / `Spectators` / `PlayerJoined` event / ...),一旦加就会到处携带,对"观众"场景也没必要。前端需要一名玩家的详细数据,查 `GET /api/users/me`(自己)或在排行榜 / `add-user-profile` 变更(他人)时拿。
  - open-question 里保留讨论入口。

## Risks / Trade-offs

- **两方 K 不对称导致"系统积分池"轻微漂移**:老手让给新手的 + 比从新手拿的 - 少,系统长期来看会"印钞"。这是 HS ELO 通用特性,国际象棋协会也接受。Mitigation:无;若严重了加 `add-rating-normalization` 变更做平衡。
- **单 User 并发"参与两盘同时结束"** → 后写覆盖先写。发生率接近 0(本次 `User` 不用乐观并发令牌)。记下来,未来 add-concurrency-hardening 统一加。
- **排行榜查询无缓存** → 每次 `GET /api/leaderboard` 走 DB 扫描 + ORDER BY;`Users` 表量级起来后可能慢。Mitigation:在 `Rating` 列上加索引(如果真慢)。本次不加(dev SQLite 下无感)。
- **浮点精度**:`Math.Pow(10, (R_B - R_A) / 400.0)` 在极端 R 差值(> 800)下精度下降。ELO 本来就不鼓励 800+ 差值的对局,影响有限。测试里覆盖几个边界。
- **测试时钟与 EloRating 无关**:`EloRating.Calculate` 不读时钟,不依赖 `IDateTimeProvider`。测试纯粹。
- **`User.RecordGameResult` 不拒绝负 Rating / 荒唐 newRating**:方法**信任**调用方(必然是 handler,handler 必然从 EloRating 拿)。若要做防御,再加 Guard 一层即可。本次不加。

## Migration Plan

零 DB 迁移。按 tasks 顺序:Domain → Application → Infrastructure → Api。每层可独立 build 并通过自身测试。Api 层合入后冒烟:两位玩家打一盘,对局结束,`GET /api/users/me` 双方都能看到 Rating 变化 + GamesPlayed = 1;`GET /api/leaderboard` 返回两位玩家,Rank 1 / 2。

回滚策略:和前几次一样,任一层 commit 可独立 revert。

## Open Questions

- **是否给 `UserSummaryDto` 加 `Rating` 字段?** 倾向**不加**(理由见 D8)。若前端实操起来觉得房间列表缺 Rating,再单独开一个变更改。
- **排行榜是否过滤 `IsActive == false` 用户?** 倾向**不过滤**(排行榜是全体战绩的纪念碑,被禁用也不抹去历史)。若后续引入"假账号"或"作弊下禁"场景,那时再加 filter。
