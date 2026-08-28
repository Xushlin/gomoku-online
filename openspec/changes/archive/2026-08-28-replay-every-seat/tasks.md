# tasks — replay-every-seat

## 0. 先量,再改

- [x] 用真的三座位聚合复现一次:`Room.Create(doudizhu)` + 两次 `JoinAsPlayer` + 打到 `Finished`,
      调 `GetGameReplayQueryHandler`。**已经量过一次**(见 proposal),但改之前再跑一次,
      因为下一条要拿它当红灯。
- [x] **正面控制**:同一个夹具里断言 `room.Seats.Count == 3`、`room.CollectUserIds()` 含 2 号座位的人。
      少了这条,「Carol 不在响应里」有可能是**夹具没坐满**而不是 handler 丢人 —— 两者长得一样。

## 1. 契约

- [x] `GameReplayDto`:删 `Black` / `White`,加 `Seats: IReadOnlyList<RoomSeatDto>`。
      **复用既有的 `RoomSeatDto`**,不新造第二种座位形状。
- [x] `GetGameReplayQueryHandler` 从 `room.Seats` 投影,按 `Index` 升序。
      删掉 `var whiteId = room.WhitePlayerId!.Value;`。
- [x] 编译器会列出所有调用点 —— 但「让它编译过」不是「决定每处该干什么」。逐个看,
      不要顺手给个 `Seats[0]` 就算改完。

## 2. 后端测试

- [x] 三座位:`Seats.Count == 3`(**恰好**,不是 `HaveCountGreaterThan(1)`),`Index` 是 `0/1/2`,
      2 号座位的 `Id` 与 `Username` 都对。
- [x] 两座位:五子棋那局 `Seats.Count == 2`。**两支都要在样本里** —— 「每个座位都在」
      在一个只有两座位的样本上恒真,那正是这个缺陷活到今天的原因。
- [x] `Moves[].Seat` ⊆ `Seats[].Index`,且三座位样本里用到的座位号集合**含 `2`**
      (否则这条断言在样本上是空的)。
- [x] **变异**:让 handler 只投影前两个座位 → 三座位那条必须红,两座位那条必须绿。
      变异要是**另一份可信的实现**,不是抛异常 —— 抛出来是「exit 1 没跑测试」,读起来像 kill。

## 3. 前端

- [x] `room.model.ts` 的 `GameReplayDto`:`black` / `white` → `seats: readonly RoomSeat[]`。
- [x] `replay-page.ts` 的 `boardState`:`seats: r.seats`,**删掉**那段
      `[{index: FIRST_SEAT, player: r.black}, ...]` 的合成;`black` / `white` 由座位查出来
      (`RoomState` 上这两个字段是可空的,且没有任何棋盘组件读它们 —— 已 grep 确认)。
- [x] `replay-page.html` 标题区:`@for (seat of r.seats; ...)`,座位数由数据决定。
- [x] 「这个棋种的回放还画不出来」文案 + `zh-CN` / `en` 两份。
      判据是**有没有专用渲染组件**,不是 `boardSizeFor` 是否为 `null` ——
      成语接龙也没有 `rows`/`cols`,但它有 `<app-chain-board>`。

## 4. 前端测试

- [x] 三座位回放:标题区**恰好**三个 `username-link`,三个 `href` 互不相同。
- [x] 两座位回放:**恰好**两个。两支同时存在,理由同 2.2。
- [x] 牌局回放渲染说明文案,**不**渲染 `<app-board>`;成语接龙回放渲染 `<app-chain-board>`,
      **不**渲染说明文案。
- [x] **一条走 DOM 的断言** —— 读整句标题区文本,不是 `toContain` 单个用户名。
      `add-xiangqi-manual` 在这里踩过:标签与值各带一次前缀,拼出来是重复的,
      而每一条 `toContain` 都绿。

## 5. 规格漂移(顺带,理由写在 delta 里)

- [x] `IRoomRepository.GetUserFinishedGamesPagedAsync` 那条:过滤条件由「黑方或白方」
      改成「任一座位」—— **对齐到已发布的代码**,代码没错,规格漂了。
- [x] `GET /api/users/{id}/games` 那条的「用户维度范围」同上。
- [x] `GameReplayDto 携带棋种键` 的「只增字段」Scenario 收窄成 `GameKey` 自己。

## 6. 收口

- [x] `dotnet build Gewu.slnx` + `dotnet test Gewu.slnx` 全绿(**不加 `--no-build`** ——
      失败的构建后面跟一次 `--no-build` 通过,和两次成功长得一模一样)。
- [x] `npm run lint` + `npm run test:ci` 全绿。
- [x] `openspec validate replay-every-seat --strict`。**它只验形状** —— 绿不代表规格是真的。
- [x] 浏览器里看一眼三座位回放:标题区三个人,棋盘位置有说明文案。
      375 px 也看一眼(三个用户名 + 20 字符上限的用户名是这里最长的真实内容)。
      Browser pane 的老规矩:先读 `innerWidth`,`resize_window` 之后**重新加载**再量。

## 7. 量到的(而不是读出来的)

- **改之前**,真三座位聚合跑 handler:`Black=Alice / White=Bob`,`moves=59`,走子里的座位号
  `[0,1,2]`,而 **Carol 的 id 与用户名都不在响应里**,端点 200 成功返回。
- **改之后**,真后端 + 真浏览器:`GET /api/rooms/{id}/replay` 返回
  `seats=[(0,DdzAlice),(1,DdzBob),(2,DdzCarol)]`,响应键里**没有** `black` / `white`,
  每一手的座位号都解析得出人。
- **后端变异**(只投影前两个座位,能编译的另一份实现):三座位那两条红,两座位四条绿。
- **前端变异 A**(标题区 `slice(0,2)`):三座位那两条红。
  **变异 B**(删掉说明文案分支):那一条红,而失败输出里看得见缺陷本身 ——
  标题区下面直接接上 scrubber,中间什么都没有。
- 全量:后端 1577 绿(335 + 157 + 1085),前端 1000 绿 / 84 文件,`npm run lint` 绿。
  对比度读数 **1252 → 1264**(涨了 12,不是掉了)。初始 bundle `410.19 kB`,与改前同。

## 8. 浏览器里发现的一件事,而它不是本变更改出来的

**回放页标题区在 375 px 下横向溢出,20 字符用户名(注册上限)才看得见。**

Angular 去掉元素之间的空白,`mx-1` 给的是 margin 不是断行机会,于是
`Black:WwwwwwwwwwwwwwwwwwwA·White:WwwwwwwwwwwwwwwwwwwB·Opponent` 连成一个**没有断点**的长串:
`scrollWidth 504 / clientWidth 311`。

- **不是本变更引入的** —— 把本次新增的那个分隔符 span 删掉,仍然是 504;而房间页(本变更没碰)
  同样两个用户名下 `scrollWidth == clientWidth == 375`,不溢出。
- **三座位那支恰好躲过了**:标签是「Seat 1」,里面有个空格,于是有断点。所以这条缺陷此前
  只在两座位棋种 + 长用户名下出现,而两座位棋种是默认样本。
- 修法是模板上一个 `break-words`(仓库里已有的写法,另外四处模板在用)。改后浏览器里
  `scrollWidth 311`,两页(两座位五子棋 / 三座位斗地主)在 375 px 都不溢出。
- jsdom 量不了布局,所以 spec 里钉的是**那个类还在**;真正的证据是浏览器读数,写在模板注释里。

**它值得单独说一句:** 这正是「空集合通过一切布局断言」的另一面 —— 短用户名下这条缺陷不存在,
而所有既有测试用的都是短用户名。
