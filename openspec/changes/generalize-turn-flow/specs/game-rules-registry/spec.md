# game-rules-registry Specification Delta

## ADDED Requirements

### Requirement: `ITimeoutFallbackRules` 让超时变成"替他走一步"而不是"判他负"

`Gewu.Domain` SHALL 定义:

```
public interface ITimeoutFallbackRules : IGameRules
{
    MoveIntent MoveOnTimeout(IReadOnlyList<PlayedMove> history, int seat);
}
```

只有"超时不该判负"的棋种实现它。五子棋、一字棋、中国象棋、成语接龙**一行不动** —— 两个座位下"判他负、对手胜"是清楚且唯一的答案。

分出一个接口而不是给 `IGameRules` 加成员,理由与 `IBoardGameRules` / `IDealtGameRules` 相同:留在基接口上,四个棋种就得各写一个骗人的实现,而**骗人的实现是下一个人删不掉的东西**。

`MoveOnTimeout` MUST 是纯函数,MUST NOT 有副作用,并 MUST 返回一个该座位在该局面下**合法**的一步。它的返回值 MUST 与真人走的一步走同一条路 —— 即由 `IGameRules.Apply` 校验并判定结果,见 `room-and-gameplay`。

**实现 MUST 保证推进对局。** 一个可以合法地无限重复的兜底动作(牌类游戏里"永远过牌")会把超时 worker 变成一个永不结束的自动对局。斗地主的形式是"能过就过,**不能过时出最小的一手**",而牌只会变少。

这条要求**不是防自旋的护栏**,理由要写清楚:每一次兜底都要等满一个超时周期(worker 从最后一手的 `PlayedAt` 重算 `lastActivity`),所以最坏情况是每个周期一步 —— 慢、可见、不会自旋。它是**对局质量**的要求,所以本 spec MUST NOT 规定一个"连续兜底次数上限":那个数字会是凭空的,而它要防的东西并不存在。

#### Scenario: 现有棋种都不实现它
- **WHEN** 遍历 `BuiltInGameRules.All(lexicon)`
- **THEN** 没有一个实现 `ITimeoutFallbackRules`

  这一条会在斗地主落地那天由那次变更改成"恰好一个实现它"。它现在钉住的是**本次变更没有偷偷改动任何现有棋种**。

#### Scenario: 兜底动作要经过合法性校验
- **WHEN** 一个实现返回了该局面下非法的一步
- **THEN** `Apply` MUST 抛 `InvalidMoveException`,而对局状态 MUST NOT 改变 —— "系统替他走的"不是绕过校验的理由

### Requirement: `MoveApplication.NextSeat` 让规则指定下一手是谁

`MoveApplication` SHALL 为:

```
public readonly record struct MoveApplication(GameResult Result, int? WinnerSeat, int? NextSeat);
```

`NextSeat` 为 `null` 表示**按环轮转**(`(seat + 1) % SeatCount`);非 `null` 表示下一手轮到该座位。

斗地主需要它:叫分结束之后先出牌的是**地主**,而地主可能是任何一个座位,与最后叫分的是谁无关。

#### `null` 表示轮转,而这与「参数不给默认值」不矛盾

本平台的既有纪律是"默认值会让'忘了传'和'故意不传'长得一样"(见 `Room.JoinAsPlayer` 的 `setup`)。这里给 `null` 一个默认语义,判据是**忘了会不会有人发现**:

- 忘了传 `setup` → 一局没有牌的棋,要到第一次出牌才炸,离开局已过去几十秒。
- 忘了给 `NextSeat` → **下一手轮到错的人**,在那个棋种的第一条测试里就会红。

而且 `null` 在这里有真实含义,不是"没填":四个现有棋种的每一手、以及斗地主出牌阶段的每一手,答案确实都是"按环轮转"。让五个实现每次都算一遍内核已经知道的事,是重复而不是明确。

**判出胜负或和局时 `NextSeat` MUST 为 `null`** —— 对局结束了,没有下一手。由构造器强制,与 `WinnerSeat` 那条同一种机制。

#### Scenario: 不指定就按环轮转
- **WHEN** 规则返回 `MoveApplication.Ongoing()`,由座位 `s` 走出,`SeatCount == n`
- **THEN** `Game.CurrentTurn == (s + 1) % n`

#### Scenario: 指定了就听规则的
- **WHEN** 一个三座位规则在座位 `0` 走完之后返回 `NextSeat == 2`
- **THEN** `Game.CurrentTurn == 2`

#### Scenario: 结束的对局不能有下一手
- **WHEN** 构造 `MoveApplication(GameResult.Decided, 0, nextSeat: 1)` 或 `MoveApplication(GameResult.Draw, null, nextSeat: 0)`
- **THEN** 构造 MUST 失败并抛

#### Scenario: 负数不是座位
- **WHEN** 构造一个 `NextSeat` 为负数的 `MoveApplication`
- **THEN** 构造 MUST 失败并抛
