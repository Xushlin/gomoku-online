# Tasks — enable-xiangqi-human-play

## 1. 两个 flag

- [x] 1.1 `XiangqiRules.SupportsHumanVsHuman => true` —— 注释说明它是**推论**:入口存在了,声明跟上。
- [x] 1.2 `XiangqiRules.IsRated => true` —— 注释写下**判断**的理由:此前不计分的唯一依据
      (没有对手池)被本变更消灭,剩下的形状与五子棋逐项相同。
- [x] 1.3 一字棋不动,并在它的注释里写明它现在是注册表里唯一 `false` 的那个,
      以及那件事对好几条"两类都要出现过"的遍历断言意味着什么。

## 2. 把「象棋没有真人对战」钉成正确的地方 —— 提案说四处,**实测七处**

- [x] 2.1 `xiangqi` spec 的要求标题走 RENAMED(旧标题「象棋今天不计分,因为它还没有对手」已是假的)。
- [x] 2.2 `room-and-gameplay`:例子换一字棋 + 加一条象棋 201。
- [x] 2.3 `web-lobby`:例子换一字棋 + 加一条象棋渲染完整大厅。
- [x] 2.4 `CreateRoomGameKeyValidationTests`:象棋移出「无真人对战被拒」的 Theory + 两条正向。
- [x] 2.5 `game-entry-route.spec.ts`:「没有大厅的游戏」不再包含象棋。**提案漏了这处。**
- [x] 2.6 `registry.spec.ts`:「象棋在自己的路由上可玩」改成大厅路由。**提案漏了这处。**
- [x] 2.7 `room-page.spec.ts`:「离开没有大厅的房间」换一字棋 + 加一条象棋回大厅。**提案漏了这处。**

后三处是跑测试跑出来的,不是读代码读出来的 —— 提案里"四处"这个数是查 spec 得到的,而
前端测试里还有三处。**清点靠搜索得到的数字,和清点靠红灯得到的数字,不是一回事。**

## 3. 入口

- [x] 3.1 象棋 manifest `launchRoute` → `/g/xiangqi/lobby`。
- [x] 3.2 `/g/xiangqi` 人机页保留。
- [x] 3.3 `web-xiangqi` spec 跟上,并顺手改正 `board: {...}` 那处遗留漂移。

## 4. 围观

- [x] 4.1 新增 `in-room-chat` 要求把「围观是内核能力、不分棋种」写明,零代码改动。
- [x] 4.2 `SpectatorsAcrossGamesTests`:遍历每个开放人人对战的棋种,验多观众评论、玩家发不了
      围观频道、房间频道共享、幂等、无人数上限,外加一条**源码断言**:
      `JoinAsSpectator` / `LeaveAsSpectator` / `PostChatMessage` 三段里不出现 `GameKey`。

## 5. 测试

- [x] 5.1–5.3 象棋真人房、ELO 结算、描述符点名断言(象棋计分 + 一字棋是唯一不计分的对战棋种)。
- [x] 5.4 `dotnet build` 0 warning;`dotnet test` **955** 全绿(239 + 84 + 634);
      `npm run lint` 通过;`npm run test:ci` **505** 全绿。

## 6. 实测

### 真人对真人的象棋 —— 平台第一次

`POST /api/rooms { gameKey: "xiangqi" }` 返回 **201**(改之前是 400)。白方加入,两名观众围观。
第一手**从浏览器的真实棋盘点出去的**(红兵 (6,0)→(5,0)),白方走真实 SignalR 连接回一手
(马 (0,1)→(2,2))。

```
status: Playing | turn: Black | spectators: 2
  1 Black (6,0) -> (5,0)     ← 浏览器
  2 White (0,1) -> (2,2)     ← 脚本
```

### 大厅

`/g/xiangqi/lobby` 完整渲染:活跃房间(含那个真人房,`Spectators: 2`)、Watch 按钮、
Play vs AI 卡、**Top players 排行榜卡**。改之前这个页面只有一句「目前只有人机对战」。

白方认输后阶梯立刻有名次,**零新代码**:

```
GET /api/leaderboard?gameKey=xiangqi -> 200
   #1 xqred    rating=1220  1-0-0
   #2 xqwhite  rating=1180  0-1-0
```

一字棋大厅仍然显示「Against the computer only」+ 无房间列表 —— 它现在是那条路径唯一的真实用例。

375 px:`scrollWidth == clientWidth == 375`,象棋盘渲染,聊天面板里**有真实消息**
(`Room xqwhite:承让`)。

### 多名观众评论 —— 能用

两个不同的真账号各发一条围观评论,都成功;玩家发房间频道成功;
玩家试发围观频道被拒,码是 `spectator-channel-forbidden`。

## 7. 验证验出一个**先于本变更存在的授权缺陷**

提案说「围观机制零改动,这次是第一次真的去验它」。验出来它坏了一半。

`in-room-chat` 的规则是明确的:「`Spectator` 频道**仅围观者**可见(玩家看不到围观者吐槽)」。
**写**的那一侧对(刚验证);**读**的那一侧有两条路都泄漏:

| 路径 | 结果 |
| --- | --- |
| `ChatMessage` 实时事件 | **正确** —— `ChatMessagePostedAsync` 按频道选 group,围观消息只进 `room:{id}:spectators` |
| `GET /api/rooms/{id}` 快照 | **泄漏** —— `ToState` 原样返回全部 `chatMessages`,不看调用者是谁 |
| `RoomState` SignalR 广播 | **泄漏** —— 同一份 DTO 推给整个 `room:{id}` group(玩家 + 围观者) |

两条都是量出来的,不是读代码推断的:

```
red    (player)    sees 3: [Spectator]xqfan1:红方这步兵进得好 | [Spectator]xqfan2:我押黑方赢 | [Room]xqwhite:承让
white  (player)    sees 3: (同上)

以**玩家**身份收到 2 个 RoomState 广播帧
其中围观频道消息条数: 2
   泄漏 → xqfan1 : 红方这步兵进得好
   泄漏 → xqfan2 : 我押黑方赢
```

**一条路做对了,而那正是另外两条一直没被发现的原因。** 屏幕上什么都看不出来:
`ChatPanel` 用 `@if (isSpectator())` 藏掉了围观 Tab,所以玩家的 UI 是干净的 ——
数据早就在他的客户端里,打开 DevTools 就能读对手的围观区在说什么。

而这个仓库自己写过反面原则(`add-hub-error-codes`):

> 负载只放码而不附带消息,是为了让「展示服务端英文」这件事**做不到**,而不是靠自觉不做。

这里靠的恰好是自觉。

**它不是本变更引入的** —— 自 `in-room-chat` 起就在,五子棋从第一天就能复现。象棋没有让它可达,
只是让它被发现:复现需要「真人对战 + 围观」同时存在,而在今天之前没有人凑出过这个组合。

**不在本变更里修**,理由是范围而不是优先级:修它要给 REST 快照按调用者裁剪、并把
`RoomState` 广播拆成分群两份(今天没有"仅玩家"的 group),那会动到 `web-game-board`
的重连协议。它符合仓库规矩里的「纯 bug fix:代码不符合既有 spec,直接修,不需要新提案」,
紧接本变更单独交付。
