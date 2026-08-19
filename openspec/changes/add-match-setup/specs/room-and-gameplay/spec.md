# room-and-gameplay Specification Delta

## MODIFIED Requirements

### Requirement: `Game` 子实体承载对局运行状态

`Game` MUST 包含字段:
- `Id: Guid`
- `RoomId: RoomId`
- `StartedAt: DateTime`(UTC)
- `EndedAt: DateTime?`
- `Result: GameResult?`(对局进行时为 `null`)
- `WinnerUserId: UserId?`
- `EndReason: GameEndReason?`(结束时非 null,与 `Result` 同时为 null 或同时非 null)
- `CurrentTurn: int` —— **座位号**,`0` 到 `SeatCount - 1`
- **`Setup: string?`** —— 本局的**服务端侧对局设置**;不需要设置的棋种为 `null`
- `Moves: IReadOnlyCollection<Move>`
- `RowVersion: byte[]`(乐观并发令牌,由 Infrastructure 层维护)

`CurrentTurn` MUST 是座位号而 MUST NOT 是 `Stone`。轮转 MUST 为 `(CurrentTurn + 1) % SeatCount`,而 MUST NOT 是两值之间的布尔翻转。

**`Stone` MUST NOT 出现在 `Gewu.Domain/Rooms/` 下的任何文件中。** 这是"内核不知道一个游戏有几个人"的可执行形式,MUST 由一条测试强制而不是靠约定。

`Stone` 本身不废弃,它下沉到棋盘类棋种的规则内部。`add-xiangqi` 立下的「`Stone.Black` 就是红」那条读法**一个字不动**。

#### `Setup` 是一个内核从不解释的字符串

内核 MUST NOT 读它的内容、MUST NOT 校验它的格式、MUST NOT 依赖它的长度。它由规则造(`IDealtGameRules.CreateSetup`)、由规则读(将来的 `Apply`),对 `Game` 而言只是一段随本局存下来的字节。

**它 MUST NOT 出现在任何 DTO 上。** 斗地主的 `Setup` 就是三家的底牌 —— 与成语纵横「答案不出服务端」是同一条平台规则:*客户端算不出来的东西,客户端就骗不了*。将来每个座位**各自**收到自己那一份是另一件事,由那个棋种的可见性变更定义;整份设置永远不出服务端。

这一条 MUST 由**一条反射断言**强制:`Gewu.Application.Common.DTOs` 命名空间下的任何类型都不得有名字含 `Setup` 的成员。行为测试只能证明**今天**的投影没带上它,而一个字段会不会被序列化取决于它在不在 DTO 上 —— **一个不存在的成员没有明天。** 同 `add-tetris` 让"客户端自述的分数"无处可去的那条断言。

`Game` 不独立于 `Room` 存活;构造仅由 `Room.JoinAsPlayer` 内部发生。`Game.FinishWith` 的签名 MUST 为 `FinishWith(GameResult, UserId?, GameEndReason, DateTime)`。

#### Scenario: 初始 Game 状态
- **WHEN** 坐满触发 `JoinAsPlayer`
- **THEN** `Game.StartedAt == now`;`CurrentTurn == 0`;`Moves` 空;`EndedAt == null`;`Result == null`;`EndReason == null`

#### Scenario: 不需要设置的棋种其 Setup 为 null
- **WHEN** 一个不实现 `IDealtGameRules` 的棋种开局
- **THEN** `Game.Setup == null` —— MUST NOT 是 `""`,空字符串会让"这个棋种没有设置"与"设置是空的"看起来一样

#### Scenario: 需要设置的棋种其 Setup 被存下来
- **WHEN** 一个实现 `IDealtGameRules` 的棋种开局,`JoinAsPlayer` 收到的 `setup` 是 `"abc"`
- **THEN** `Game.Setup == "abc"`,一字不改

#### Scenario: 任何 DTO 都不暴露 Setup
- **WHEN** 反射遍历 `Gewu.Application.Common.DTOs` 下的全部类型
- **THEN** 没有任何成员的名字含 `Setup`

#### Scenario: 两座位游戏的轮转不变
- **WHEN** 一个 `SeatCount == 2` 的棋种连走 3 步
- **THEN** `CurrentTurn` 依次为 `0 → 1 → 0 → 1`

#### Scenario: 三座位游戏按环轮转
- **WHEN** 一个 `SeatCount == 3` 的规则连走 3 步
- **THEN** `CurrentTurn` 依次为 `0 → 1 → 2 → 0`

  这一条用一个假的三座位规则验证,而它证明的是**取模算术**,MUST NOT 被当成"这个接缝对牌类够用"的证据 —— 后者只有真游戏能证。

#### Scenario: Game 结束状态
- **WHEN** 某方获胜或平局或认输或超时后
- **THEN** `EndedAt != null`;`Result != null`;若有胜方则 `WinnerUserId != null`;`EndReason != null` 且对应路径

### Requirement: `Room.JoinAsPlayer` 让玩家入座,坐满才开局

系统 SHALL 提供 `Room.JoinAsPlayer(UserId userId, DateTime now, IGameRules rules, string? setup)`。调用后:

- 若 `Status != Waiting`:MUST 抛 `RoomNotWaitingException`
- 若 `SeatOf(userId) != null`(已经坐着,含创建者):MUST 抛 `AlreadyInRoomException`
- 若 `userId ∈ Spectators`:MUST 先从围观者集合移除,再入座
- 若 `Seats.Count >= rules.SeatCount`:MUST 抛 `RoomFullException`
- 否则:在**下一个空座位号**入座;**当且仅当**坐满(`Seats.Count == rules.SeatCount`)时 `Status = Playing` 且 `Game = new Game(currentTurn: 0, startedAt: now, setup: setup)`

**座位数由 `rules` 给,MUST NOT 存在 `Room` 上。** 存一份就是规则事实的第二份副本,而它错了的表现是"房间永远开不了局"或"少一个人就开局了"。

#### `setup` 与规则 MUST 一致,且没有默认值

`setup` 参数 MUST 是**必填的可空参数**,MUST NOT 有默认值:默认值会让"忘了传"和"故意不传"在源码里长得一模一样,而那正是 `fix-spectator-chat-leak` 给 `ToState` 加必填 `RoomView` 时写下的理由。

而且 `Room` MUST 校验两者一致:

- `rules is IDealtGameRules` 而 `setup` 为 `null` → MUST 抛
- `rules` 不是 `IDealtGameRules` 而 `setup` 非 `null` → MUST 抛

于是"忘了传"是一个异常,不是一局没有牌的斗地主。第二条同样要有:一个把设置传给不需要设置的棋种的调用方,拿着一个错误的心智模型,而那份设置会被存下来再也没人读。

`setup` MUST 由**调用方**造好传进来,而 MUST NOT 由 `Room` 从一个种子生成 —— 造它需要熵,而 Domain 不该知道有一个随机源。熵的来源是 Application 层已有的 `ISeedProvider`。这也让测试可复现:传一个钉住的设置串,而不是"发了什么算什么"。

#### Scenario: 两人棋种第二位玩家加入即开局
- **WHEN** 房间处于 `Waiting`,`SeatCount == 2`,调用 `JoinAsPlayer(bobId, now, rules, null)`
- **THEN** `PlayerAt(1) == bobId`;`Status == Playing`;`Game.CurrentTurn == 0`;`Game.StartedAt == now`;`Game.Setup == null`

#### Scenario: 三人棋种坐第二个人时仍然等待
- **WHEN** `SeatCount == 3`,房间里已有 host,第二个人 `JoinAsPlayer`
- **THEN** `Seats.Count == 2`;**`Status` 仍为 `Waiting`**;`Game == null`

#### Scenario: 三人棋种坐满第三个人才开局
- **WHEN** 承上,第三个人 `JoinAsPlayer`
- **THEN** `Seats` 的 `Index` 为 `0, 1, 2`;`Status == Playing`;`Game.CurrentTurn == 0`

#### Scenario: 需要设置的棋种坐满时设置落在 Game 上
- **WHEN** 规则实现 `IDealtGameRules`,最后一个人入座时 `setup` 为 `"deal"`
- **THEN** `Game.Setup == "deal"`

#### Scenario: 需要设置却没给,拒绝
- **WHEN** 规则实现 `IDealtGameRules`,最后一个人入座时 `setup` 为 `null`
- **THEN** MUST 抛;`Status` 仍为 `Waiting`,`Game == null` —— MUST NOT 开出一局没有设置的棋

#### Scenario: 不需要设置却给了,拒绝
- **WHEN** 规则不实现 `IDealtGameRules` 而 `setup` 非 `null`
- **THEN** MUST 抛

#### Scenario: 坐不满时不校验设置
- **WHEN** `SeatCount == 3`,第二个人入座,规则实现 `IDealtGameRules` 而 `setup` 为 `null`
- **THEN** 不抛 —— 还没开局,设置此刻无从谈起

  这一条是刻意的:一致性校验发生在**开局那一刻**,而不是每一次入座。否则三人棋种的前两次入座都得携带一份最终会被丢掉的设置,而那份设置的存在会误导下一个读代码的人。

#### Scenario: 非等待状态
- **WHEN** `Status` 为 `Playing` 或 `Finished`,调用 `JoinAsPlayer`
- **THEN** 抛 `RoomNotWaitingException`

#### Scenario: 已经坐着的人重复加入
- **WHEN** 任一已入座的用户(含创建者)再调 `JoinAsPlayer`
- **THEN** 抛 `AlreadyInRoomException`,消息点明他已坐在几号

#### Scenario: 围观者升级为玩家
- **WHEN** 用户先进入围观者集合,随后调 `JoinAsPlayer`
- **THEN** 该用户从 `Spectators` 移除,在下一个空座位入座

#### Scenario: 座位坐满后再有人要坐
- **WHEN** 座位已满
- **THEN** 抛 `RoomNotWaitingException` 或 `RoomFullException`

## ADDED Requirements

### Requirement: `AddGameSetup` 迁移只加一列

`AddGameSetup` SHALL 给 `Games` 加一个可空的 `Setup` 列(`TEXT`),既有行为 `NULL`。

这是**纯加宽**:没有回填、没有值重映射、没有删列。因此 EF 生成的版本**可以直接采用** —— 这与本仓库前四次迁移都不同,而能这么说是因为核对过,不是因为默认它对。

`Down` 直接删列。收窄一列而底下有数据时通常必须拒绝(见 `AddMoveTextPayload`),但这里不同:`Setup` 的**唯一**读者是需要它的那个棋种的规则,而回滚到这个迁移之前意味着那个棋种还不存在,所以不可能有非 `NULL` 的行需要保护。**这个理由 MUST 写在迁移里** —— 否则下一个人只看到"这个 `Down` 没有守卫",而无从知道那是核对过的结论还是漏掉的。

#### Scenario: 既有对局不受影响
- **WHEN** 在含既有 `Games` 行的库上跑迁移
- **THEN** 每行的其他列一字不变;`Setup` 为 `NULL`

#### Scenario: 回滚删列
- **WHEN** 回滚该迁移
- **THEN** `Setup` 列消失,其他列不变
