# game-rules-registry Specification Delta

## MODIFIED Requirements

### Requirement: 内置棋种清单是它所需依赖的函数

`BuiltInGameRules.All(IIdiomLexicon idioms)` SHALL 返回**全部**内置棋种的规则实例,而 DI 与
"遍历注册表"的不变量测试 MUST 都从它取。

它是**函数**而不是静态列表,因为有的棋种需要依赖(成语接龙要一本词典)。诱人的替代方案是
"这个棋种在 DI 里单独注册",而那正是本仓库修过两次的缺陷:**一份手写的清单,被一条遍历测试
当成注册表**。

清单 SHALL 包含 `doudizhu`。`DoudizhuRules` 不需要外部依赖(发牌与牌型都是纯函数),所以它进
清单不需要新参数 —— 但它 MUST 进这**同一份**清单,MUST NOT 只在 DI 里注册。

#### Scenario: 清单与生产 DI 一致
- **WHEN** 比较 `BuiltInGameRules.All` 的键集合与 DI 注册的键集合
- **THEN** 两者相等

#### Scenario: 斗地主在清单里
- **WHEN** 遍历 `BuiltInGameRules.All(lexicon)`
- **THEN** 其中有一个 `GameKey == "doudizhu"`,且它 `SeatCount == 3`

#### Scenario: 遍历注册表的不变量自动覆盖新棋种
- **WHEN** `IsRated ⇒ SupportsHumanVsHuman`、`IsRated ⇒ SeatCount == 2`、以及建房能力那几条遍历测试运行
- **THEN** 它们**不需要改一个断言**就覆盖到斗地主

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

#### Scenario: 恰好一个内置棋种实现它
- **WHEN** 遍历 `BuiltInGameRules.All(lexicon)`
- **THEN** 恰好一个实现 `IDealtGameRules`,且它的 `GameKey == "doudizhu"`

  这一条此前是"没有一个实现它" —— 那时它钉的是"`add-match-setup` 没有偷偷改动任何现有棋种"。
  现在它钉的是斗地主是**唯一**需要秘密开局的棋种:再多一个的那天,这条会红,而那正是该问
  "这两个棋种的设置真是同一种东西吗"的时刻。

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

#### Scenario: 恰好一个内置棋种实现它
- **WHEN** 遍历 `BuiltInGameRules.All(lexicon)`
- **THEN** 恰好一个实现 `ITimeoutFallbackRules`,且它的 `GameKey == "doudizhu"`

  这一条此前是"没有一个实现它" —— `generalize-turn-flow` 加接缝时钉的是"没有偷偷改动现有棋种"。
  斗地主落地那天它按预告变红,改成"恰好一个":第二个要兜底的棋种出现时它会再红一次,而那正是
  该问"这两个棋种的超时真是同一种东西吗"的时刻。

#### Scenario: 兜底看得到对局设置
- **WHEN** 一个实现设置的棋种超时,规则的 `MoveOnTimeout` 被调用
- **THEN** `state.Setup` 恰好是 `Game.Setup`,一字不改

#### Scenario: 兜底动作要经过合法性校验
- **WHEN** 一个实现返回了该局面下非法的一步
- **THEN** `Apply` MUST 抛 `InvalidMoveException`,而对局状态 MUST NOT 改变 —— "系统替他走的"不是绕过校验的理由
