## Why

斗地主要三个座位。但「两人」这个假设**并不像看起来那么弥散** —— 量过：

| `Stone` 出现在 | 次数 / 文件 | 该不该动 |
| --- | --- | --- |
| `Gewu.Domain/Rooms`(聚合本体) | 32 / 3 | **要动** |
| `Gewu.Domain/ValueObjects`(`MoveIntent`、`PlayedMove`) | 16 / 2 | **要动** |
| `Gewu.Domain/Games`(各棋种规则内部) | 119 / 10 | **不动** —— 棋盘上那个东西就叫子 |
| Application / Infrastructure / Api | 48 / 28 | 大多是 DTO 映射 |

内核里的两人假设,归到底是**一行**:

```csharp
CurrentTurn = stone == Stone.Black ? Stone.White : Stone.Black;
```

所以这次做**最小的那件真事**:内核不再说 `Stone`,改说**座位号**;棋盘类游戏在自己的规则内部继续用 `Stone`。数据库仍然是两个座位列,每个游戏仍然是两个座位 —— **行为零变化**。

## 为什么现在做,以及为什么只做这一件

这个仓库有两次成功的先例:`generalize-match-domain`(为象棋)与 `generalize-match-payload`(为成语接龙)。两次都是**对着一份已经写下来、尚未实现的具体规则**去塑形,而不是对着一个想象中的通用性 —— 那是它们成立的原因。斗地主的规则现在写下来了,而且家规分歧点已经逐条定过。

**同时它刻意不把使能工作一次做完。** 另外三件各自延期,并且各带一个触发条件:

| 延期的 | 触发条件 |
| --- | --- |
| 座位落到数据库(N 个座位 + 迁移) | 第一个 `SeatCount != 2` 的游戏 |
| 按座位投影状态(私有手牌) | 第一个有按座位隐藏信息的游戏 |
| 出牌载荷(一组牌) | 第一个"一手就是一组牌"的游戏 |

理由是 `add-tetris` 立下的那条:**用一个假实现去"证明"接缝通用,是证明不了的** —— `add-puzzle-core` 正是这么干的,注册了一个照着成语纵横捏的 fake,然后华容道一到,`Validate` 和 `Score` 两个都得改。

而本次不同:它引入的座位抽象**落地当天就被现有五个棋种全部用上**,所以它是当场被检验的,不是赊账的。这是同一条规矩的两面,不是例外。

## What Changes

- `IGameRules.SeatCount` —— 现有实现全部返回 2。
- 内核改说座位:`Game.CurrentTurn`、`Move` 上记录的出手方、`Room.PlayMove` 的解析,全部换成座位号。轮转从布尔翻转变成 `(seat + 1) % SeatCount`。
- `IGameRules.Apply(history, intent, seat)`;`INInARowRules` / `XiangqiRules` 在**自己内部**把座位 0/1 映成 `Stone.Black`/`Stone.White`。
- 新不变量 **`IsRated ⇒ SeatCount == 2`**,与既有的 `IsRated ⇒ SupportsHumanVsHuman` 并列,在构造器里强制。

  这条是给斗地主"第一版不计分"用的:现有 ELO 是两人制的,三人评分要单独设计。**把它写成不变量而不是注释里的 TODO,是因为这个仓库为后者付过账** —— `add-game-capabilities` 就是把一个手工维护的布尔约束成结构性事实的那次。等三人评分真设计出来那天,是去改这条不变量,而不是希望有人记得。

- **`Stone` MUST NOT 出现在 `Gewu.Domain/Rooms/` 下的任何文件里。** 这是"内核不知道一个游戏有几个人"的可执行形式,和 `fix-spectator-chat-leak` 那条"`JoinAsSpectator` 不许提到 `GameKey`"是同一种断言。

## 线上格式不变,而这是刻意的

`MoveDto.stone` 仍然是 `'Black' | 'White'`,Api 边界上由座位 0/1 映射过去。所以**前端一行不改**(那 25 个文件动不到),这次是纯后端内部重构。

这层映射是**带触发条件的债**,不是疏漏:第一个三座位游戏落地那天,DTO 加座位字段,映射删掉。写在这里,是因为一层没写下理由的边界映射,下一个人读到时会当成手滑。

## 被否掉的方案(都是量过或有先例的)

1. **把 `Stone` 全局换成 `Seat`** —— 354 次、后端 51 + 前端 25 个文件。不只是评审不了,而且是错的:棋盘上那颗东西就该叫子。这是**量出来的否决,不是感觉**。
2. **加一个 `Stone.Third`** —— 对一个叫"子"的类型说谎,而且到四人局又要加第四个。
3. **给牌类游戏另建一个聚合**(像谜题和 score-attack 那样) —— 否掉的理由是**各品类实际复用了什么**:谜题和 score-attack 用不到房间生命周期、座位、轮次超时、聊天、围观、复盘、SignalR 里的**任何一样**,所以它们另立聚合是省事;牌类要**全部**。复制一遍内核才是更大的代价。这是与 `add-puzzle-core` 相反的取舍,而两次的理由是同一条。
4. **保留两列再加一个可空的第三列** —— 一个写死的上限披着通用化的外衣,而且正是 `AddRoomGameKey` 那个 `defaultValue: ""` 的形状。

## Impact

- `Gewu.Domain`(`Room` / `Game` / `Move` / `MoveIntent` / `PlayedMove`)、五个 `IGameRules` 实现、Application 的落子路径、Api 的 DTO 映射。
- **数据库零改动**(座位仍是两列),**前端零改动**,**线上格式零改动**。
- 受影响 spec:`room-and-gameplay`、`game-rules-registry`。

## 验收标准

1. **现有测试一条不改地全绿。** 行为零变化,所以任何需要改断言的地方都说明这次改动越界了。
2. `Gewu.Domain/Rooms/` 下 grep `Stone` 为 0 —— 源码级断言,变异检查:塞回一个 `Stone` 引用要能让它红。
3. 一条对 `Game` 的单元测试:`SeatCount == 3` 时轮转是 `0 → 1 → 2 → 0`。

   第 3 条用的是一个假的三座位规则,而这**不违反**上面引的那条规矩:一个 fake 证明不了**接缝的形状**,但它能证明**取模算术**。区别在于被测的东西是不是"这个接口对第二种实现够不够用" —— 这里被测的是 `(seat + 1) % n`。
