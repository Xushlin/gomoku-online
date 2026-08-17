## Why

象棋今天只能打机器人。`XiangqiRules.SupportsHumanVsHuman => false` 的文档注释写下了这次变更,并点名了它的触发条件:

> 今天没有人人对战入口 —— 平台还没有进入象棋对局的任何途径。这是**结构性事实**,不是判断:**大厅泛化之后翻它**,而计不计分是那时一个独立的、需要理由的决定。

`generalize-lobby` 已归档,触发条件到了。这是这个仓库第三个**自己写下触发条件**的推迟(前两个:`generalize-match-domain` 的 JSON 列、`web-lobby` 的 AI 卡片),而这一条是三个里最干净的 —— 它连"翻它的时候还要另外决定什么"都写了。

### 围观与围观评论**已经存在**,这次变更不新建它

这一点必须先说清楚,因为它决定了这次变更的真实大小。对战内核早就有整套围观机制:

| 部件 | 位置 |
| --- | --- |
| `ChatChannel { Room=0, Spectator=1 }` | Domain |
| `Room.PostChatMessage` 的成员/频道校验(玩家发不了围观频道) | Domain,有测试 |
| `Room.JoinAsSpectator` —— 幂等、无人数上限、玩家不能围观自己的局 | Domain |
| `IRoomNotifier` 按频道分发到 `room:{id}:spectators` 子群 | Application |
| `POST /api/rooms/{id}/spectate` | Api |
| 大厅 `Playing` 房间行上的「围观」按钮 → `spectate` → 进房 | Web |
| `ChatPanel` 双频道不对称可见性 | Web |

所以「对战类的都可以加上围观评论」的准确说法是:**已经加上了,而且是内核级的 —— 它不点名任何棋种。** 象棋够不到它的唯一原因是象棋没有真人房,于是它的大厅显示「目前只有人机对战」,围观按钮所在的房间列表根本不渲染。

**换句话说:给象棋开人人对战,围观和多人评论是免费到位的。** 这次变更要做的不是建它,而是第一次**真的去验它** —— 这条路径从未被真人跑过。五子棋是此前唯一有真人房的棋种,而围观从来只有单元测试,没有人在浏览器里以两名观众的身份评论过一局。

## What Changes

### 两个 flag,两条不同性质的理由

`SupportsHumanVsHuman => true` 是**推论**:平台现在有大厅、有建房、有加入,象棋走的是同一个 `Room` 聚合。`enforce-human-vs-human` 立下的定义是「只要 `POST /api/rooms` 接受某个棋种,平台就确实提供了入口」——反过来也成立,入口存在了,声明就得跟上。

`IsRated => true` 是**判断**,所以它需要一个写下来的理由,而这正是原注释预告的那个决定。理由:象棋此前不计分的**唯一**依据是「没有对手池,阶梯量不出棋力」,而这次变更消灭了那条依据。剩下的形状与五子棋逐项相同 —— 有真人对手池、也有 AI,而机器人对局计分是 `ai-opponent` D7 的反套利规则,不是漏洞。

对照一字棋,它**不动**:3×3 是已解棋,双方不犯错必平,真人对战没有可下的东西;而且它不计分的依据是「唯一对手是机器人」,开了真人房反而要重新论证。所以本变更之后,注册表里仍然**两类都有** —— `The_verdict_tracks_the_capability_across_the_whole_registry` 那条「两种结果都要出现过」的断言继续有效,不会退化成只走一边的空转。

### 四处把「象棋没有真人对战」当成正确断言钉住了

这是本变更最需要小心的部分,而理由是 `enforce-human-vs-human` 刚付过的账:那次的 `CreateRoomGameKeyValidationTests` 同时断言了洞的两半 —— 一半是洞本身、一半自 `add-xiangqi` 起就是假的 —— 而它一直是绿的。**一条把过期事实钉成正确的断言,比没有断言更难发现。**

逐一处理:

1. `xiangqi` spec 的要求标题就是**「象棋今天不计分,因为它还没有对手」**。改完之后这个标题是假的,所以走 RENAMED,不是留着改内容。
2. `room-and-gameplay` 的场景「已登记但无人人对战的棋种建真人房被拒」以 `xiangqi` 举例,并附了一句「象棋自 `add-xiangqi` 起就已登记,任何仍以它举例"未登记"的场景都是过期的」。那句话本身仍然对,但**这条场景现在需要一字棋来举例**,否则它断言的行为不再存在。
3. `web-lobby` 的场景「只有人机的棋种 → 访问 `/g/xiangqi/lobby` → 显示'目前只有人机对战'」同样换成一字棋(链接指向 `/g/tictactoe`)。
4. `CreateRoomGameKeyValidationTests` 的 `[InlineData(GameKeys.Xiangqi)]` 从「无真人对战被拒真人房」那条 Theory 移出,并加一条正向:象棋现在开得出真人房。

### manifest 的入口改成大厅

`launchRoute` 从 `/g/xiangqi`(人机页)改成 `/g/xiangqi/lobby`,与五子棋一致。`gameEntryRoute` 读的就是这个字段,所以离开象棋房间会回到象棋大厅 —— 那是"再来一局"该去的地方。

`/g/xiangqi` 人机页**保留**:它仍然是人机入口,而大厅上的「人机对战」卡片(象棋 `supportsAi: true`)是第二个。这会让象棋落到与五子棋同样的状态 —— 两个入口通往同一件事 —— 而那是 `leaderboard-page` 已经记下的既有瑕疵,本变更不扩大也不修它。

### 计分带来的东西是自动的

阶梯页 `/g/xiangqi/leaderboard` 不需要新代码:`game-lobby` 按 `descriptor.isRated` 渲染榜卡,目录页按同一个字段给阶梯链接,而 `GET /api/games` 投影的就是 `IGameRules.IsRated`。这正是 `add-web-per-game-rating` 拒绝在前端放一份 `rated` 副本换来的东西 —— 服务端翻一个布尔,客户端跟着变,没有第二处要改。

有一条现存的目录测试断言「一字棋没有阶梯链接」,那是那份副本不许爬回来的可执行形式。它继续有效,而且现在更有价值:一字棋仍然是唯一不计分的对战棋种。

## Impact

- 受影响 spec:`xiangqi`(RENAMED + MODIFIED)、`room-and-gameplay`、`web-lobby`、`web-xiangqi`、`in-room-chat`(把围观评论的内核级适用范围写明,并加多观众场景)。
- 受影响代码:`XiangqiRules` 两个属性、象棋 manifest、`CreateRoomGameKeyValidationTests`。
- **围观机制零改动。** 这次是第一次验它。
- 一字棋不动,故意的。
- 无迁移:`UserGameStats` 按 `(UserId, GameKey)` 惰性建行,象棋第一局结束时自然出现第一行。
