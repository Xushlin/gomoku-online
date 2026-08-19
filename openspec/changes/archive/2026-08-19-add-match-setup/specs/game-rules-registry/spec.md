# game-rules-registry Specification Delta

## ADDED Requirements

### Requirement: `IDealtGameRules` 承载"这个棋种开局要一份服务端侧设置"

`Gewu.Domain` SHALL 定义:

```
public interface IDealtGameRules : IGameRules
{
    string CreateSetup(int seed);
}
```

只有需要秘密初始状态的棋种实现它。五子棋、一字棋、中国象棋、成语接龙**一行不动** —— 它们的开局是常量,走子历史本来就广播,没有任何东西要藏。

**分出一个接口而不是给 `IGameRules` 加成员**,理由与 `IBoardGameRules` / `INInARowRules` 当初分出来时相同:留在基接口上,四个棋种就得各写一个骗人的实现(`=> null` 之类),而**骗人的实现是下一个人删不掉的东西** —— 他无从知道有没有调用方。本 spec 已有的那条纪律仍然适用:**接口只承载对每个实现都成立的东西。**

`CreateSetup` MUST 是纯函数:同一个 `seed` MUST 产出同一个字符串。这是重放的前提,也是测试能钉住一局牌的前提。实现 MUST NOT 用 `System.Random` 之外的运行时随机源,更 MUST NOT 用 `System.Random` —— 它的算法在 .NET 版本之间变过,而这条要求跨版本成立(同 `TetrisPieceSequence` 与 `DoudizhuDeal` 上写下的理由)。

`seed` 由**调用方**给,取自 Application 层的 `ISeedProvider`。Domain MUST NOT 自己取随机数。

返回的字符串对内核完全不透明,见 `room-and-gameplay` 的 `Game.Setup`。

#### Scenario: 现有棋种都不实现它
- **WHEN** 遍历 `BuiltInGameRules.All(lexicon)`
- **THEN** 没有一个实现 `IDealtGameRules`

  这一条会在斗地主落地那天由那次变更改成"恰好一个实现它"。它现在的价值是钉住**本次变更没有偷偷改动任何现有棋种** —— 那是本变更的验收标准。

#### Scenario: 同一个种子给出同一份设置
- **WHEN** 对同一个实现两次调 `CreateSetup(20260819)`
- **THEN** 两个字符串相等

#### Scenario: 设置由 Application 造好再交给聚合
- **WHEN** 一个需要设置的棋种开局
- **THEN** `ISeedProvider.NextSeed()` 被调用一次,其结果传给 `CreateSetup`,而 `CreateSetup` 的结果传给 `Room.JoinAsPlayer` —— **`Room` 与 `Game` 都不曾见过那个种子**

#### Scenario: 不需要设置的棋种不触发随机源
- **WHEN** 一个不实现 `IDealtGameRules` 的棋种开局
- **THEN** `ISeedProvider.NextSeed()` MUST NOT 被调用

  一个每局都取一次随机数却没人用的调用,会让"这个棋种有随机性吗"这个问题在读代码时得不到确定答案。
