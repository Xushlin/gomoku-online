# game-rules-registry 的规格变化

## ADDED Requirements

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

#### Scenario: 恰好一个内置棋种实现它
- **WHEN** 遍历 `BuiltInGameRules.All(lexicon)`
- **THEN** 恰好一个实现 `IPerSeatViewRules`,且它的 `GameKey == "doudizhu"`

#### Scenario: 没有隐藏信息的棋种不带私有切片
- **WHEN** 为一个不实现本接口的棋种投影房间快照
- **THEN** `GameSnapshotDto.SeatView` MUST 是 `null` —— 不是空串、不是空对象。空串会让客户端以为"有私有状态,只是空的"

#### Scenario: 尚未开局时没有私有切片
- **WHEN** 一个实现本接口的棋种,其房间还在 `Waiting`
- **THEN** 投影 MUST NOT 抛异常,`SeatView` MUST 是 `null` —— 大厅里每个等待中的房间都会走到这条路,而一个抛异常的投影会让房间列表整页挂掉

#### Scenario: 同一个座位问两次得到同一个答案
- **WHEN** 对同一个 `state` 与同一个 `seat` 调两次
- **THEN** 两个字符串相等;而两个**不同**座位的字符串 MUST 不相等(否则裁剪根本没发生)
