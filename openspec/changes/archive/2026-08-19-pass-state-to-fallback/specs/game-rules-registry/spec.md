# game-rules-registry Specification Delta

## MODIFIED Requirements

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

> 这一条此前写的是 `MoveOnTimeout(IReadOnlyList<PlayedMove> history, int seat)`。`generalize-turn-flow`
> 加它的时候 `MatchState` 还不存在;紧接着的 `pass-setup-to-rules` 为了同一个理由(规则读不到设置)
> 把 `Apply` 改成收 `MatchState`,**却没有回头看几十行之外这个刚加的接缝**。与
> `enforce-ai-availability` 记下的"修好规则夹具、没看隔七行的 AI 夹具"是同一个形状。

`MoveOnTimeout` MUST 是纯函数,MUST NOT 有副作用,并 MUST 返回一个该座位在该局面下**合法**的一步。它的返回值 MUST 与真人走的一步走同一条路 —— 即由 `IGameRules.Apply` 校验并判定结果。

**实现 MUST 保证推进对局。** 一个可以合法地无限重复的兜底动作(牌类游戏里"永远过牌")会把超时 worker 变成一个永不结束的自动对局。

这条要求**不是防自旋的护栏**:每一次兜底都要等满一个超时周期,所以最坏情况是每个周期一步 —— 慢、可见、不会自旋。所以本 spec MUST NOT 规定一个"连续兜底次数上限"。

#### Scenario: 现有棋种都不实现它
- **WHEN** 遍历 `BuiltInGameRules.All(lexicon)`
- **THEN** 没有一个实现 `ITimeoutFallbackRules`

#### Scenario: 兜底看得到对局设置
- **WHEN** 一个实现设置的棋种超时,规则的 `MoveOnTimeout` 被调用
- **THEN** `state.Setup` 恰好是 `Game.Setup`,一字不改

#### Scenario: 兜底动作要经过合法性校验
- **WHEN** 一个实现返回了该局面下非法的一步
- **THEN** `Apply` MUST 抛 `InvalidMoveException`,而对局状态 MUST NOT 改变
