# wakeng Specification

## Purpose
TBD - created by archiving change add-wakeng-cards. Update Purpose after archive.
## Requirements
### Requirement: 挖坑的大小是它自己的,而连续性是另一件事

`Gewu.Domain/Games/Wakeng/WakengRank.cs` SHALL 定义挖坑的强弱:
`3 > 2 > A > K > Q > J > 10 > 9 > 8 > 7 > 6 > 5 > 4` —— **3 最大而不是最小**。

它 MUST NOT 直接拿 `(int)CardRank` 比大小:那个数值是**编码**顺序(它与斗地主的大小顺序
恰好一致),而挖坑要自己映一层。数值 MUST NOT 改 —— 它是编码下标的来源,也就是持久化格式。

**能组成连牌的点数是 4 到 K,A / 2 / 3 都不行。** 原文只排除了 3 和 2,却又说
「因此连到 K 的顺子是相同张数中最大的」—— 而 A 在挖坑的大小表里比 K 大,所以那个「因此」
只有在 A 也不能进连牌时才成立。**这是一处判断而不是推导**,所以它 SHALL 只有一份定义:
改一处就同时改掉四种连牌。

「强弱」与「连续位置」SHALL 是两个函数。它们在 4–K 上给出同一个数,而合成一个会让
「A 算第 11 位」这种错悄悄成立 —— 强弱覆盖 13 个点数,连续性只覆盖 10 个。

#### Scenario: 3 最大,4 最小
- **WHEN** 按 4、5、…、K、A、2、3 的顺序取强弱
- **THEN** 严格递增且两两不同;`Strength(3) > Strength(2) > Strength(A)`

#### Scenario: A / 2 / 3 不进连牌
- **WHEN** 问 K / A / 2 / 3 能不能进连牌
- **THEN** 只有 K 能;可连的点数恰好 10 个

---

### Requirement: 一手合法牌就是「k 组等大的牌,k > 1 时连续」

`WakengCombo.TryRecognise` SHALL 认出八种牌型:单、对、三头、四头、顺子、连对、飞机、火箭。

**挖坑没有带牌、也没有炸弹**,于是每一手合法牌都是同一句话 —— **k 组等大的牌,k > 1 时点数
连续**:k = 1 时按组的大小得到单 / 对 / 三头 / 四头;k ≥ 3 时按组的大小得到顺子 / 连对 /
飞机 / 火箭。**k = 2 不是任何牌型**(3344 不能出),而那是「连牌 3 组起」的直接后果,
不是一条特例。

因此下列 MUST NOT 被认成牌型:三带一、三带二、四带一、四带二(挖坑不许带牌)、
两组连牌、组大小不一致(如 333 44)、含 A / 2 / 3 的连牌、有断口的连牌、空手。

**四头不是炸弹。** 它只压得住更小的四头。

#### Scenario: 按组的大小认牌型
- **WHEN** 出 1 / 2 / 3 / 4 张同点数的牌
- **THEN** 分别是单 / 对 / 三头 / 四头;出 3 组以上等大的连续牌则是顺子 / 连对 / 飞机 / 火箭

#### Scenario: 带牌不是牌型
- **WHEN** 出 `444 5` 或 `4444 55`
- **THEN** MUST 认不出来 —— 挖坑的三头与四头都不能带牌

#### Scenario: 两组连牌不是牌型
- **WHEN** 出 `44 55`
- **THEN** MUST 认不出来

---

### Requirement: 跟牌必须同型、同张数、更大

`WakengCombo.Beats` SHALL 同时要求三件事:牌型相同、张数相同、更大。**三个条件缺一不可。**

- 少了「同型」,四头就能压顺子(挖坑没有炸弹);
- 少了「同张数」,五张顺子就能压三张顺子;
- 少了「更大」,同型同张就能互相压。

**「同型」这一条的断言 MUST 是能区分的**:四头与它想压的东西张数几乎从不相同(4 对 3、
4 对 2、4 对 1),所以那些断言在「四头变成炸弹」的实现下**照样是绿的** —— 它们因为别的理由
通过。唯一能区分的形状是**同张数、不同牌型、而且压的那一手更大**(例如 `KKKK` 对 `4567`)。

#### Scenario: 四头压不住同张数的顺子
- **WHEN** 用 `KKKK` 去压 `4567`
- **THEN** MUST 压不住 —— 同是四张,而四头不是顺子

#### Scenario: 长顺子不是更大的短顺子
- **WHEN** 用五张顺子去压三张顺子
- **THEN** MUST 压不住;反向也压不住

#### Scenario: 花色不决定任何事
- **WHEN** 用 ♠7 去压 ♣7
- **THEN** MUST 压不住

---

### Requirement: 发牌 52 张无王、16/16/16 + 4,可复盘且不出服务端

`WakengDeal.FromSeed(seed)` SHALL 发三家各 16 张 + 4 张底牌,用 `Card.SuitedDeck`(52 张,
**不含大小王**),洗法用共享的 `CardShuffle`。**同一个种子 MUST 发出同一副牌。**

`Encode()` 的输出 **MUST NOT 发给客户端** —— 它就是三家的底牌。与成语纵横「答案不出服务端」
同一条:*客户端算不出来的东西,客户端就骗不了*。

`Decode` MUST 拒绝:段数不对、张数不对、有重复的牌、以及**含王**。最后一条单列,因为一副带王
的牌能通过前三条,而它会让「3 最大」这条规则失去意义。

一个种子发出的整副牌 SHALL 被一条测试写死 —— 那是「洗牌一个字节都没变」的可执行形式。

#### Scenario: 一副牌用完且不含王
- **WHEN** 从任意种子发牌
- **THEN** 52 张各出现一次,一张王都没有

#### Scenario: 解码拒绝带王的发牌
- **WHEN** 把某一手里的一张换成大王,再解码
- **THEN** MUST 抛 `FormatException`

---

### Requirement: 首叫权是拿底牌前持有最小 ♣ 的座位,而它必须轮换

`WakengDeal.FirstBidder()` SHALL 返回**拿底牌前持有最小 ♣ 的座位**以及那张牌 —— 按挖坑的
大小从小到大扫梅花,第一张落在某家手里的就是它(原文:若没人有 ♣4,则拿 ♣5 者首叫,
依此类推)。

它 SHALL 一定找得到:十三张梅花、底牌只有四张,至少九张在手上。找不到只能是这份发牌本身
坏了,此时 MUST 抛而 MUST NOT 默默返回 0 号座位 —— 一个默默的默认会让「首叫权算错」表现成
「0 号总是先叫」,而那要打很多局才看得出来。

**它 MUST 轮换。** 一条走多个种子的断言要求三个座位都当过首叫 —— 若首叫永远是 0 号,
那就等于「把发牌旋转成最小 ♣ 总在 0 号」,而 `generalize-match-kickoff` 明确否掉了那个做法
(统计上等价,体验上不等价)。

扫描方向 SHALL 由一条断言钉住:♣4 只有 4/52 的概率进底牌,所以多数种子下首叫牌就是 ♣4 ——
从大往小扫也能通过「总找得到」,但过不了这一条。

#### Scenario: 比首叫牌更小的梅花都在底牌里
- **WHEN** 取任意种子的首叫牌
- **THEN** 它是梅花、在那家手里,而比它更小的每一张梅花都在底牌里

#### Scenario: 三个座位都当过首叫
- **WHEN** 走 200 个种子
- **THEN** 首叫座位覆盖 0 / 1 / 2

---

### Requirement: 计分是叫分 × 基数,挖坑者那一侧 ×2,三家之和恒为零

`WakengScoring.Settle` SHALL 按原文结算:挖坑者先出完则赢 `叫分 × 基数 × 2`、另两人各输
`叫分 × 基数`;联手两人任一先出完则各赢 `叫分 × 基数`、挖坑者输 `叫分 × 基数 × 2`。

**三家之和 MUST 恒为零**,而这条 MUST 在**两个方向**上都被断言 —— 只查一个方向的话,
「挖坑者赢时多给一份」这种错有一半的时候是看不见的。

叫分不在 1–3 之内 MUST 抛。**0 分是「不挖」,而不挖的人不结算** —— 一个 0 分的结算会把所有人
的分算成 0,看起来像「这局没人输赢」,而那与「这局根本没算」长得一样。

**三家都说不挖时,第一家挖,兜底 1 倍。** 原文没写这种情况,这是一处判断,所以它 SHALL 是一个
有名字的常量(`ForcedBid`)而不是写在分支里的 `1`。基数默认 1(`DefaultBase`),将来做成房间设置。

#### Scenario: 和恒为零
- **WHEN** 任意叫分 × 任意基数 × 挖坑者赢 / 输
- **THEN** 三项之和为 0

#### Scenario: 0 分不结算
- **WHEN** 用叫分 0 去结算
- **THEN** MUST 抛 `ArgumentOutOfRangeException`

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

### Requirement: 候选出法由服务端枚举,而「要不起」就是这个列表为空

`WakengFollows.For(hand, onTable)` SHALL 给出这手牌在当前局面下**全部**合法的出法,
按「先弱后强」排:

- `onTable == null`(自由首出):这手牌能组出的全部合法牌型;
- 否则:**同型、同张数、更大**的那些 —— 挖坑**没有炸弹**,所以不存在跨型压牌。

**它 SHALL 是「要不起」与「提示」两件事的唯一判据。** 写成两套逻辑会造出一个能自相矛盾的
组合:提示说「你可以出这手」,而自动过牌已经替你过了。**一个事实两个读者,不是两个事实。**

一条断言 SHALL 把两个出口钉在一起:在若干局面上逐个比对
`canFollow == (For(...).Count > 0)`。

「要不起」那条断言 MUST 用一个**真的要不起**的局面,并 MUST 带**正面对照**(同一手牌换一个
更小的桌面牌就出得起)—— 否则一个恒返回空列表的实现同样是绿的。

#### Scenario: 自由首出时列出全部牌型
- **WHEN** 桌上没有牌
- **THEN** 列表含这手牌能组出的每一种合法牌型,且每一项都能被 `TryRecognise` 认出

#### Scenario: 跟牌只给同型同张数更大的
- **WHEN** 桌上是一手三张顺子
- **THEN** 列表里只有三张顺子且更大的那些;四头**不在**列表里(挖坑没有炸弹)

#### Scenario: 真要不起时列表为空,而更小的桌面牌就不为空
- **WHEN** 手里同型的牌都比桌上那手小
- **THEN** 列表为空;而把桌上那手换成更小的一手,同一手牌的列表**非空**

---

### Requirement: `seatView` 带 `canFollow`,而候选列表走按需查询

`WakengSeatView` SHALL 多一个 `canFollow: bool` —— 「此刻轮到你时你出得起吗」。
它 MUST 只对这个座位可见(它由这个座位的手牌决定),所以它属于 `seatView`。

**它 MUST NOT 是一个列表。** 候选出法可能有几十项,而每次广播都带着它是
「一个没人渲染但所有人付钱的切片」。候选列表 SHALL 走**按需**的
`GET /api/rooms/{id}/hints`,而只有**在座玩家**拿得到自己的那一份。

**围观者与非玩家拿到的是一个空列表,而不是一次拒绝** —— 这一句是修正:上一版写的是
「MUST 被拒」,而实现从来是 `200` 加一份空列表(量过端点,不是读代码猜的)。理由在
`add-wakeng-play-hints` 的记录里:提示是**可有可无的便利**,而「这里没有可提示的东西」的
正确反应是按钮不出现,不是一条错误路径。**空列表与拒绝在「MUST NOT 返回任何一家的候选」
这一条下长得一样**,而那正是这处漂移能活下来的原因。

`canFollow` 在自由首出、以及不轮到自己时的取值 SHALL 有明确定义并被断言 ——
一个「有时是 false 只因为还没轮到你」的字段会让客户端在错的时候自动过牌。

#### Scenario: 围观者拿到空列表而不是别人的候选
- **WHEN** 围观者请求 `/hints`
- **THEN** 返回一个空列表,而 MUST NOT 返回任何一家的候选

#### Scenario: canFollow 与候选列表一致
- **WHEN** 在若干局面上同时取两者
- **THEN** `canFollow == (候选列表非空)`,逐个局面成立

