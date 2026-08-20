# wakeng 的规格变化

## ADDED Requirements

### Requirement: 叫分决定挖坑者,而三家都不挖时第一家兜底

`WakengRules` SHALL 以叫分开局:每家依次报 `0`(不挖)/ `1` / `2` / `3`,报的分 MUST 高于
当前最高分才算数(`0` 永远合法),最高者成为**挖坑者**并收下 4 张底牌(手牌 16 → 20)。

叫分 SHALL 在两种情况下结束:**有人叫到 3**(没人压得过,再问一遍是浪费一次交互),
或**三家各叫过一次**。

**三家都说不挖时,首叫者挖,叫分记 1 分。** 原文没写这种情况,这是一处判断(不是重新发牌);
于是**挖坑没有流局** —— 斗地主三家不叫是和局,挖坑不是。`MoveApplication.Drawn()` MUST NOT
在这个棋种上出现,而这条 SHALL 有断言:一个照抄斗地主流局分支的实现在别处全都是绿的。

底牌 SHALL 在挖坑者定下**之后**才公开。叫分阶段它 MUST 为 `null` —— 那时它还没被翻开,
而它恰恰决定了这一局值不值得挖。

#### Scenario: 叫到 3 立即结束叫分
- **WHEN** 首叫者报 `bid:3`
- **THEN** 他是挖坑者,另两家不再被问;底牌进他手里,共 20 张

#### Scenario: 三家都不挖则首叫者兜底
- **WHEN** 三家依次报 `bid:0`
- **THEN** 挖坑者是**首叫者**,叫分为 `1`,阶段进入出牌 —— 而 MUST NOT 是和局

#### Scenario: 压不过当前最高的叫分被拒
- **WHEN** 已有人报 `2`,下一家报 `2` 或 `1`
- **THEN** MUST 抛 `InvalidMoveException`;报 `0` 或 `3` 仍然合法

---

### Requirement: 首出权归首叫者,不归挖坑者

`WakengRules` SHALL 在叫分结束时把出手权交给**首叫者**(持最小 ♣ 的那个座位),
而 MUST NOT 交给挖坑者。

原文:「持有 ♣4(拿底牌前最小的 ♣ 牌)的玩家获得**首叫权和首出权**」。这与斗地主相反 ——
那边地主先出。它是 `IFirstSeatRules` 存在理由的另一半:内核的首手座位是**首叫者**,
而叫分结束之后出手权**回到同一个座位**。

**两条结束路径都 MUST 显式指名那个座位。** 三家各叫一次时自然轮转恰好也落在首叫者身上
(3 个座位、3 次叫分),而那是一个**巧合**;有人叫 3 时自然轮转会给错人。依赖那个巧合的实现
会在"有人叫 3"那条路径上把出手权交给下一家。

**这条断言 MUST 用一个首叫者不是 0 号的种子。** 否则「轮到首叫者」与「轮到 0 号」在同一个
断言下不可区分,那条测试会因为别的理由通过 —— 与 `fix-three-seat-membership` 里那条催促
断言需要"当前该走的人既不是 0 号也不是发起者"是同一个形状。

#### Scenario: 开局就轮到首叫者
- **WHEN** 三个座位坐满、开局
- **THEN** `Game.CurrentTurn == WakengDeal.FirstBidder().Seat`,而 MUST NOT 恒为 `0`

#### Scenario: 挖坑者不是首叫者时,出牌仍从首叫者开始
- **WHEN** 首叫者报 `bid:0`,下一家报 `bid:3`
- **THEN** 挖坑者是下一家,而 `Game.CurrentTurn` 回到**首叫者**

---

### Requirement: 一步棋的文本编码带标签,而畸形的牌是一次领域拒绝

`WakengMove` SHALL 把一步棋编码进 `Move.Text`,三种形式:`bid:0`…`bid:3`、`pass`、
`play:<cards>`。`bid:` 之后 MUST 恰好一位数字 —— `Move.Text` 是持久化格式,同一步棋只该有
一种写法。

**标签不是装饰。** 牌的字母表是 `A-Za-z@#`,所以一个由字母组成的英文词就是一手合法的牌
(`cab` = 三张)。标签让"这是动作还是牌"无歧义。

**畸形的牌 MUST 抛 `InvalidMoveException` 而 MUST NOT 让 `FormatException` 冒出去。**
`Card.DecodeMany` 对不认识的字符和重复的牌都抛 `FormatException`,而那不是 `DomainException`
—— 于是 `play:!!!` 会变成 **500**,客户端看到"服务器出错了",而实际上是它自己发错了。
这是 `add-doudizhu` 修过的一条真缺陷。

这条映射 SHALL 由 `Gewu.Domain/Games/Cards/CardPlay.cs` 一处提供,两个牌类棋种共用,并
SHALL 有一条**两个游戏各走一遍**的断言。**一个需要被记得的 `catch` 会在第三个解析器那里
被忘掉**,而这正是它值得共享的那一小块 —— 它的重复会重造一个量过的缺陷。

它 MUST 留在 move 层而 MUST NOT 下沉到 `Card.DecodeMany`:`WakengDeal.Decode` /
`DoudizhuDeal.Decode` 也调它,而它们**要的正是 `FormatException`** —— 一份坏掉的发牌是
损坏的记录,不是一步非法的棋。两个调用方要两种异常,所以映射只能在上面这一层。

#### Scenario: 编解码往返
- **WHEN** 把每一种一步棋编码再解回来
- **THEN** 得到同一步棋;`bid:+2` / `bid: 2` / `bid:22` MUST 被拒

#### Scenario: 畸形的牌不是服务器错误
- **WHEN** 提交 `play:!!!` 或 `play:AA`(同一张牌两次)
- **THEN** MUST 抛 `InvalidMoveException`,而 MUST NOT 是 `FormatException`

---

### Requirement: 规则从 `(Setup, History)` 重建局面,自身无状态

`WakengTable.Reconstruct(MatchState)` SHALL 从发牌 + 走子历史重建全局:阶段、首叫者
(座位与那张 ♣)、挖坑者、叫分、已叫次数、三家手牌、底牌、桌面上那一手、赢家。

`WakengRules` MUST 无状态 —— 同一个实例被并发的多个房间共享,这是 `IGameRules` 的硬要求。
重建时 MUST NOT 再校验历史里的每一步(它们当初就是这么被接受的),与
`XiangqiRules.Replay` / `DoudizhuTable` 同一个约定。

`state.Setup` 为 `null` 时 MUST 大声坏掉,而 MUST NOT 发一手空牌 —— 那是一条损坏的记录。

#### Scenario: 没有发牌的一局大声坏掉
- **WHEN** 用 `Setup == null` 的 `MatchState` 调 `Apply`
- **THEN** MUST 抛,而 MUST NOT 返回一个空手牌的局面

#### Scenario: 重建是纯函数
- **WHEN** 对同一个 `MatchState` 重建两次
- **THEN** 两次的手牌、桌面、阶段全部相同

---

### Requirement: 出牌、过牌与桌面清空

`WakengRules.Apply` SHALL 在出牌阶段要求:出的牌 MUST 全在这个座位手上、MUST 被
`WakengCombo.TryRecognise` 认出、跟牌时 MUST `Beats` 桌上那一手(**同型、同张数、更大**)。

桌上无牌时 MUST NOT 过牌 —— 首出必须出。连续两家过牌之后桌面 SHALL 清空,而出手权回到
**打出那一手的人**。

打完最后一张牌 SHALL 判胜,赢家是**这个座位**。挖坑者赢还是联手方赢,由客户端从叫分历史里
读出来:`WinnerUserId` 只装得下一个人,而两名联手方一起赢装不进去。

#### Scenario: 首出不许过牌
- **WHEN** 桌面为空时提交 `pass`
- **THEN** MUST 抛 `InvalidMoveException`

#### Scenario: 两家过牌之后桌面清空
- **WHEN** 某座位出牌,另两家依次过牌
- **THEN** 出手权回到出牌那一家,且此时可以自由首出任何合法牌型

#### Scenario: 不持有的牌被拒
- **WHEN** 提交一张不在自己手上的牌
- **THEN** MUST 抛 `InvalidMoveException`,而对局状态 MUST NOT 改变

---

### Requirement: 超时兜底必须推进对局,而它的终止论证与斗地主不同

`WakengRules.MoveOnTimeout` SHALL 在叫分阶段返回 `bid:0`,在出牌阶段"能过就过、首出则出
手上最小的一张单牌"。单牌永远是合法牌型,所以首出时它总是可行的。

**它 MUST 推进对局,而挖坑的终止论证与斗地主不同。** 斗地主三家都被托管的结果是流局,
三步就终局;挖坑三家都不挖会**进入出牌阶段并继续**,所以推进靠的是出牌阶段每一次兜底都让
一张牌离开某只手。

这条 SHALL 有一条**可执行**的断言:一个带上限的循环,只调超时兜底,直到对局结束 ——
而不是一段论证。上限是为了让"不推进"表现成"跑不完",而不是表现成挂住。

#### Scenario: 叫分阶段的三次超时不判负、也不流局
- **WHEN** 三家依次超时
- **THEN** 三步都是 `bid:0`,阶段进入出牌,挖坑者是首叫者、叫分 1;对局 MUST 仍在进行

#### Scenario: 只靠超时兜底也会走到终局
- **WHEN** 从开局起反复调 `TimeOutCurrentTurn`,上限若干步
- **THEN** 房间 MUST 变成 `Finished`,且 MUST 在上限之内

---

### Requirement: 挖坑不计分,而理由是结构性的

`WakengRules.IsRated` SHALL 为 `false`,而理由与斗地主逐字相同:ELO 是**两人**模型,而挖坑按
**分**结算(`WakengScoring.Settle` 给出三个座位各得多少)。一个按分的阶梯是**另一条榜** ——
与俄罗斯方块的分数榜和 ELO 榜分开是同一件事。

它也让 `IsRated ⇒ SeatCount == 2` 保持成立,不需要为挖坑开例外。**一个需要开例外的不变量
已经不是不变量了。**

`SupportsHumanVsHuman` SHALL 为 `true` —— 平台为它开人人对战入口,而那是
`enforce-human-vs-human` 定下的:这个字段由行为定义,建房端点收了就得声明。

**挖坑没有 AI**,而这 SHALL NOT 需要任何新代码:不在 `BuiltInGameAis.All` 里,
`enforce-ai-availability` 就会让 `POST /api/rooms/ai` 返回 400。`GET /api/games` 的
`supportsAi` 从同一个注册表投影,所以客户端画的按钮与服务端收的请求不可能不一致。

#### Scenario: 描述符里的四条事实
- **WHEN** 请求 `GET /api/games`
- **THEN** `wakeng` 的 `supportsHumanVsHuman == true`、`isRated == false`、
  `supportsAi == false`、`rows` 与 `cols` 都是 `null`

#### Scenario: 建房两条路径一开一关
- **WHEN** `POST /api/rooms { gameKey: "wakeng" }` 与 `POST /api/rooms/ai { gameKey: "wakeng" }`
- **THEN** 前者 `201`,后者 `400` —— **两半都 MUST 被量**,因为
  `enforce-human-vs-human` 与 `enforce-ai-availability` 都是从一半推另一半栽的

---

### Requirement: 接内核不改内核,而这条标准有两半

本变更 SHALL NOT 改动 `Gewu.Domain/Rooms/` 下的任何文件,而这条 SHALL 由源码级断言强制。

标准 SHALL 分成诚实的两半,与 `add-doudizhu` 立下的一样:

1. **聚合(`Gewu.Domain/Rooms/`)一个字都不许提到这个棋种。**
2. **抽象层(`Games/Abstractions/`)只许出现一行** —— `GameKeys` 里那个常量。
   每一个棋种自 `add-xiangqi` 起都往那里加了一行,一条"抽象层完全不提"的标准与仓库的
   实际做法矛盾。

两条断言都 SHALL 先检查**文件集非空**:路径写错的话它们会空转通过。两条都 SHALL 剥掉注释行
—— 历史说明本来就要提到这个棋种的名字。

#### Scenario: 聚合不认识挖坑
- **WHEN** 扫 `Gewu.Domain/Rooms/*.cs` 的非注释行
- **THEN** 没有一行提到 `Wakeng` / `wakeng`;且文件集非空

#### Scenario: 抽象层只把它当一个键
- **WHEN** 扫 `Gewu.Domain/Games/Abstractions/*.cs` 的非注释行
- **THEN** 恰好一行,且是 `public const string Wakeng = "wakeng";`

#### Scenario: 一整局跑过真聚合
- **WHEN** 用真 `Room` 从开局打到有人出完牌
- **THEN** 每一步都以 `Text` 落库、四个坐标字段全 `null`;`Game.Setup` 等于
  `CreateSetup(seed)`;终局的 `WinnerUserId` 是出完牌那个座位上的人

---

### Requirement: 每个座位看得到自己的牌,看不到别人的;首叫牌公开,基数不进视图

`WakengRules.ViewFor` SHALL 只给这个座位:自己的手牌、三家各剩几张、桌面上那一手、
首叫者与他亮的那张 ♣、挖坑者与叫分、以及**定下挖坑者之后**的底牌。围观者与还没入座的人
(`seat == null`)MUST 拿到空手牌 —— 不是某一家的牌,更不是三家的牌。越界的座位号同理。

**首叫者亮的那张 ♣ 是公开的。** 按规则它本来就是明示的(它决定了谁首叫首出),而服务端算得出
——**客户端不该自己猜**。它是一处判断,记在这里。

**基数 MUST NOT 进视图。** 它今天恒等于 `WakengScoring.DefaultBase == 1`,而那不是这一局的
*状态*,是一个还不存在的房间设置。发一个只有一个取值的字段,等于请客户端画「×1」;
将来它成为设置时,它属于**房间**而不属于按座位的视图 —— 三个座位看到的是同一个数。

**核心断言是"没有任何一个座位看得到别人的任何一张牌",而不是"我看得到我自己的"**:
后者在一个把三家手牌都塞进去的实现上同样是绿的。它 SHALL **逐张比对**,并 SHALL 带一条
越界座位号的负控制 —— 一个坏的座位号 MUST NOT 变成**别人的牌**。

#### Scenario: 逐张比对,谁也看不到别人的牌
- **WHEN** 取三个座位各自的视图
- **THEN** 每份视图里的每一张牌都属于那个座位;另两家的每一张牌都不在里面

#### Scenario: 围观者与越界座位拿到空手牌
- **WHEN** 用 `seat == null` 与 `seat == 7` 各取一次视图
- **THEN** 两份的手牌都是空;而张数、阶段、首叫牌这些公开信息仍在

#### Scenario: 底牌在挖坑者定下之前不可见
- **WHEN** 叫分尚未结束
- **THEN** 每一份视图里的底牌都是 `null`;定下挖坑者之后,三份视图里都能看到同样的 4 张
