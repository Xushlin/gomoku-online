# replay-every-seat

回放说得出每一个座位上的人。

## Why

**量到的,不是读出来的。** 用真的三座位聚合跑了一遍 `GetGameReplayQueryHandler`
(Alice / Bob / Carol 的一局斗地主,打到地主出完 20 张牌,`Status == Finished`),响应是:

```
gameKey = doudizhu
Black   = Alice        ← 0 号座位
White   = Bob          ← 1 号座位
moves   = 59, 出现过的座位号 = [0, 1, 2]
Carol 的 id 在响应里?   False
Carol 的名字在响应里?   False
```

端点**没有报错**,没有 409,没有守卫 —— 它 200 成功返回了一份**丢掉一个人**的回放。

### 比「少一个人」更硬的判据:这份载荷自相矛盾

`Moves[].Seat` 里有 `0 / 1 / 2` 三个座位号,而玩家字段只解析得出两个。59 手里有 20 手,
它们的出手人是**这份载荷自己说不出是谁**的。所以这不是标签写错了,换个称呼解决不了 ——
是 DTO 缺一个字段。

### 它是可达的,不是理论上的

| 环节 | 有没有拦住 | 实际 |
| --- | --- | --- |
| `RoomsController.Replay` | 无座位数守卫 | 任何登录用户,任何 Finished 房间 |
| `GetGameReplayQueryHandler` | 无座位数守卫 | 无条件读 `BlackPlayerId` / `WhitePlayerId` |
| 战绩列表(大厅 / 个人页) | **不过滤棋种** | 仓储走 `RoomSeats.Any(...)`,三座位房间照样列出来 |
| 房间的结束对话框 | 不分棋种 | 「查看回放」对牌局也点得动,`leaveTo('/replay/{id}')` |
| 现有测试 | **零覆盖** | `GetGameReplayQueryHandlerTests` 里 `doudizhu` / `wakeng` 各 0 处 |

也就是说:打完一局斗地主 → 点「查看回放」是主路径,而路径尽头是一页丢了一个人的回放。

### `Room` 自己的文档写着不许这么用

```
/// **牌类棋种 MUST NOT 用这两个名字** —— 三个座位没有"黑白",用 PlayerAt。
public UserId BlackPlayerId => _seats.Single(x => x.Index == FirstSeat).UserId;
```

`RoomSeatDto` 的文档更直接:「`Black` / `White` 描述不了三个座位……2 号座位上的人**在任何字段里
都不出现** —— 实测过。」**那句话是为 `RoomStateDto` 写的,而回放这条路上没人读它。**
两个 DTO 一个修了一个没修,而修好的那个把理由写在了自己身上。

### 顺带量到的两件事

**一,回放页对牌局什么都不画。** 斗地主的描述符没有 `rows` / `cols`(`DoudizhuRules` 不是
`IBoardGameRules`),所以 `boardSizeFor` 返回 `null`,三个 `@if` 分支全为假 —— 标题区下面
直接是 scrubber,拖动它不改变任何看得见的东西。**「拖了没反应」读起来是功能坏了**,
而事实是这一格从来没画过。补契约**不会**让它画出来(理由见 Non-goals)。

**二,`game-replay` 的 live spec 有一处与已发布代码相反。** 规格写着战绩查询过滤
`BlackPlayerId == userId OR WhitePlayerId == userId`,而实现走 `RoomSeats.Any(...)`。
差别只在三座位棋种上看得见:**照规格的字面写法,坐 2 号座位的人自己的对局不会出现在自己的
战绩里。** 照着规格「修正」代码会造出那个缺陷,所以一并对齐。
`openspec validate --strict` 对这种矛盾一律绿 —— 它验形状,不验真伪。

## What Changes

- `GameReplayDto` 去掉 `Black` / `White`,换成 `Seats: IReadOnlyList<RoomSeatDto>`
  —— 与 `RoomStateDto.Seats` **同一个** `RoomSeatDto`,不新造第二种形状。
- `GetGameReplayQueryHandler` 从 `room.Seats` 投影,不再读 `room.BlackPlayerId` / `WhitePlayerId`。
  用户名查询无需改动:`CollectUserIds()` 早就遍历所有座位(`ThreeSeatRoomHandlersTests` 钉着)。
- 前端 `GameReplayDto` 模型跟着改;回放页标题区按 `seats.length` 渲染,`boardState.seats`
  直接取 `replay().seats`,**删掉**那段恒为两条的合成。
- 回放页给「画不出盘面的棋种」一段说明文案(两份 locale),替掉今天的一片空白。
- 测试:一局**真的**三座位已结束对局跑通 handler,断言 `Seats.Count == 3` ——
  **恰好三条**,不是「至少两条」,少一个座位必须红。

### 为什么删而不是留

删完之后 `Black` / `White` 在整个仓库里**零个读者** —— 唯一的消费方是回放页,而它这次改成读
`Seats`(`frontend-desktop` / `frontend-mobile` 还不存在,`frontend-web` 是唯一客户端)。
`RoomStateDto` 留着这两个字段是因为**那里有真读者**;这里没有。一个没人读、又对三分之一棋种
为假的字段,是下一个人照抄的模板。

契约破坏在这里是免费的:没有生产数据,没有部署,唯一的客户端在同一个仓库里同一次改掉。

### 一条 live 要求与本变更字面冲突,所以改它而不是绕开

`game-replay` 里 `GameReplayDto 携带棋种键` 那条有个 Scenario:

> **THEN** 既有字段的名称与类型 MUST NOT 改变

原文是「比对**本变更**前后」—— 它是 `add-web-replay-and-profile` 给自己立的规矩,归档之后
变成了一条对所有人生效的 live 要求。**一条被后来的事实推翻的要求,要么改要么删,不能一边
留着一边违反。** 这里收窄成它真正要守的东西:`GameKey` 不许改名改型。

## Non-goals

- **不接牌桌。** `CardTable` 的画面全部来自 `state.game.seatView` —— 按座位投影、由 SignalR
  每步下发,而回放 DTO 里既没有它也没有那副牌。牌是 `IDealtGameRules.CreateSetup` 的服务端侧
  设置,平台规则写着它 MUST NOT 上任何 DTO。所以「牌局回放」要先回答**一局已结束的牌局,
  底牌该不该公开** —— 那是规则问题,不是接组件的问题。本变更只保证那一格**说人话**。
- **不改 `UserGameSummaryDto`。** 它有同一个缺陷,但修它要连带回答「三个人的一局,战绩行上
  的『对手』是谁」(今天 `opponentOf` 是 `black.id === me ? white : black`,对三座位悄悄给出
  两个对手中的一个)。那是显示层取舍,不是契约对错。**拆除条件写进了 spec**。
- **不改席位叫法。** 标题区今天写死 `game.room.seat-black` / `seat-white`,对象棋与成语接龙都是
  错的 —— 那是 `per-game-seat-labels` 在改的东西。**那个变更改叫法,本变更改有几个**,
  它的 proposal 里「不做,而理由要写下来」一节点名把这个缺陷留给了本变更。两者落地顺序不限。
- **不改领域字段名。** `Room.BlackPlayerId` / `WhitePlayerId` 是 0 / 1 号座位的派生读法,
  87 处调用点读的正是「谁是黑方」,它们的文档写着为什么留着。本变更只让**回放不再用它们**。
- **不碰 `WinnerUserId` 说不清三人牌局的问题。** 斗地主农民赢是两个人赢,而那一格是一个 id。
  它在 `room-and-gameplay`,与那条「点数阶梯」的欠账同一笔。
