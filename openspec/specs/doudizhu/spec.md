# doudizhu Specification

## Purpose
TBD - created by archiving change add-doudizhu-cards. Update Purpose after archive.
## Requirements
### Requirement: 牌与它的一字符编码

`Gewu.Domain/Games/Doudizhu/Card.cs` SHALL 定义一副 54 张牌,并给每张牌一个**一字符编码**。

点数的**数值就是大小顺序**:`3 < 4 < … < K < A < 2 < 小王 < 大王`。这是斗地主序,不是扑克序 —— 2 比 A 大,而 A 不能当 1 用。花色 MUST NOT 参与任何比较,它只影响显示与编码。

编码 SHALL 是持久化格式的一部分,**MUST NOT 改动**:改一个字符,所有历史对局的重放都会读出别的牌。字母表 MUST 避开引号、逗号、反斜杠、斜杠与换行 —— 一个持久化格式不该需要读它的人先想清楚转义了几层。

一手牌的编码 MUST 与输入顺序无关(按牌本身排序),否则"这两手是不是同一手"要靠调用方排序。同一张牌在一手里出现两次 MUST 抛 —— 一副牌里它只有一张,所以那不是"非法的一手",是编码本身坏了。

#### Scenario: 每张牌都能往返
- **WHEN** 对 54 张牌逐张编码再解码
- **THEN** 得到同一张牌;54 个字符互不相同

#### Scenario: 一手牌装得进现有的文本载荷
- **WHEN** 编码 20 张牌(一手最多的张数)
- **THEN** 长度为 20,小于 `Move.Text` 的 64 字符上限

  **这就是斗地主不需要第四种载荷的全部理由。** `generalize-match-payload` 留的触发条件「真出现不规则走子时再加列」在这里不成立:一手牌是常规的文本内容,与成语接龙的一个成语同类。

#### Scenario: 编码被钉到字节
- **WHEN** 编码 3♣、3♠、4♣、2♠、小王、大王
- **THEN** 分别得到 `A`、`D`、`E`、`z`、`@`、`#`

#### Scenario: 重复的牌被拒
- **WHEN** 解码 `"AA"`
- **THEN** 抛 `FormatException`

---

### Requirement: 牌型识别与压牌

`CardCombo.Recognise` SHALL 认出 14 种牌型,认不出来返回 `null`。`CardCombo.Beats` SHALL 判定压牌。

比大小的依据:三带看**三张**、四带二看**四张**、顺子类看最大的那一组。顺子类 MUST 只与**同长度**的比。

压牌通则是同牌型、同张数、依据更大,只有两条例外:炸弹压任何非炸弹(不论张数),王炸压一切。**四带二不是炸弹** —— 它压不了别的牌型,也压不过任何炸弹,只能压更小的四带二。

顺子类的连续段范围 MUST 是 `3..A`:2 与两张王进不了顺子。单顺 ≥5 张,双顺 ≥3 组,飞机 ≥2 组。

两张王 MUST 认成王炸而不是对子。

**任何带牌 / 翅膀 MUST NOT 取自一个四张同点数的组合。** 这条规则的执行点是"含恰好一个四张的手牌一定先走到四带二分支并在那里被拒" —— 该分支末尾的早返是**承重的**,MUST 有测试钉住它。

> 曾经另有一个写在飞机分支里的守卫来管这件事。变异测试证明它是死代码:改坏它,该拒的手牌照样被拒 —— 拒它的是别处。**一个看起来承重的守卫,和一个真的承重的守卫,只有在被改坏的时候才分得出来。**

#### Scenario: 三带一按三张比,不按带的那张
- **WHEN** 比较 `555 + 3` 与 `444 + A`
- **THEN** 前者压得过后者

#### Scenario: 顺子只与同长度的比
- **WHEN** 比较 6 张的顺子与 5 张的顺子
- **THEN** 两个方向都压不过

#### Scenario: 2 与王进不了顺子
- **WHEN** 识别 `10 J Q K A 2`,或含王的五张连牌
- **THEN** 都返回 `null`

#### Scenario: 四带二不是炸弹
- **WHEN** 用 `6666 + 3 + 4` 去压一个 `333`
- **THEN** 压不过;而 `3333` 压得过 `6666 + 3 + 4`

#### Scenario: 翅膀不能拆炸弹
- **WHEN** 识别 4 组连续三张 + 一个炸弹当四张单翅膀(16 张)
- **THEN** 返回 `null`

#### Scenario: 没有一手牌压得过自己
- **WHEN** 任一牌型与自己比较
- **THEN** 返回 false

---

### Requirement: 发牌可复盘,且结果不出服务端

`DoudizhuDeal.FromSeed(seed)` SHALL 发出 17/17/17 + 3 张底牌,用完整的 54 张牌各一次。

**同一个种子 MUST 永远发出同一副牌** —— 重放一局靠的就是这一点。洗牌 MUST NOT 使用 `System.Random`:它的算法在 .NET 版本之间变过,而升级一次运行时就让所有历史对局重放出别的牌,比没有重放更糟。MUST 使用写死的 xorshift32,与 `TetrisPieceSequence` 同一个实现、同一个理由。

零状态 MUST 被替换成一个非零常数。**后果不是"没洗"**:状态恒为 0 时每次的交换目标都是 0 号位,牌确实动了、54 张也还各一次;真正的后果是**熵全丢** —— 任何落到零状态的种子发出同一副牌。断言 MUST 钉这条精确的性质,而不是"发出来的不等于牌堆原序"(后者在守卫被改坏之后照样绿)。

`Encode()` 的输出就是三家的底牌,它 **MUST NOT 发给客户端**,也 MUST NOT 从对局 id 之类的公开值派生 —— 那等于把所有人的手牌印在客户端能读到的地方。这与成语纵横「答案不出服务端」是同一条:*客户端算不出来的东西,客户端就骗不了*。

#### Scenario: 同种子同牌
- **WHEN** 用同一个种子发两次
- **THEN** 两次的编码逐字节相同

#### Scenario: 一副牌各一次
- **WHEN** 发一次牌
- **THEN** 三手 + 底牌共 54 张,互不相同

#### Scenario: 零种子被替换
- **WHEN** 用种子 `0` 与直接给出替代常数的种子各发一次
- **THEN** 两副牌相同;且都不等于种子 `1` 发出的牌

#### Scenario: 解码拒绝张数不对的一手
- **WHEN** 解码一段只有 16 张牌的手牌
- **THEN** 抛 `FormatException`

---

### Requirement: 计分 —— 底分乘倍数,三人之和恒为零

`DoudizhuScoring.Settle` SHALL 按 `分值 = 底分 × 倍数` 结算:地主赢拿 `+2×分值`、两名农民各 `−分值`,反之相反。

底分 MUST 是 1–3(叫分制的最高分),越界 MUST 抛。倍数从 1 起**逐项翻倍**:每个炸弹 ×2、王炸 ×2、春天 ×2、反春天 ×2。

**王炸与普通炸弹同一个倍率** —— 这是本仓库定下的家规,理由是少一个特例,不是通行规则。

春天与反春天 MUST 互斥:春天要求地主出完牌、反春天要求农民赢,两者不可能同时成立。同时为真时 MUST 抛,而 MUST NOT 在计分里挑一个 —— 让构造出这种输入的地方当场坏掉。

**三人得分之和 MUST 恒为 0。** 这是这套算法唯一能自我检查的性质,所以它 MUST 是一条断言。

#### Scenario: 地主赢两份,农民各输一份
- **WHEN** 底分 2、无倍数、地主赢
- **THEN** 地主 `+4`,每名农民 `−2`

#### Scenario: 三人之和为零
- **WHEN** 任意底分与胜负组合
- **THEN** `地主 + 2 × 每名农民 == 0`

#### Scenario: 倍数逐项相乘
- **WHEN** 底分 3、两个炸弹、一个王炸、春天
- **THEN** 倍数 16、地主 `+96`、每名农民 `−48`

#### Scenario: 春天与反春天不能同时成立
- **WHEN** 两者都为真
- **THEN** 抛 `ArgumentException`

### Requirement: 叫分制决定地主与底分

系统 SHALL 以**叫分**决定地主:座位 0 先叫,然后 1、2,每人恰好一次机会。

- 一次叫分 MUST 是 `1` / `2` / `3` 分之一,或**不叫**
- 叫的分 MUST 严格高于当前最高分;不高则拒绝
- 有人叫 `3` 分时叫分**立即结束** —— 没有人压得过,再问一遍是浪费一次交互
- 三人各叫过一次之后,最高分者是地主;底牌归他,**他先出牌**

底分就是那个最高分。`DoudizhuScoring.DoudizhuOutcome.BaseScore` 早就写着「叫地主时的最高分,1 / 2 / 3」,所以叫分制不是本变更的新决定,是把 `add-doudizhu-cards` 已有的假设兑现。

#### 三家都不叫是流局(和局),MUST NOT 重新发牌

重发需要在同一个 `Game` 上换第二份 `Setup`,而那要改内核。「发牌在开局那一刻定下、之后不变」这条性质 MUST 保住:它是重放与"服务端侧设置"这个概念的地基。

备选方案是"强制 0 号当地主",MUST NOT 采用 —— 那会因为一条他没选的规则罚他。流局把代价放在没人吃亏的地方。

#### Scenario: 依次叫分,最高者为地主
- **WHEN** 座位 0 叫 1、座位 1 不叫、座位 2 叫 2
- **THEN** 叫分结束;地主是座位 2;底分为 2;下一手轮到座位 2

#### Scenario: 叫 3 分立即结束叫分
- **WHEN** 座位 0 叫 3
- **THEN** 叫分立即结束;地主是座位 0;底分为 3;下一手仍是座位 0(他先出牌)

#### Scenario: 叫的分不高于当前最高分被拒
- **WHEN** 座位 0 叫 2,座位 1 也叫 2
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 三家都不叫则流局
- **WHEN** 三个座位依次都 `bid:0`
- **THEN** `Apply` 返回 `GameResult.Draw`,`WinnerSeat == null`

#### Scenario: 叫分阶段 MUST NOT 出牌
- **WHEN** 叫分还没结束,某个座位提交 `play:…`
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 出牌阶段 MUST NOT 叫分
- **WHEN** 叫分已结束,某个座位提交 `bid:…`
- **THEN** 抛 `InvalidMoveException`

### Requirement: 一步棋的文本编码带标签

一步斗地主 SHALL 编码进 `Move.Text`,三种形式:

| 文本 | 含义 |
| --- | --- |
| `bid:0` … `bid:3` | 叫分,`0` 是不叫 |
| `pass` | 出牌阶段过牌 |
| `play:<cards>` | 出牌,`<cards>` 是 `Card.Encode` 的输出 |

**标签 MUST 存在。** 牌的字母表是 `A-Za-z@#`,而 `p` / `a` / `s` 都是合法的牌字符 —— 一个裸的 `"pass"` **就是一手四张牌的合法编码**。标签在第一个 `:` 之前(或整串为 `pass`),使解析无歧义。

标签 MUST 是可读的英文而 MUST NOT 是单字符前缀:`Move.Text` 会被人在数据库里直接读,而 `"cABC"` 与 `"play:ABC"` 差 5 个字符、差一整个"这是什么"。

长度 MUST 装进 `Move.Text` 的 64 字符:`play:` 五个字符加最多 20 张牌(地主的全部手牌)= 25。

#### Scenario: 三种形式都能解析
- **WHEN** 解析 `bid:2` / `pass` / `play:ABC`
- **THEN** 分别得到"叫 2 分" / "过牌" / "出 A、B、C 三张"

#### Scenario: 认不出的文本被拒
- **WHEN** 解析 `bid:4`、`bid:-1`、`play:`、`playABC`、`fold`、空串
- **THEN** 每一个都抛 `InvalidMoveException`

#### Scenario: 裸的 pass 不会被当成四张牌
- **WHEN** 解析 `pass`
- **THEN** 得到"过牌",而 MUST NOT 得到"出 p、a、s、s 四张牌"

  这一条是那个标签存在的全部理由,所以它 MUST 有一条自己的测试。

#### Scenario: 位置类载荷被拒
- **WHEN** 对斗地主提交一个带坐标的 `MoveIntent`
- **THEN** 抛 `InvalidMoveException` —— 斗地主没有盘面

### Requirement: 规则从 `(Setup, History)` 重建全局,自身无状态

`DoudizhuRules` SHALL 在每次 `Apply` 时从 `MatchState` 重建:

- 当前阶段(叫分 / 出牌 / 已结束)
- 地主是谁、底分多少
- 三家各自**还剩什么牌**:初始手牌减去已打出的;地主再加三张底牌
- 桌上是什么牌型、谁打的、连续几家过牌了

规则实例 MUST 无状态 —— 同一个实例被并发的多个房间共享,这是 `IGameRules` 的硬要求。每步 O(n) 重放在一局 ≤ 100 手的量级上无关紧要,与棋盘类棋种每步重放盘面是同一条理由。

出牌 MUST 校验**这个座位确实持有这些牌**。这是 `Setup` 必须到得了规则的原因,也是 `pass-setup-to-rules` 存在的原因。

#### Scenario: 出手上没有的牌被拒
- **WHEN** 某座位提交一手它手上没有的牌
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 同一张牌不能出两次
- **WHEN** 某座位先出了一张牌,之后再提交同一张
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 地主的手牌是 17 + 3
- **WHEN** 叫分结束
- **THEN** 地主可出的牌共 20 张,两名农民各 17 张

#### Scenario: 无状态
- **WHEN** 同一个规则实例被两个不同的 `MatchState` 先后调用
- **THEN** 两次结果只取决于各自的 `state`

### Requirement: 出牌、过牌与桌面清空

出牌阶段 SHALL 按以下规则:

- 首出(桌面为空)MUST 出牌,MUST NOT 过牌
- 非首出时,出的牌 MUST 压得过桌上那一手(`CardCombo.Beats`),否则拒绝
- 过牌总是合法,除非自己是首出
- **连续两家过牌**之后桌面清空,轮到的那一家是新的首出
- 某个座位打完最后一张牌时,该座位获胜

轮转 MUST 是 `(seat + 1) % 3`,而叫分结束那一手 MUST 用 `MoveApplication.NextSeat` 把出手权交给地主。

#### Scenario: 首出不能过牌
- **WHEN** 桌面为空,轮到的座位提交 `pass`
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 压不过的牌被拒
- **WHEN** 桌上是一对 K,某座位出一对 5
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 两家过牌后桌面清空
- **WHEN** 座位 0 出牌,座位 1、2 依次过牌
- **THEN** 轮到座位 0,且他是首出 —— 可以出任意合法牌型,而 MUST NOT 过牌

#### Scenario: 出完最后一张牌即获胜
- **WHEN** 某座位打出它最后的牌
- **THEN** `Apply` 返回 `GameResult.Decided`,`WinnerSeat` 是该座位

#### Scenario: 赢家是座位,不是"哪一方"
- **WHEN** 一名农民打完了牌
- **THEN** `WinnerSeat` 是**那个农民的座位**,而不是"农民方"

  `WinnerUserId` 只能装一个人,而两名农民一起赢装不进去。今天这样够用:游戏不计分,客户端从叫分历史里知道谁是地主,自己能把"农民赢了"说出来。**真正需要"哪一方赢"的是结算**,而结算等那条按分的榜。

### Requirement: 超时兜底必须推进对局

`DoudizhuRules` SHALL 实现 `ITimeoutFallbackRules`:

- 叫分阶段 → `bid:0`(不叫)
- 出牌阶段 → 能过就 `pass`;自己是首出则出**手上最小的一张单牌**

两条都 MUST 严格推进:叫分最多三手就结束(三家都被托管则流局,而流局是终局);出牌时每次兜底至少让一张牌离开某只手。

单牌永远是合法牌型,所以"出最小的一张"在首出时总是可行的 —— 这条 MUST NOT 依赖手上有什么。

#### Scenario: 叫分阶段的兜底是不叫
- **WHEN** 叫分阶段某座位超时
- **THEN** 兜底动作是 `bid:0`

#### Scenario: 能过牌时兜底就过牌
- **WHEN** 出牌阶段桌上有牌,某座位超时
- **THEN** 兜底动作是 `pass`

#### Scenario: 首出时兜底出最小的单牌
- **WHEN** 出牌阶段桌面为空,某座位超时
- **THEN** 兜底动作是 `play:<那一手手牌里最小的一张>`

#### Scenario: 三家都被托管则流局
- **WHEN** 叫分阶段三家依次超时
- **THEN** 对局以 `GameResult.Draw` 结束

### Requirement: 斗地主不计分,而理由是结构性的

`DoudizhuRules.IsRated` MUST 为 `false`。

理由 MUST NOT 是"暂时"或"以后再说":ELO 是**两人**模型,而斗地主按分结算(`DoudizhuSettlement` 给出三个座位各得多少)。一个按分的阶梯是**另一条榜**,与俄罗斯方块的分数榜和 ELO 榜分开是同一件事。

这也让 `IsRated ⇒ SeatCount == 2` 那条不变量保持成立,不需要为斗地主开例外。**一个需要开例外的不变量已经不是不变量了。**

`SupportsHumanVsHuman` MUST 为 `true` —— `POST /api/rooms { gameKey: "doudizhu" }` 要能建房,而那条字段自 `enforce-human-vs-human` 起是按行为定义的。

**没有 AI。** 一个会算牌的机器人容易做,而机器人对局是计分的 —— 但斗地主不计分,所以那条"会排出刷机器人的榜"的顾虑在这里不成立。不做 AI 的理由更简单:三个座位的房间要两个机器人,而那是另一个变更的工作量。

#### Scenario: 遍历注册表时斗地主是不计分的
- **WHEN** 遍历 `BuiltInGameRules.All(lexicon)`
- **THEN** `doudizhu` 的 `IsRated == false` 且 `SeatCount == 3`

#### Scenario: 不变量仍然成立
- **WHEN** 遍历注册表检查 `IsRated ⇒ SeatCount == 2`
- **THEN** 没有反例 —— 斗地主不需要例外

#### Scenario: 可以建人人对战房
- **WHEN** `POST /api/rooms { gameKey: "doudizhu", ... }`
- **THEN** 201 —— `SupportsHumanVsHuman` 为 `true`

#### Scenario: 不能建人机房
- **WHEN** `POST /api/rooms/ai { gameKey: "doudizhu", ... }`
- **THEN** 400 —— 注册表里没有斗地主的 AI 工厂(`enforce-ai-availability`)

### Requirement: 接内核不改内核,而这条标准有两半

本变更 SHALL NOT 改动匹配聚合(`Gewu.Domain/Rooms/`),并 SHALL 只在 `GameKeys` 里往规则抽象层
(`Gewu.Domain/Games/Abstractions/`)加一行常量。

这是六个使能变更(`generalize-match-seats`、`add-room-seats`、`generalize-match-outcome`、`add-match-setup`、`generalize-turn-flow`、`pass-setup-to-rules`)的**验收标准**,与 `add-xiangqi` / `add-klotski` / `add-idiom-chain` 继承的是同一条。

**它 MUST 分成两半陈述,因为一半式的说法是错的:**

- **聚合**(`Gewu.Domain/Rooms/`)MUST NOT 提到任何一个具体棋种 —— 一个字都不行。
- **规则抽象层**(`Gewu.Domain/Games/Abstractions/`)MAY 且仅 MAY 在 `GameKeys` 里多一行常量。

> 本条的第一版写的是「两个目录下都不许提到这个棋种」,而它**与 `add-xiangqi` 以来的每一次都矛盾**:`GameKeys` 就住在 `IGameRules.cs` 里,五个棋种的键全在那儿。写成断言之后它当场红了 —— 那是它第一次被检验。**一条从来没被执行过的验收标准,可以和实际做法矛盾很多年。**

两半 MUST 各有一条源码级断言,且 MUST 剥掉注释行:注释里对历史的说明正是要留的东西(与 `SeatKernelTests` 那条「`Stone` 不出现在 `Rooms/` 下」同一个做法、同一个坑)。

MUST 有一局**用真 `Room` 打完**的测试:发牌 → 叫分 → 出牌 → 有人打完 → 对局结束。它证明的是接缝真的通,而不是各层单测各自通。

#### Scenario: 聚合不认识这个棋种
- **WHEN** 扫 `Gewu.Domain/Rooms/` 下每个 `.cs` 的非注释行
- **THEN** 没有一行提到 `Doudizhu` / `doudizhu`

#### Scenario: 抽象层只把它当成一个键
- **WHEN** 扫 `Gewu.Domain/Games/Abstractions/` 下每个 `.cs` 的非注释行
- **THEN** 恰好一行提到它,且那一行是 `public const string Doudizhu = "doudizhu";`

#### Scenario: 断言不会空转
- **WHEN** 上面两条断言运行
- **THEN** 它们先断言扫到的文件集合非空 —— 路径写错时 MUST 失败,而 MUST NOT 以"没有反例"通过

#### Scenario: 一整局走通真聚合
- **WHEN** 用真 `Room` 从开局打到某座位出完牌
- **THEN** `Room.Status == Finished`;`Game.WinnerUserId` 是那个座位上的玩家;`Game.EndReason == Decided`

#### Scenario: 每一手都落在 Moves 里
- **WHEN** 上述整局结束
- **THEN** `Game.Moves` 的条数等于叫分手数加出牌手数,每条的 `Text` 非空、四个坐标列全为 `null`

