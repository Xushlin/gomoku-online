# Tasks — add-tetris

## 1. Domain:方块与场地

- [x] 1.1 `Tetromino`:七种标准方块 × 四个旋转态的格子偏移表。
- [x] 1.2 `TetrisField`:10×20,`CanPlace` / `Drop`(算停在哪一行) / `ClearLines`。
- [x] 1.3 `TetrisPieceSequence`:`seed → 方块序列` 的**纯函数**确定性生成器。

## 2. Domain:重放与计分

- [x] 2.1 `TetrisRules.Replay(seed, placements)` → `(Score, Lines, Level)`。
- [x] 2.2 非法放置 → 整局拒绝(抛),MUST NOT 只跳过那一步。
- [x] 2.3 计分 `1/2/3/4 行 = 100/300/500/800 × 等级`;每 10 行升一级,从 1 起。

## 3. Domain:`ScoreRun`

- [x] 3.1 实体 + 领域方法 `Finish(score, lines, level, now)`,拒绝已结束的 run。
- [x] 3.2 与 `PuzzleAttempt` 逐条对齐:服务端时钟、不可复用、他人的 run 404。

## 4. Application / Api

- [x] 4.1 `StartScoreRunCommand` → 返回 id + seed。
- [x] 4.2 `SubmitScoreRunCommand` → 重放、写分;客户端报的分数忽略。
- [x] 4.3 `GetScoreLeaderboardQuery` → 周期窗口 `week|month|all`。**`week` 是自然周**
      (周一 00:00 **UTC** 起),不是滚动 7 天 —— 见 spec,以及边界必须取 UTC 的理由。
- [x] 4.4 三个端点 + 迁移。
- [x] 4.5 榜上**一个玩家只占一行**(窗口内最高分)。这条 spec 原来没写,是实现时补的 ——
      计分类天生鼓励反复重开,所以"每局一行"的失败模式不是偶发而是必然。

## 5. 测试

- [x] 5.1 生成器:同种子同序列、不同种子不同序列、七种都出现。
- [x] 5.2 重放确定性:同输入两次同结果。
- [x] 5.3 客户端报的分数被忽略(报 999999,实得按放置算)。
- [x] 5.4 非法放置整局拒绝。
- [x] 5.5 计分:1 行 100、4 行 800(**不是** 4×100)、等级放大、每 10 行升级。
- [x] 5.6 `ScoreRun`:不能重复提交、他人 404、时间取服务端。
- [x] 5.7 一条断言:代码里**没有** `IScoreAttackRules` 之类的注册表。
- [x] 5.8 自然周的**边界**:结束于本周一 00:00 UTC 前一秒的一局不在周榜上(距今不足 7 天,
      所以滚动窗口的实现会留下它);周日仍属本周(`(int)DayOfWeek - 1` 会把周日整天甩出去)。

## 6. 验证

- [x] 6.1 `dotnet build` 0 warning;`dotnet test` 全绿。
- [x] 6.2 真实 HTTP:开 run → 拿 seed → 提交一串放置 → 分数正确 → 榜上有名。
- [x] 6.3 同一个 run 提交两次,第二次被拒。
- [x] 6.4 前端零改动。

## 7. Application / Api 这一层怎么落的

### 「没有注册表」是怎么在有两个消费者的情况下保住的

开 run 要判"这个键能不能玩",提交要判"这串放置怎么重放"。两个消费者,一个事实。

分成两份判断就是 `enforce-ai-availability` 那个缺陷的形状:`POST /api/rooms/ai` 接受了成语接龙,
后台每 1500 ms 抛一次 `has no AI`,而超时 worker 还给人发了 **+46 ELO**。那次的修法是让校验去读
`IGameAiRegistry` 而不是新加一个 `SupportsAi` 布尔 —— **手写的布尔是判断,而判断会静静过期**。

这里是同一条纪律的最小形态:`ScoreAttackGames.IsScoreAttackGame` 与 `Replay` 认的是同一个键,
一条测试遍历五个键断言两者结论**逐个相等**。它**不是**注册表 —— 一条分支的 switch 是 switch。
第二款计分游戏出现那天,内核从两个真实现之间长出来,而不是从一个实现加一个假实现之间。

### 客户端报的数字:不是"记得忽略",是**无处可放**

`SubmitScoreRunCommand` 里没有 score / lines / level / duration 字段,`StartScoreRunCommand`
里没有 seed 字段。一条"handler 忽略了 score"的行为测试只能证明**今天的** handler 忽略了它;
一个根本不存在的字段没有明天。断言因此是反射的:命令的公开成员与那批名字**不相交**。

实测那一条比它更硬 —— 提交体里同时塞了 `score: 999999, lines: 999, level: 99,
durationMs: 1, finishedAt: 2030-01-01`,响应回来的是 **`score: 300, lines: 3, level: 1,
durationMs: 979`**:重放算出的三个数字,加服务端时钟量出的用时。

### 生成器的"两份实现"这次真的被两份实现验了

spec 说这个生成器是本游戏唯一容许两份实现的东西,理由是它能用一条测试逐项对齐。
但那句话此前只有一份实现,所以**它自己也没被验过**。

这次用 Python 独立写了一遍 xorshift32 + 七袋 + 场地 + 贪心(就是客户端要做的事),
两边对 seed `20260818` 的**前 21 个方块(三整袋)完全相同**。更强的是端到端那一条:
Python 从**服务端下发的** seed 631482753 算出 46 个放置、预测 300 分,服务端重放出来
也是 300 分 / 3 行 —— 一个方块对不上就几乎不可能同分,而且很可能直接非法。

那份 Python 不进仓库(它是客户端的活),但这个数字在这里,下一个人可以重新量。

### 榜:每人一行,而这条 spec 原来漏了

去重写成"同一玩家没有任何一局比它更好"的相关子查询,而不是
`GroupBy(...).Select(g => g.OrderBy(...).First())` —— 后者在 SQLite 上不保证被翻译,
一旦退化成客户端求值,过滤与分页就都搬进了进程,**而结果照样是对的**。
所以它在 `Gewu.Infrastructure.Tests` 里打真 SQLite,不是在 Application 层对着 mock。

比较必须是**全序**(分数 → 结算更早 → id)。前两级几乎足够,第三级是为了让"同分且同一毫秒"
也只留一行:榜上出现同名两行,是那种看一眼就知道错了、却很难说清为什么的缺陷。

### 一个我写下的机制理由,被实测推翻了

`GetScoreLeaderboardQueryValidator` 上原本写的是:「ASP.NET 的枚举绑定接受数字,所以
`?window=99` 会绑成 `(ScoreWindow)99`,一路走到 `StartOf` 的兜底分支被当成 `all`」。
听起来对,而且我为它写了测试。**实测不是这样**,同一个构建上量的:

| 请求 | 结果 |
| --- | --- |
| `?window=0` / `1` / `2` | **200** —— 数字确实被接受 |
| `?window=3` / `-1` | **400**,来自模型绑定器(RFC 9110 形状) |
| `?window=week` / `WEEK` / `Week` | 200 —— 名字大小写不敏感 |
| `?window=fortnight` / 空 | 400 |

所以数字**是**被接受的,但绑定器会按**已定义值**校验,`99` 根本到不了 `StartOf`。
那条 `IsInEnum` 在这个端点上是够不着的。

修法不是删掉它,而是把防线搬到真正该在的地方:`StartOf` 现在对未定义值**抛**,
不再有一个"当成 all"的兜底 —— 兜底会让一个打错的窗口静静返回全部历史,而那是最不该
发生的那种"成功",并且把正确性押在"上游总记得校验"上。`IsInEnum` 留着,理由改成
它护的是**查询对象**而不是某一种传输(命令可以由任何调用方构造,那些路径上没有绑定器),
职责只是让失败长成一个带字段名的 400。

**一个听起来对的机制描述,和一个量过的机制描述,只在出事那天才有区别。**

### 放置数上限 ≠ 分数上限

分数刻意不设上限(硬上限会先误伤真高手,这是记在 spec 里的决定)。但请求体与重放都是 O(n),
所以放置数有上限 **100 000**,算术依据写在常量上:每个方块按 2 秒算,10 万次放置是
**连续玩 55 小时**。这是资源限制,不是对成绩的怀疑。

### 迁移

`Score` / `Lines` / `Level` 三列**可空**,而这一点是特意核对的:`generalize-match-payload`
正是在这里栽过 —— CLR 类型改成可空了,但配置里还留着 `.IsRequired()`,而**显式配置压过
CLR 可空性**,于是迁移干净地生成、数据库在运行时才拒收。生成出来的迁移确认三列
`nullable: true`,实测库里一个未提交的 run 三列都是真 `NULL`。

`Down` 直接 `DropTable`,而这次那是对的:前两个被修过的 `Down` 都有**别处仍读得到的数据**
要搬回去,这次表本身是新的,回滚这个功能就是回滚这些 run,没有第二个去处。理由写在迁移类上。

## 8. 真实 HTTP 实测(Development,scratch 库,端口 5233)

```
POST /api/score-runs {"gameKey":"gomoku"}   -> 404  'gomoku' is not a score-attack game
POST /api/score-runs {"gameKey":"tetris"}   -> 200  runId + seed=631482753
POST .../submit  (体内塞 score:999999 等)    -> 200  score=300 lines=3 level=1 durationMs=979
POST .../submit  再来一次                    -> 409  was already scored at ...
POST .../submit  由 bob 发                   -> 404  was not found        ← 不是 403
POST .../submit  30 个都堆在 0 列            -> 400  Placement 11: J ... stack is too high
GET  .../leaderboard?window=week            -> 200  2 行,rank 1=300、rank 2=100
GET  .../leaderboard?window=all             -> 200  同上
GET  .../leaderboard?window=99              -> 400  绑定器拒
GET  .../leaderboard?gameKey=not-a-game     -> 200  空榜(不是 400)
POST /api/score-runs 无 token                -> 401
POST .../submit {"placements":[]}           -> 400  errors.Placements
```

库里三行,三个**不同的**种子(631482753 / 742241575 / 206543478)—— 客户端选不了序列这件事
是看出来的,不是断言出来的。其中未提交那一行 `FinishedAt / Score / Lines / Level` 全是 `NULL`。

`PRAGMA foreign_key_list(ScoreRuns)` → `Users(Id)` ON DELETE CASCADE,所以
`GetScoreLeaderboardQueryHandler` 里"有 run 就一定有用户名"那句注释有东西撑着。

测试:**1055 条全绿**(278 Application + 98 Infrastructure + 679 Domain),`dotnet build` 0 warning。
本变更新增 55 条。前端**零改动**。

### 变异验证(改坏实现,确认测试真的会红)

| 改坏什么 | 红了几条 |
| --- | --- |
| 自然周 → 滚动 7 天 | 5(4 条纯函数 + 1 条打库的边界) |
| 榜去重整段删掉 | 2 |
| `FindAsync` 不带所有者 | 1 |
| `IsScoreAttackGame` 多认一个 `gomoku` | 1 |
| 给 `SubmitScoreRunCommand` 加一个 `Score` 字段 | 1 |

## 9. 还没做

**UI。** 与 `add-xiangqi` / `add-idiom-chain` 同样的拆法:规则与端点先落地并能用真实 HTTP 验,
UI 是下一个变更(`add-web-tetris`)。它要做的是游戏循环、硬降预览(用公开的 `TetrisField`)、
提交放置序列,以及榜页面。

## 10. 过程中两次自我纠正,都由变异测试逼出来


### 一、我为一条测不到东西的断言写了理由

计分那两条用例最初是**纯常量算术**,根本不碰 `Replay`。我在注释里为此写了理由:
「构造一个真的消四行的放置序列需要一个求解器,而那会把断言变成'我的求解器算对了吗'」。

变异测试当场证伪:把等级因子换成 1,**32 条全绿**。理由本身也是错的 —— 验等级放大不需要
消四行,只需要在等级 2 消一行。

修法不是硬构造局面,而是把计分抽成公开的 `ScoreForClear(cleared, linesBefore)`:
它是这个游戏对外契约的一部分(分数榜要能被理解),客户端也要显示"这一手多少分"。
现在两个变异各让 2 条与 4 条变红。

**一条断言只测到它真正调用的东西 —— 而"为什么没法测"的理由,自己也需要被检验。**

### 二、脚手架的 bug 与被测物的 bug 长得一样

第一版的放置生成器用 `i % Columns` 取列,于是宽 ≥ 2 的形状落在列 9 越界,两条用例红了。
红的是**脚手架**:规则正确地拒绝了越界。第二版按宽度取模,又在 17 手时堆满。

最后换成"最低优先贪心"—— 它真的消到行,而这只能写出来是因为 `TetrisField` 是公开的,
而它公开的真正理由是客户端要画硬降预览。**为测试需要而暴露的 API 与本来就该暴露的 API,
区别在于有没有第二个消费者。**

## 11. 用户已定的两个设计决定(2026-08-18)

- **不设分数上限。** 任何硬上限都会先误伤真高手 —— 分布最右端既是作弊者也是最强玩家,阈值分不开。
  软上限只是把同一个判断推给一个不存在的人工流程。限制写在明处,不用会伤害正常玩家的机制假装它不存在。
- **周榜按自然周**(周一 00:00 UTC),不按滚动 7 天。自然周有所有人共享的截止时刻,
  滚动窗口会让昨天还在榜上的成绩今天悄无声息地掉下去。**可预期的残忍好过不可解释的漂移。**

两条都已写进 `specs/tetris/spec.md`,含理由与边界场景。
