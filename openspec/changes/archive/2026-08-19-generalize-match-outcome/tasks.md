# Tasks — generalize-match-outcome

## 1. 枚举与规则接缝

- [x] 1.1 `GameResult` → `{ Ongoing = 0, Decided = 1, Draw = 3 }`
- [x] 1.2 一条测试断言枚举成员名恰好是 `{Ongoing, Decided, Draw}`(防有人把颜色加回来)
- [x] 1.3 `MoveApplication(GameResult Result, int? WinnerSeat)`,构造器强制 `WinnerSeat != null ⇔ Result == Decided`
- [x] 1.4 `MoveApplication` 的非法组合各有一条测试
- [x] 1.5 三个工厂 `Ongoing()` / `Won(seat)` / `Drawn()`,并有一条测试钉住它们落在合法组合上

## 2. 盘面家族变短

- [x] 2.1 `Board.PlaceStone` 判胜时返回 `Decided`(删掉 `move.Stone == Black ? … : …`)
- [x] 2.2 `HardAi.IsWinForStone(result, stone)` 整个删掉 —— 它的两个调用点各自知道刚落的是谁
- [x] 2.3 `MediumAi` 两层(自赢 / 堵五)去掉 `myWin` / `oppWin` 常量
- [x] 2.4 `TicTacToeBoard.IsWinFor(result, stone)` → `IsWinForMover(result)`,少一个参数
- [x] 2.5 `NInARowRules.Apply` 判胜时 `WinnerSeat = seat`
- [x] 2.6 `XiangqiRules.Apply` 同上;`IdiomChainRules` 改用 `MoveApplication.Ongoing()`

## 3. 聚合根

- [x] 3.1 `Room.PlayMove`:`winnerId = application.WinnerSeat is int s ? PlayerAt(s) : null`
- [x] 3.2 `Room.Resign`:按座位推对手,结果 `Decided`
- [x] 3.3 `Room.TimeOutCurrentTurn`:同上
- [x] 3.4 `Resign` / `TimeOutCurrentTurn` 在 `Seats.Count != 2` 时抛 `SeatCountNotSupportedException`(码 `seat-count-not-supported`)
- [x] 3.5 三座位赢家取自 `PlayerAt(2)` 的测试(`RoomOutcomeTests`)

## 4. Application

- [x] 4.1 `GameEloApplier` 按 `WinnerUserId` 判胜负;`Decided` 而赢家不属于两位玩家时抛
- [x] 4.2 顺手删掉 `result` 参数 —— 结果与赢家都从 `room.Game` 读(见 §8)

## 5. 迁移

- [x] 5.1 `UPDATE Games SET Result = 1 WHERE Result = 2`
- [x] 5.2 `Down` 从 `RoomSeats` 反查座位把颜色算回来,而不是 `defaultValue`
- [x] 5.3 迁移测试:先手胜 + 后手胜 + 和局,过一遍再回滚一遍;外加"赢家坐 2 号时回滚必须拒绝"

## 6. Web

- [x] 6.1 `GameResult` 类型改成 `'Ongoing' | 'Decided' | 'Draw'`
- [x] 6.2 `game-ended-dialog` 按赢家判胜(数据里 `mySide` → `myUserId`)
- [x] 6.3 `room-page` 的音效用**同一个**判据 —— 抽成 `outcome.ts` 的 `myOutcome`
- [x] 6.4 ~~三处战绩显示~~ **不需要改**:见 §8,它们本来就在比 `winnerUserId`
- [x] 6.5 前端测试 fixture 里的 `'BlackWin'` 全部替换
- [x] 6.6 补 `outcome.spec.ts`;补 room-page 的三条胜负音效断言(变异测试逼出来的,见 §8)

## 7. 验证

- [x] 7.1 `dotnet test Gewu.slnx` 全绿 —— 1166(Domain 770 / Application 279 / Infrastructure 117)
- [x] 7.2 `npx ng test --no-watch` 744 全绿;`npm run lint` 通过;bundle 473.14 kB / 预算 480 kB,无告警
- [x] 7.3 变异测试:12 条后端 + 4 条前端,逐个改坏
- [x] 7.4 `openspec validate --strict` 通过

## 8. 实现记录

### 那两个值是**两处镜像**,而这是先数了它们才知道的

动手前先数 `BlackWin` / `WhiteWin` 在 src 里的 18 处引用,**每一处都是同一个式子**
`stone == Stone.Black ? BlackWin : WhiteWin`,而那个 `stone` 逐处核对下来都是**刚走这一步的人**:

| 位置 | 颜色来自 |
| --- | --- |
| `Board.PlaceStone` | `move.Stone` —— 它自己的入参 |
| `HardAi` / `MediumAi` / `TicTacToeBoard` | 刚试走的那颗子 |
| `XiangqiRules` | `side`,即走子方 |
| `Room.PlayMove` | 映回黑 / 白玩家,再写进 `WinnerUserId` |

所以问题不是"枚举少了第三个值",是同一个事实存了两份。**先问"这个值是从哪来的"而不是
"怎么加第三个值",答案就从"加一个 `Seat2Win`"变成了"删两个"** —— 而三座位那个洞顺手就补上了。

代码因此**变短**:`HardAi.IsWinForStone` 整个删掉,连带两个它自己注释为「不可能 —— 防御式」
的分支(注释是对的,而现在它们连表达都表达不出来);`TicTacToeBoard.IsWinFor` 少一个参数。

### 一处**真的**需要知道走子方的地方

`TicTacToeHardAi.TerminalScore(result, myStone, depth)` 拿到的 `result` 来自落 `toMove` 的一手,
而 `toMove` 可能是我也可能是对手 —— 这里 `Decided` 确实不够,必须把走子方传进来。改成
`TerminalScore(result, mover, myStone, depth)`,两个调用点分别传 `myStone` 与 `toMove`,与旧行为
逐项等价。穷举验证那条测试(每个可达局面都要落在博弈论值上)因此保持原样通过。

同理 `TicTacToeHardAiTests` 里收集终局的 `List<GameResult>` 变成 `List<Outcome>`(结果 + 走子方)。
**这不是"为了编译过"的适配,是那个信息本来就该在那儿** —— 它此前是从 `PlaceStone` 抄回来的入参里
读的。

### `GameEloApplier` 顺手少了一个参数

它此前是 `ApplyAsync(room, result, …)`。赢家要从 `room.Game.WinnerUserId` 读(那是唯一真源),
而结果如果继续从入参读,就变成**两个事实来自两个地方**。三条结束路径都是先 `FinishWith` 再调它,
所以两个都从 `room.Game` 读,并在拿不到对局时抛。三个调用点各短一截。

`Decided` 而赢家不属于两位玩家时**抛**,不猜一方获胜 —— 那种状态是聚合出了错,静默算一次分
会把错扩散进评分。

### 迁移:EF 生成了一个**完全空**的迁移

列的类型、可空性、约束一个都没变,变的只是那些数字的**含义**。迁移生成器看的是模型,不是语义,
所以一次纯值域重映射对它是隐形的 —— 与 `RenameMoveStoneToSeat` 里 `Games.CurrentTurn` 的位移
同一类,那次也是存储类型没变、生成器什么都没写,而漏掉它会让每一局的先后手整个翻过来。

`Down` 把颜色**算回来**(赢家坐 1 号 → 旧 `WhiteWin`),这本身就是本次改动的论据:那个颜色
一直是可以从座位算出来的。赢家坐 2 号在旧枚举里没有表示,所以 `Down` 先拒绝。

### 一条本仓库的机制,注释说得比实际强

`AddMoveTextPayload` 的同款回滚守卫把它写成「表名就是错误信息」。实测 SQLite 报的是
`CHECK constraint failed: ok = 1` —— **只有约束表达式,没有表名**。那次的测试只断言了异常类型,
所以没人发现注释与实际不符。

给 CHECK 约束**起名**之后,SQLite 报的就是那个名字,于是"信息在错误里"这句话才成立。本次的守卫
这么做了,并由一条断言消息内容的测试钉住(变异回匿名约束会让它红)。已合并的那个迁移不动 ——
硬规矩,而且它的拒绝本身是对的,差的只是诊断。

### 变异测试逼出来的三件事

**一、前端胜负音效没有任何测试。** 把 `case 'win': play('game-win')` 改成 `play('game-lose')`,
744 条里一条都不红。弹窗标题有测试,**紧挨着它响的那个声音没有** —— 而这两个恰好是"分歧了会被
听出来"的一对。补了三条(胜 / 负 / 和)。

**二、我自己新写的 ELO 守卫没有测试。** 把 `GameEloApplier` 里那条 `when w == whiteId` 改成
通配 `_`(于是任何未知赢家都算白方输),整个 Application 套件不红。**新加一个守卫和给它一条
测试,是两件事** —— 而我在第一轮里只做了前一件,然后在这份文件里写下了它是 RED。补了
`EloWinnerGuardTests`:一个"计分的三座位"探针规则让 2 号座位赢,于是赢家既不是 0 号也不是 1 号,
走的是聚合的正常路径而不是反射改私有字段。补完之后同一处变异变红。

**三、`myOutcome` 需要 `winnerUserId !== null` 那个守卫。** 去掉之后 `null === null` 会让
"赢家为 null、我也没登录"读成**我赢了**。`Decided` 恒有赢家(服务端构造器强制),所以这一对本不该
到达客户端;但一个恒真的比较不该靠上游保证。

变异结果(逐条改坏,期望变红):

```
RED  MoveApplication 不再拒绝「判胜却没有赢家」
RED  MoveApplication 不再拒绝「没判胜却带赢家」
RED  MoveApplication 不再拒绝负数座位
RED  Room 把赢家硬写成 0 号座位
RED  Resign / TimeOut 不再检查座位数
RED  Board 判胜时改回 Ongoing
RED  GameResult 把 Draw 的底层值挪到 2
RED  ELO 不再校验赢家属于两位玩家（第一轮 GREEN，补 EloWinnerGuardTests 之后才红）
RED  迁移 Up 不做值重映射
RED  迁移 Down 不按座位算回颜色
RED  迁移 Down 的三座位守卫失效
RED  迁移 Down 的 CHECK 约束不再具名
RED  myOutcome 把「我是赢家」恒为真
RED  myOutcome 去掉 null 赢家的守卫
RED  弹窗把「我赢了」画成败
RED  房间页胜负音效反过来（第一轮 GREEN，补三条音效断言之后才红）
```

### 我预测错的一处:三处战绩显示**不用改**

tasks 6.4 原本列着 `my-recent-games` / `games-list` / `replay-page`。去看了,三处都已经在写
`g.winnerUserId === this.userId()` —— 它们**从来没用过那个颜色**。要改的只有房间页那两处
"我赢了没有",以及它们的 spec fixture。**是去查而不是照模式推,才发现这一点。**

### 顺手订正的 spec 漂移(不是本变更的目的,但绕不开)

MODIFIED 会整条替换 requirement,所以要重写一条就得把它写对。写的过程中撞上三处已经过期的:

1. `game-rules-registry` 的 `IGameRules.Apply` 仍写着 `Stone side` —— `generalize-match-seats`
   只改了 `IGameRules` 那一条 requirement,没回头看**同一个文件里第二处描述同一个签名**的地方。
2. 同文件的「`MoveIntent.From` 可空」仍是 `generalize-match-payload` 之前的签名。那次**新增了**
   一条正确的 requirement,却把这条错的留在原地 —— 同一个事实两条 requirement、其中一条是旧的。
3. `elo-rating` 里那段「平台此刻只有一个评分池……这一条是限期约束,`add-per-game-rating` 之后
   MUST 被重写」,是 **`add-per-game-rating` 自己写进去的**:它把整条 requirement 重写了一遍,
   却把"本段必须由本次改动重写"这句话留在里面。**一个把自己指定为自己拆除条件的段落,在触发条件
   与它同处一次改动时最容易活下来。**

`web-game-board` 那条把整段源码抄进 spec 的 requirement 也一并订正(`row`/`col` 可空、`text`、
`Connected5` 早已改名 `Decided`)。**一条抄源码的 requirement,会在那段源码每次变化时静静过期。**

三处漂移期间 `openspec validate --specs --strict` 一直是 38/38 全绿 —— 它校验的是 spec 的**形状**,
不是 spec 的**真假**。这已经是本仓库第三次记下这句话。
