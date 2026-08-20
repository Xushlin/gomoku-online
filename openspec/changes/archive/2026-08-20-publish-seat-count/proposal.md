# publish-seat-count

## Why

**`seats.length` 不是「这个棋种有几个座位」,是「有几个座位被坐上了」。** 侧栏拿它当前者用,
于是一个**等待中**的三座位房间渲染的是「黑方 / 白方」—— 在屏幕上量到的,原文是
`Black: Baa11… White: Caa11…`。

这是同一个缺陷的**第五处**,而它就藏在为修它而加的那个分支里:
`add-doudizhu-table-visuals` 加了 `moreThanTwoSeats()`,注释写着「座位表就在这份快照里,
不必去问注册表要 `seatCount`」。那句话回答的是另一个问题 —— 对局进行中三个座位都坐满,
所以它当时是对的;而房间在坐满**之前**也要渲染,而那时它是错的。

**「这个棋种有几个座位」是一个结构性事实**(`IGameRules.SeatCount`),而客户端今天读不到它。
`GET /api/games` 就在旁边发着 `rows` / `cols`,同样的投影方式。

## What changes

### 服务端 —— 一个字段,一处投影

- `GameDescriptorDto` 加 `int SeatCount`。**非空** —— 每个有 `IGameRules` 的棋种都有座位数,
  不存在「不适用」。(对比 `Rows` / `Cols`:它们可空,因为成语接龙真的没有盘面。)
- 投影自 `IGameRules.SeatCount`,与其它字段同一处。
- `GetGameDescriptorsQueryHandlerTests.The_dto_does_not_carry_WinLength` **会红** ——
  它断言的是**整个**属性集合,注释写着「加字段时它会红,那正是想要的:对外契约多一个字段
  该是一次有意的决定,不是一次顺手的提交」。**这就是那次有意的决定。**

### 客户端 —— 一处判据,零个新服务成员

- `GameDescriptor` 模型加 `seatCount`。
- 侧栏的 `moreThanTwoSeats()` 改读 `capabilities.of(gameKey)?.seatCount`,而
  **`GameCapabilitiesService` 一行不改** —— 它已经有 `of(key)` 返回整个描述符。
- **异步的账已经付过了**:`room-page.ts` 的 `loading()` 里本来就有
  `!this.capabilities.loaded()`(`remove-manifest-board` 加的),所以描述符到达之前
  整页就是骨架屏,侧栏根本不会用一个未知的座位数去渲染。
- 顺带:座位数已知之后,泛化那一支可以把**空座位**也画出来(「3 号:空」),于是它对
  座位数大于二的棋种严格优于颜色那一支 —— 而在它之前,泛化支只画得出在座的人。

## 为什么颜色那一支**留着**

两座位棋盘棋种的侧栏 MUST 继续说「黑方 / 白方」(象棋读作红 / 黑)。那不是遗留:
你正看着一张摆着黑白子的棋盘,而「谁是黑方」是座位号给不出的信息。

**大厅的答案与这里不同,而两个都对。** `fix-lobby-seats` 让大厅行**不许**说颜色,
理由是 `board-seats.ts` 自己的禁令(那套读法只有棋盘家族可以调)—— 大厅是跨棋种的列表,
不是棋盘。侧栏在一个具体棋种的房间里,而那个房间要么有棋盘要么没有。
**同一个问题,两个层次,两个答案** —— 而能为每一个说出不同的理由,才说明这是在应用规则,
不是在套模板。

## 不做什么

- **大厅行不读它。** `fix-lobby-seats` 已经判过:大厅渲染在座玩家,而「还有空位」这件事
  同一行上已经说了两遍(Waiting 徽章 + Join 按钮)。为一个装饰性的「N/M」给大厅列表
  加一个异步门,是 `generalize-lobby` 量过的那笔账。
- **不加 `GameCapabilitiesService` 的新成员。** `of(key)` 已经够了;多一个
  `seatCountFor()` 是同一个事实的第二个入口。
- **前端不存一份座位数副本。** 那正是 `remove-manifest-board` 删掉的东西
  (`GameManifest.board`),而它删掉的理由在这里逐字成立:一份没人读的副本错了不会有人发现。

## 验收

- 一条**遍历**断言:`GET /api/games` 每一条的 `seatCount` 等于注册表里那个棋种的
  `SeatCount`,而 MUST NOT 对着一份手写期望值比。
- 两侧都要有样本:断言集合里**同时**出现 2 和 3 —— 一条只走到 2 的遍历,在一个恒返回 2 的
  实现下是绿的。
- 侧栏:三座位**等待中**的房间 MUST 走泛化支(而这正是今天红的那一格);两座位房间 MUST
  仍然说颜色。
- 变异:`moreThanTwoSeats()` 退回读 `seats.length` MUST 红;投影写死 `2` MUST 红。
