# add-wakeng

## Why

挖坑的**纯逻辑已经在仓库里了**(`add-wakeng-cards`):大小、八种牌型、压牌、发牌、首叫权、
计分,全部是纯函数,45 条测试。缺的是把它们接到内核上 —— 阶段、叫分、出牌、按座位可见、
超时兜底。

这与 `add-doudizhu` 是同一个形状,而它证明的东西不同。斗地主证明了三个座位 + 隐藏信息 +
规则指定下一手能过同一个聚合;挖坑要证明的是**先手由发牌决定**(`generalize-match-kickoff`
那个 seam 的第一个真实现)、以及**首出权与挖坑权分属两个座位**这件事内核能表达。

## What changes

### 新增 — `backend/src/Gewu.Domain/Games/Wakeng/`

- **`WakengMove`** — 一步棋的文本编码:`bid:0`…`bid:3` / `pass` / `play:<cards>`。
  与斗地主同一个语法,而**是另一个类型**:见下面「为什么不共用」。
- **`WakengTable`** — 从 `MatchState`(发牌 + 走子历史)重建局面。规则因此无状态。
- **`WakengSeatView`** — 一个座位看得到的东西。
- **`WakengRules`** — `IGameRules` + `IDealtGameRules` + `IFirstSeatRules` +
  `ITimeoutFallbackRules` + `IPerSeatViewRules`。**五个接口,而挖坑是第一个实现
  `IFirstSeatRules` 的棋种。**

### 新增 — `backend/src/Gewu.Domain/Games/Cards/CardPlay.cs`(共享)

一件事:**把一串牌解成牌,而畸形的输入是一次领域拒绝、不是一个 `FormatException`。**

它存在是因为 `add-doudizhu` 修过一条真缺陷:`Card.DecodeMany` 对不认识的字符和重复的牌都抛
`FormatException`,而那不是 `DomainException` —— 于是 `play:!!!` 会以未映射异常冒出去变成
**500**,客户端看到「服务器出错了」,而实际上是它自己发错了。斗地主在自己的 `Parse` 里
`catch` 了它。挖坑要写第二个解析器,而**一个需要被记得的 catch 会在第三个解析器那里被忘掉**。

它 MUST 留在 move 层而 MUST NOT 下沉到 `Card.DecodeMany`:`WakengDeal.Decode` /
`DoudizhuDeal.Decode` 也调它,而它们**要的正是 `FormatException`** —— 一份坏掉的发牌是
损坏的记录,不是一步非法的棋。两个调用方要两种异常,所以映射只能在上面这一层。

### 修改 — 三行加一处删

- `Games/Abstractions/IGameRules.cs`:`GameKeys.Wakeng = "wakeng"` 一行常量。
  与 add-xiangqi 以来的每一个棋种同一个形状。
- `Games/NInARow/NInARowRules.cs`:`BuiltInGameRules.All(...)` 里多一个实例。
  **DI 不动** —— 它从 `All` 派生。
- `Games/Doudizhu/DoudizhuMove.cs`:那个 `try/catch` 换成调 `CardPlay` —— **净删代码**。

### 六条「恰好一个」的注册表走查会红,按它们自己的注释改

它们全都在预言这一天:

| 测试 | 现在 | 改成 |
| --- | --- | --- |
| `FirstSeatTests.No_built_in_game_picks_its_first_seat_yet` | 没有棋种实现它 | 恰好一个(wakeng) |
| `FirstSeatTests.Every_built_in_game_still_starts_at_seat_zero` | 每个棋种都从 0 开 | 没有 seam 的每个棋种都从 0 开 |
| `GameSetupTests.Exactly_one_built_in_game_deals_a_setup` | 恰好一个 | 恰好两个 |
| `TurnFlowTests.Exactly_one_built_in_game_falls_back_on_timeout` | 恰好一个 | 恰好两个 |
| `GameSetupMigrationTests.Exactly_one_built_in_game_can_produce_a_non_null_setup` | 恰好一个 | 恰好两个 |
| `AiSmoke` step 8/9 | 只报斗地主 | 也报挖坑 |

第五条那一句注释写的是「第二个棋种要设置的那天这条会红 —— **那时这笔账变大,该重新估**」。
本变更 MUST 真的重估并把结论写下来,而不是只把 1 改成 2:`AddGameSetup` 的 `Down` 会丢掉
**两个**棋种的发牌,而回滚再前滚之后房间看起来还能玩、规则在下一手抛。

## 不做什么

- **没有 AI。** 与斗地主同理,而理由更硬:一个能算牌的机器人在没有炸弹、跟牌必须同型同张的
  牌型下强得离谱。`enforce-ai-availability` 会让 `POST /api/rooms/ai` 返回 400,**不需要
  任何新代码** —— 不在 `BuiltInGameAis.All` 里就够了。
- **不计分(`IsRated == false`)。** 结构性理由,与斗地主逐字相同:ELO 是两人模型,挖坑按分
  结算。这也让 `IsRated ⇒ SeatCount == 2` 保持成立,不必开例外。
- **没有 UI。** `add-web-wakeng` 的事。牌桌组件大概能复用,底牌是 4 张而不是 3 张。
- **`WakengScoring.Settle` 仍然没有生产调用方** —— 与 `DoudizhuScoring.Settle` 一样。
  这笔账现在涉及两个棋种,触发条件不变:平台需要一条**点数榜**的那天。

## 三处判断,写下来因为它们不是推导

1. **首出权归首叫者,不归挖坑者。** 原文:「持有 ♣4(拿底牌前最小的 ♣ 牌)的玩家获得
   **首叫权和首出权**」。这与斗地主相反(那边地主先出),而它正是 `IFirstSeatRules` 存在的
   理由的另一半:内核的首手座位是**首叫者**,叫分结束之后出手权**回到同一个座位**,
   而不是给挖坑者。
2. **三家都不挖时第一家挖,兜底 1 倍**(用户定的)。于是**挖坑没有流局** —— 斗地主三家不叫
   是和局,挖坑不是。一条断言钉住 `MoveApplication.Drawn()` 在这个棋种上永不出现。
3. **基数不进 `seatView`。** 它今天恒等于 `WakengScoring.DefaultBase == 1`,而那不是这一局的
   *状态*,是一个还不存在的房间设置。发一个只有一个取值的字段,等于请客户端画「×1」;
   将来它成为设置时,它属于**房间**而不属于按座位的视图(三个座位看到的是同一个数)。

## 为什么不共用 `DoudizhuMove`

两个游戏的走子文本**语法一模一样**,而它们**不是同一个事实**。

`hoist-card-model` 搬走 `Card` 的理由是挖坑**真的在用同一批值** —— 同样 52 张、同样的编码
字母表、同一个 `DecodeMany`。那是一个事实。`WakengMove` 与 `DoudizhuMove` 产出**不同的
字符串**,喂给**不同的规则**,没有任何一段代码同时读两者:共享的只有形状。
**形状相同不等于事实相同**,而这正是 `hoist-card-model` 拒绝把 `TetrisPieceSequence` 并进
`CardShuffle` 时用的同一条判据 —— 按「是不是同一件事」分,不按「代码长得像不像」分。

它们**可以分歧**(挖坑哪天要 `bid:4`,斗地主一行不动),而「分歧是允许的」就是「这不是一个
事实」的检验。

共享唯一真正必要的那一小块 —— 那条 `FormatException` 映射 —— 已经提成 `CardPlay`,
因为**它的重复会重造一个量过的缺陷**。

**触发条件:第三个牌类棋种。** 那时这笔风险付过三次,而账要重算。

## 验收标准(继承自 `add-doudizhu`)

- `git status backend/src` 下 **`Rooms/` 零改动**;`Games/Abstractions/` 只多一行常量。
  两条源码级断言分别验,并且各带一条「文件集非空」的检查 —— 路径写错的话它们会空转通过。
- `WakengThroughRoomTests` 用**真 `Room`** 打一整局真挖坑。
- 五个现有棋种一行不动。
