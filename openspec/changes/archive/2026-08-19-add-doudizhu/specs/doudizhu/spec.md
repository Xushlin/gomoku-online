# doudizhu Specification Delta

## ADDED Requirements

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
