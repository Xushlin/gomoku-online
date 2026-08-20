# game-rules-registry 的规格变化

## MODIFIED Requirements

### Requirement: `IDealtGameRules` 承载"这个棋种开局要一份服务端侧设置"

`Gewu.Domain` SHALL 定义:

```
public interface IDealtGameRules : IGameRules
{
    string CreateSetup(int seed);
}
```

只有需要秘密初始状态的棋种实现它。五子棋、一字棋、中国象棋、成语接龙**一行不动** —— 它们的开局是常量,走子历史本来就广播,没有任何东西要藏。

**分出一个接口而不是给 `IGameRules` 加成员**,理由与 `IBoardGameRules` / `INInARowRules` 当初分出来时相同:留在基接口上,四个棋种就得各写一个骗人的实现,而**骗人的实现是下一个人删不掉的东西**。

`CreateSetup` MUST 是纯函数:同一个 `seed` MUST 产出同一个字符串。这是重放的前提,也是测试能钉住一局牌的前提。实现 MUST NOT 用 `System.Random` —— 它的算法在 .NET 版本之间变过,而这条要求跨版本成立。

`seed` 由**调用方**给,取自 Application 层的 `ISeedProvider`。Domain MUST NOT 自己取随机数。

返回的字符串对内核完全不透明,但**规则读得到它**(`MatchState.Setup`)—— 见 `IGameRules.Apply`。

#### Scenario: 恰好两个内置棋种实现它
- **WHEN** 遍历 `BuiltInGameRules.All(lexicon)`
- **THEN** 恰好两个实现 `IDealtGameRules`,它们的 `GameKey` 恰好是 `{"doudizhu", "wakeng"}`

  这一条走过两级:先是"没有一个实现它"(`add-match-setup` 钉的是"没有偷偷改动现有棋种"),
  再是"恰好一个"(斗地主)。它按自己的预告红了第二次,而**那个时刻要问的问题被真的问了**:
  这两个棋种的设置是同一种东西吗?是 —— 两者都是"一副洗好的牌",都由一个种子确定,都
  MUST NOT 出服务端。**这个 seam 因此第一次被一个不同的游戏验证过**,而不只是被第二个
  实现填满:挖坑的牌是 52 张无王、16/16/16 + 4,与斗地主的 54 张、17/17/17 + 3 没有一处
  共用的常量。

  「恰好」的牙没有拔掉:第三个的那天它还会红。

#### Scenario: 同一个种子给出同一份设置
- **WHEN** 对同一个实现两次调 `CreateSetup(20260819)`
- **THEN** 两个字符串相等

#### Scenario: 设置由 Application 造好再交给聚合
- **WHEN** 一个需要设置的棋种开局
- **THEN** `ISeedProvider.NextSeed()` 被调用一次,其结果传给 `CreateSetup`,而 `CreateSetup` 的结果传给 `Room.JoinAsPlayer` —— **`Room` 与 `Game` 都不曾见过那个种子**

#### Scenario: 不需要设置的棋种不触发随机源
- **WHEN** 一个不实现 `IDealtGameRules` 的棋种开局
- **THEN** `ISeedProvider.NextSeed()` MUST NOT 被调用

### Requirement: `ITimeoutFallbackRules` 让超时变成"替他走一步"而不是"判他负"

`Gewu.Domain` SHALL 定义:

```
public interface ITimeoutFallbackRules : IGameRules
{
    MoveIntent MoveOnTimeout(MatchState state, int seat);
}
```

只有"超时不该判负"的棋种实现它。五子棋、一字棋、中国象棋、成语接龙**一行不动** —— 两个座位下"判他负、对手胜"是清楚且唯一的答案。

分出一个接口而不是给 `IGameRules` 加成员,理由与 `IBoardGameRules` / `IDealtGameRules` 相同:**骗人的实现是下一个人删不掉的东西**。

它收 `MatchState` 而 MUST NOT 只收历史。兜底动作可能需要**服务端侧的对局设置**:斗地主首出时要出"手上最小的一张单牌",而手牌在发牌里,不在历史里。

`MoveOnTimeout` MUST 是纯函数,MUST NOT 有副作用,并 MUST 返回一个该座位在该局面下**合法**的一步。它的返回值 MUST 与真人走的一步走同一条路 —— 即由 `IGameRules.Apply` 校验并判定结果。

**实现 MUST 保证推进对局。** 一个可以合法地无限重复的兜底动作(牌类游戏里"永远过牌")会把超时 worker 变成一个永不结束的自动对局。斗地主的形式是"能过就过,**不能过时出最小的一手**",而牌只会变少。

这条要求**不是防自旋的护栏**:每一次兜底都要等满一个超时周期,所以最坏情况是每个周期一步 —— 慢、可见、不会自旋。它是**对局质量**的要求,所以本 spec MUST NOT 规定一个"连续兜底次数上限"。

> **本要求的正文是手工合并的,而那是一条比它本身更值得记的账。**
> 本变更的这一段是在 `pass-state-to-fallback` 之前写的,所以它带的是**旧签名**
> (`MoveOnTimeout(IReadOnlyList<PlayedMove>, int)`);而那个变更**先合并**,把签名改成了
> `MatchState`。两个变更改同一条要求,而 MODIFIED 是整体替换 —— 于是"按合并顺序归档"
> 会让**后合并的那个**用旧正文盖掉新正文。归档前逐条比对发现了它,合并结果是:签名与
> `MatchState` 那两段取新的,"恰好一个实现"与斗地主的兜底形式取本变更的。
> **按合并顺序归档是必要的,不是充分的。**

#### Scenario: 恰好两个内置棋种实现它
- **WHEN** 遍历 `BuiltInGameRules.All(lexicon)`
- **THEN** 恰好两个实现 `ITimeoutFallbackRules`,它们的 `GameKey` 恰好是 `{"doudizhu", "wakeng"}`

  它按预告红了第二次,而该问的问题是"这两个棋种的超时真是同一种东西吗"。是,而**理由比
  '都是牌类'窄**:两者的座位数都是 3,所以"判他负、对手胜"里的"对手"都不唯一;而两者的兜底
  动作都能推进,因为**牌只会变少**。这两条与花色、大小、牌型全都无关 —— 一个三座位的非牌类
  棋种会落进同一条。

  一处差别写下来,因为它是这两个实现唯一不同的地方:斗地主三家都不叫是**流局**,兜底三次就
  终局;挖坑三家都不挖是**第一家兜底 1 倍**,叫分阶段结束后对局继续,所以它的"推进"要靠出牌
  阶段每次让一张牌离开某只手。**同一条要求,两条不同的终止论证。**

#### Scenario: 兜底看得到对局设置
- **WHEN** 一个实现设置的棋种超时,规则的 `MoveOnTimeout` 被调用
- **THEN** `state.Setup` 恰好是 `Game.Setup`,一字不改

#### Scenario: 兜底动作要经过合法性校验
- **WHEN** 一个实现返回了该局面下非法的一步
- **THEN** `Apply` MUST 抛 `InvalidMoveException`,而对局状态 MUST NOT 改变 —— "系统替他走的"不是绕过校验的理由

### Requirement: `IPerSeatViewRules` 承载"同一局,不同座位看到的不一样"

`Gewu.Domain` SHALL 定义:

```
public interface IPerSeatViewRules : IGameRules
{
    string ViewFor(MatchState state, int? seat);
}
```

只有有隐藏信息的棋种实现它。五子棋、一字棋、中国象棋、成语接龙**一行不动** —— 它们的全部状态就是走子历史,而走子历史本来就广播给所有人。

**分出一个接口而不是给 `IGameRules` 加成员**,理由与 `IDealtGameRules` / `IBoardGameRules` 相同:留在基接口上,四个棋种就得各写一个骗人的实现,而**骗人的实现是下一个人删不掉的东西**。

`seat` 为 `null` 表示"不占座位的人"(围观者,或进了房间还没入座的)。实现 MUST 只给这类人**公开信息**。

`ViewFor` MUST 是纯函数:同一个 `state` 与同一个 `seat` 给出同一个字符串。这样"某个座位看得到什么"是可断言的,而不是取决于调用时机。

**返回值对内核完全不透明。** 它原样进 `GameSnapshotDto.SeatView`,由客户端按棋种解析。内核 MUST NOT 解析它 —— 与闯关那条线的 `LayoutJson` / `SolutionJson` 同一个做法:内核不该知道什么是牌,而每个棋种要藏的东西天生不一样。

**实现 MUST NOT 泄漏别人的隐藏状态**,而这条 MUST 有一条**逐项比对**的断言,MUST NOT 只断言"我看得到我自己的":后者在一个把三家手牌都塞进去的实现上同样是绿的。

#### Scenario: 恰好两个内置棋种实现它
- **WHEN** 遍历 `BuiltInGameRules.All(lexicon)`
- **THEN** 恰好两个实现 `IPerSeatViewRules`,它们的 `GameKey` 恰好是 `{"doudizhu", "wakeng"}`

  **这条 Scenario 在被改成"两个"之前从来没有被实现过。** `add-doudizhu-visibility` 写下了
  "恰好一个实现 `IPerSeatViewRules`,且它的 `GameKey == \"doudizhu\"`",而
  `backend/tests/` 下**一次都没有出现过 `IPerSeatViewRules` 这个词** —— 用一条阳性对照
  (同样的搜法必须搜得到 `IDealtGameRules`)量过,不是读代码推出来的。它的两个邻居
  (`IDealtGameRules` / `ITimeoutFallbackRules`)各有一条真断言,所以这一条读起来像也有。

  这是本仓库同一个缺陷的第四次:`web-board-skins` 抄了 11 个变量名的 requirement、
  `web-shell` 数 sound pack 的 Scenario、`web-idiom-chain` 的 375 px 断言,都是"写下来了、
  没有实现"。**一条没有实现的 Scenario 与一条错的 Scenario 在归档时长得一模一样**,而
  `openspec validate --strict` 两者都放行 —— 它验的是形状,从不验真假。

  它现在有断言了,并且是变异验过的。

#### Scenario: 没有隐藏信息的棋种不带私有切片
- **WHEN** 为一个不实现本接口的棋种投影房间快照
- **THEN** `GameSnapshotDto.SeatView` MUST 是 `null` —— 不是空串、不是空对象。空串会让客户端以为"有私有状态,只是空的"

#### Scenario: 尚未开局时没有私有切片
- **WHEN** 一个实现本接口的棋种,其房间还在 `Waiting`
- **THEN** 投影 MUST NOT 抛异常,`SeatView` MUST 是 `null` —— 大厅里每个等待中的房间都会走到这条路,而一个抛异常的投影会让房间列表整页挂掉

#### Scenario: 同一个座位问两次得到同一个答案
- **WHEN** 对同一个 `state` 与同一个 `seat` 调两次
- **THEN** 两个字符串相等;而两个**不同**座位的字符串 MUST 不相等(否则裁剪根本没发生)
