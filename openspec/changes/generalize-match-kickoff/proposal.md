# generalize-match-kickoff

## Why

挖坑的规则是:**持最小 ♣ 的人首叫且首出**。而内核的 `Game` 构造函数里写着
`CurrentTurn = FirstSeat`(常量 0)—— 这是内核第五处「对到目前为止的每个棋种都成立、
于是被写死」的假设,前四处是两个座位、颜色命名的胜负、开局设置、以及下一手是谁。

五个现有棋种的先手都是**约定**:谁坐 0 号谁先。挖坑不是 —— 它的先手是**发牌**决定的,
而且必须每局轮换。

**把发牌旋转成「最小 ♣ 总在 0 号」被考虑过并否掉了**:统计上等价,体验上不等价 ——
那样同一个人每一局都先叫,而先叫在挖坑里是有利有弊的一个位置。

## What Changes

- 新 seam `IFirstSeatRules.FirstSeat(MatchState) → int`。
- `Game` 的构造函数收一个首手座位;`Room` 在开局那一刻算它,默认仍是 0。
- 越界的返回值在**开局那一刻**抛 `InvalidFirstSeatException`(`invalid-first-seat`)。
- **五个现有棋种一行不动**,1294 条既有测试一条不改。

## 三个决定

**一、又是一个单独的接口,而不是给 `IGameRules` 加成员。**
理由与 `IDealtGameRules` / `IPerSeatViewRules` 当初分出来时逐字相同:留在基接口上,
五个现有棋种就得各写一个骗人的实现,而**骗人的实现是下一个人删不掉的东西** —— 他无从知道
有没有调用方。

**二、它收 `MatchState` 而不是收设置。**
开局那一刻历史是空的,唯一有内容的是设置(发牌)—— 但形状与 `Apply` / `MoveOnTimeout` /
`ViewFor` 一致,而**四个 seam 说同一种话**比省一个字段重要。一条测试钉住开局时历史是空的
而不是 null。

**三、越界不存,当场抛。**
存下来会造出一局**谁都动不了**的棋:每个人都不是当前回合,于是几十秒后由超时兜底暴露出来 ——
而那时报的是超时,不是「首手座位是 99」。这与 `MissingGameSetupException` 的理由是同一条:
一份存下来再也没人读的错误状态,是最贵的那种错。

## Impact

- Affected specs: `room-and-gameplay`
- Affected code: `Games/Abstractions/IGameRules.cs`、`Rooms/Game.cs`、`Rooms/Room.cs`、
  `Exceptions/RoomExceptions.cs`,新增 `FirstSeatTests`
- **没有棋种在这次里落地** —— 与 `generalize-match-domain` / `generalize-match-payload` /
  `add-match-setup` 同一形状:seam 先按一份写下来但还没实现的规则塑形,而它只有等挖坑落地
  才**被证明**。
