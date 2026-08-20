# fix-lobby-seats

## Why

**大厅把每一个房间都当成两个人的。** 量出来的,不是推的 —— 三座位房间在 `/g/doudizhu/lobby`
上今天就是这样:

| 位置 | 现状 | 后果 |
| --- | --- | --- |
| `active-rooms.html` | 写死两行 `lobby.rooms.seat-black` / `seat-white` | 斗地主房间显示「**黑方**: A / **白方**: B」—— 标签是错的,而**2 号座位上的人根本不出现** |
| `my-active-rooms.ts` `sideKey()` | `room.black?.id === myId ? 'black' : room.white?.id === myId ? 'white' : 'spectator'` | 2 号座位在**自己的**对局里被标成「你是观战」 |
| `RoomSummaryDto`(服务端) | 只有 `Black` / `White`,没有座位列表 | 上面两处**没有别的数据可读** |
| `RoomSummary`(客户端模型) | 同上 | 同上 |

**这是同一个缺陷的第三与第四处。** 前两处已经修过:`add-web-doudizhu` 修掉侧栏
「白方走棋」(三座位房间里那句话说的是一个不存在的方),`add-doudizhu-table-visuals` 修掉侧栏
只列黑白两个人(2 号座位上的人在自己的房间里不出现)。两次都只修了**房间页**,
因为那是当时屏幕上看得见的地方 —— **而大厅读的是另一个 DTO**,所以那两次的修复对它一行影响都没有。

`generalize-match-contract` 明确把这一笔留了下来(它做的是 `RoomStateDto`),而 `AiSmoke` 里
那两条断言至今绿着,注释却写着「add-doudizhu-visibility 付这笔账」—— **付的是另一个 DTO 的账**。
那段注释在 `add-wakeng` 里已经改对,这个变更付真正的那笔。

触发条件到了:`add-web-wakeng` 要给第二个三座位棋种画大厅。

## What changes

### 服务端 —— 一个字段

- `RoomSummaryDto` 加 `IReadOnlyList<RoomSeatDto> Seats`,与 `RoomStateDto` **同一个类型、
  同一个形状**。`Black` / `White` **保留** —— 与 `RoomStateDto` 的先例一致(它加 `Seats` 时也
  没有删那两个),而四个两座位棋种的每一个读者都还在用它们。
- 投影处按 `RoomStateDto` 的写法照做。**没有新概念,没有迁移,没有规则改动。**

### 客户端 —— 两处读法

- `RoomSummary` 模型加 `seats`。
- `active-rooms` 渲染**在座的玩家**,而不是两行写死的「黑方 / 白方」。**大厅不再知道
  「一个房间有两个人」。**

  **标签里没有颜色,而这不是我挑的 —— 是 `board-seats.ts` 自己写下的约束。** 它的文档说
  「Only the board family may call it. A game with more than two seats has no colours to map,
  which is why nothing outside `games/` and the board components uses it.」 大厅不是棋盘,
  所以它 MUST NOT 读那套颜色。于是行上是**人名**,座位号连显示都不需要:大厅要回答的是
  「这里面有谁、我能不能进去」,而「谁坐第几号」是房间页的事。

  **`seat-empty` 那个占位符因此消失,而理由要写下来。** 今天两座位房间显示
  「黑方: A · 白方: **空**」。`seats` 只含**在座的**座位,而一个房间**一共有几个座位**
  不在这个 DTO 里 —— 要么加 `seatCount` 到 `GET /api/games`,要么不画那个空位。
  选后者:`add-web-doudizhu` 已经为同一个取舍判过一次(「为一个你已经握在手里的数去
  取一个异步依赖,是把一个同步事实变成一个加载状态」),而这里连那个数都还没有,代价是
  整行要等 `capabilities.loaded()`。而「还有空位」这件事在同一行上**已经说了两遍**:
  Waiting 状态徽章,和那颗 Join 按钮。
- `my-active-rooms.sideKey()` 从 `seats` 找自己:找到就是「你在 N 号座位」,找不到才是观战。
  **「不在座位上」与「在第三个座位上」MUST NOT 是同一个答案** —— 这与
  `fix-three-seat-membership` 在服务端修的是同一句话,只是那边的后果是拿到了整个围观频道。

## 顺带量到的第五处,而它**不在**本变更范围内

侧栏那条修复(`add-doudizhu-table-visuals`)的判据是 `seats.length > 2`,而它自己的注释写着
「座位表就在这份快照里,不必去问注册表要 `seatCount`」。那句话回答的是**另一个问题**:

> **`seats.length` 不是「这个棋种有几个座位」,是「有几个座位被坐上了」。**

于是一个**等待中**的三座位房间,侧栏仍然渲染「黑方 / 白方」—— 同一个缺陷的第五处,
而它就藏在为修它而加的那个分支里。**这一条是在浏览器里量到的,不是读代码推的**:
一个两人在座的斗地主房间,侧栏原文是 `Black: Baa11… White: Caa11…`。它**不能**用本变更的办法修:侧栏**想要**给座位起名字、
并标出哪个是空的,而那两件事真的需要「一共有几个座位」。

那个数是 `IGameRules.SeatCount`,一个结构性事实,而 `GET /api/games` 今天不发它 ——
`Rows` / `Cols` 就在旁边,同样的投影方式。**触发条件:下一个变更 `publish-seat-count`。**
拆开是因为它给大厅行加一个异步门(`capabilities.loaded()`),而 `generalize-lobby` 量过那笔账;
把一个契约字段悄悄塞进一个缺陷修复里,是这个仓库自己判过的坏做法。

## 不做什么

- **不删 `Black` / `White`。** 删它是另一个变更(`RoomStateDto` 那边也还没删),而这里
  只要把「读不到第三个座位」补上。一个不该删的东西顺手删掉,会让这个 PR 的 diff 里
  混进几十个与缺陷无关的改动。
- **不动大厅的其它卡片。** `my-recent-games` 读的是**对局记录**(`g.black` / `g.white`),
  那是另一个 DTO、另一笔账,而三座位棋种不计分、不进那份记录 —— 今天它连数据都没有。
  写下来是因为「看起来像同一个缺陷」和「是同一个缺陷」得分开。

## 验收

- 一条**遍历**断言:大厅的房间行渲染出的人数 MUST 等于 `seats.length`,对 2 个与 3 个座位
  都走一遍。写成遍历而不是「斗地主房间画三个人」,是因为后者在一个把第三个人硬编码
  进去的实现上也绿。
- 变异:把 `seats` 的渲染换回读 `black`/`white` MUST 红;`sideKey` 忽略 `seats` MUST 红。
- 375 px:三座位房间的房间行**带满三个人名**时不横向溢出 —— `generalize-lobby` 记的那条
  (「空列表上『无横向滚动』这条检查是白过的」)在这里是原样适用:两个人名的行过得去,
  三个未必。
