# history-every-seat

战绩也说得出每一个座位上的人 —— 说不出的那一格,明说说不出。

## Why

`replay-every-seat` 修了回放,并把**同一个缺陷的另一半**列为「不做」,拆除条件写的是:

> 有人要在战绩列表里正确显示一局三人牌局的结果时(那时「谁赢了」也已经不是一个
> `WinnerUserId` 说得清的了)。

**那个条件到了。** 而括号里那句话是这个变更最难的一半,不是最容易的一半。

### 一、`UserGameSummaryDto` 丢人,和回放丢得一模一样

```csharp
Black: new UserSummaryDto(r.BlackPlayerId.Value, ...),
White: new UserSummaryDto(whiteId.Value, ...),
```

`GetUserGamesPagedQueryHandler` 与回放的 handler 逐字同形。仓储**不过滤棋种**
(`RoomSeats.Any(...)`,所有座位),所以三座位对局照样进战绩列表 —— 进来之后少一个人。

### 二、「对手」是单数,而三人局有两个对手

两处消费方写的是同一行代码:

```ts
return g.black.id === this.userId() ? g.white : g.black;
```

规格里那句话也是单数的:「"对手" = 当前 profile 的 user **不是**的那一方」。于是三人局里
**另外两个人只显示得出一个**,而显示的是哪一个取决于本人坐 0 号还是别的座位。
读起来像是那一局只有两个人 —— 不是缺一个字段的样子,是数据看起来很正常的样子。

### 三、最难的那一半:这一行**说不出**谁赢了,而它现在在瞎说

```csharp
// 打完最后一张就赢了 —— 赢家是**这个座位**,不是"农民方"。`WinnerUserId` 只能装一个人,
// 而两名农民一起赢装不进去;客户端从叫分历史里知道谁是地主,自己能说出"农民赢了"。
```

领域层把取舍写清楚了,也留了出路 —— **而那条出路在这个 DTO 上不成立**:
`UserGameSummaryDto` 刻意不含 `Moves`(「列表视图太重」,而那个决定是对的)。

于是今天:

```ts
return g.winnerUserId === this.userId() ? 'profile.result-win' : 'profile.result-loss';
```

**没走出去的那个农民,自己赢的一局显示成「负」。**

这一条比前两条更值得单独说,理由是**只加 `Seats` 会把它盖起来**:那一行会列出两个对手、
看起来完全正常,然后继续说错的胜负。**一个看起来被修好的错误比一个明显的错误更难被发现。**

## What Changes

- `UserGameSummaryDto` 去掉 `Black` / `White`,换成 `Seats`(与 `GameReplayDto` /
  `RoomStateDto` **同一个** `RoomSeatDto`);handler 走 `room.ToSeatDtos(usernames)`,
  与回放那条共用 `replay-every-seat` 抽出来的那一份。
- 两处消费方:`opponentOf`(单数)→ 列出 `seats` 里除本人以外的**每一个**。
- **结果那一格从三支变四支**,新增「说不出」:
  平局 → 平;赢家是我 → 胜;**两座位**且赢家不是我 → 负;**其余 → 说不出**。
- 新增一个 i18n 键 `profile.result-unrecorded`(zh / en)。

### 为什么是「说不出」,而不是把它算对

算对要的是棋种自己的**阵营**概念 —— `DoudizhuScoring.Settle` 知道,而它至今**没有生产
调用方**,那笔账的拆除条件是「平台需要一条点数阶梯」。把它拖进来会让这个变更从一个
DTO 修复变成一条新的结算链路。

**而「说不出」不是拖延,它是这一行现在唯一为真的话。** 两座位那三支照旧,所以这不是
把所有人的胜负都变模糊 —— 它只在真的说不出时才出现。

## Non-goals

- **不给三人局算每人胜负。** 见上,拆除条件是点数阶梯。
- **不加 `GameKey` 到这个 DTO。** 修这个缺陷不需要它:对手是谁、胜负说不说得出,
  两者都只依赖 `seats.length` 与 `winnerUserId`。**一个不被读的字段正是
  `add-match-setup` 踩过的坑。**
- **不改 `WinnerUserId` 的形状。** 它装一个座位是领域层写明的取舍,改它是 `room-and-gameplay`
  的账,而且要先有阵营概念。
- **不动战绩的排序、分页、权限。** 那几条要求本变更一个字不改。
