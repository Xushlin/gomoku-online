## MODIFIED Requirements

### Requirement: `ReplayPage` 初始化时拉取 `GET /api/rooms/{id}/replay`,渲染只读棋盘

`ReplayPage` `ngOnInit` SHALL:

1. 从 `ActivatedRoute.snapshot.paramMap` 读 `id`。
2. 调 `RoomsApiService.getReplay(id)` (新增方法,见 `web-game-board` 修订)。
3. 成功 → 把 `GameReplayDto` 存入本地 `replay = signal<GameReplayDto | null>`,把 `currentPly` 重置为 0。
4. 404 → 渲染翻译后的 `replay.errors.not-found` + 返回大厅链接。
5. 409 → 渲染翻译后的 `replay.errors.still-in-progress` + 链接到 `/rooms/:id` 实时房间页。
6. 其他 → 渲染翻译后的 `replay.errors.generic` + Retry 按钮(再次调 `getReplay`)。

成功状态下,模板 SHALL 引用既存 `Board` 组件并传 `[readonly]="true"`、`[state]="boardState()"`、`[mySide]="'spectator'"`,其中 `boardState` 是一个 `computed` 把 `replay()` + `currentPly()` 合成为一个 `RoomState` 形状对象,`status='Finished'`,`game.moves` 为 `replay.moves.slice(0, currentPly())`。

`boardState.seats` SHALL **直接取 `replay().seats`**,MUST NOT 由两个玩家字段拼出来。
此前那里写的是 `[{index: 0, player: r.black}, {index: 1, player: r.white}]` —— 一份**恒为两条**的
合成物,而它旁边的注释已经写明「三座位棋种的回放要等 `GameReplayDto` 也改说座位」。
现在改了,所以那段合成 SHALL 删掉而不是保留成 fallback:**留着的 fallback 会在座位数为三时
悄悄给出两条**,而症状是牌桌少画一家,不是一个报错。

#### Scenario: 成功获取并初次渲染
- **WHEN** Alice 打开 `/replay/r-1` 且后端返回 200 + `GameReplayDto`(20 步)
- **THEN** `Board` 组件渲染;无落子(currentPly=0);标题区显示对局元信息(房间名、**每个座位**的用户名为链接、`endReason`、`endedAt`)

#### Scenario: 404 处理
- **WHEN** `getReplay` 返回 HTTP 404
- **THEN** 不渲染 `Board`;渲染翻译键 `replay.errors.not-found` + 返回大厅链接;不再发起任何 hub / REST 调用

#### Scenario: 409 处理
- **WHEN** `getReplay` 返回 HTTP 409(`GameNotFinishedException`)
- **THEN** 渲染翻译键 `replay.errors.still-in-progress` + 一个 link `[routerLink]="['/rooms', id]"` 让用户去看实时对局

#### Scenario: 通用错误带 Retry
- **WHEN** `getReplay` 抛出非 404/409 错误(网络 / 500)
- **THEN** 渲染翻译键 `replay.errors.generic` + Retry 按钮;点 Retry 重新调 `getReplay`

---

### Requirement: 标题区元信息使用用户名链接组件

ReplayPage 标题区 SHALL 渲染:

- 房间名(纯文本)
- **`replay().seats` 里的每一个座位**,按 `Index` 升序,**席位名由该棋种的 manifest 给**
  (象棋读作「红方 / 黑方」),username 是
  `<a [routerLink]="['/users', <id>]" class="username-link">`。没声明席位名的棋种说座位号。
- 状态徽章:`endReason` 翻译(`game.ended.reason-connected-5` / `.reason-resigned` / `.reason-timeout`)
- 结束时间(`endedAt`,通过 Angular `formatDate` 按当前 locale 显示)

#### Scenario: 用户名是链接
- **WHEN** 渲染标题区
- **THEN** 每个座位的 username 文本是 `<a>`,`href` 解析为 `/users/<userId>`;有 `username-link` class

#### Scenario: 象棋回放说红黑
- **WHEN** 渲染一局象棋的回放标题区
- **THEN** MUST 说「红方 / 黑方」;MUST NOT 出现「白方」

**座位数 SHALL 由 `seats.length` 决定,MUST NOT 写死两个。**

这一条与上面那条席位名是**两件事,而它们此前各缺一半**。`per-game-seat-labels` 把「怎么称呼」
做对了,但它当时只读得到 `GameReplayDto` 的 `Black` / `White`,所以它自己在实现里写着
「恰好两位,而那是 DTO 的形状,不是这一处的选择」—— 一句诚实的话,也是一张欠条。
`replay-every-seat` 把那个形状改成了座位表,欠条到期。

两个都不能写死:写死名字会把象棋的红方叫成黑方,写死两个则让三座位对局的标题区
**结构上就画不出第三个人**,而页面看起来一切正常。

座位数取 `seats.length` 而不是描述符的 `seatCount`:回放只有 Finished 房间,坐满才开局,
所以在这一页「有几个人」与「有几个座位」是同一个数。房间侧栏取 `seatCount`,因为它面对
等待中的房间,那里两者会分叉 —— **判据不同是因为问题不同,不是漏抄。**

标题区那一行 SHALL 能在长用户名下断行(`break-words`)。这是浏览器里量到的既有缺陷,
与本变更无关但同一处模板:Angular 去掉元素间空白、`mx-1` 是 margin 不是断行机会,于是
「席位名:」+ 20 字符用户名 + 「席位名:」+ 20 字符连成一个没有断点的长串,375 px 下
`scrollWidth 504 / clientWidth 311`。20 字符是注册上限,所以那是真实的最长内容。

#### Scenario: 三座位对局的标题区画三个人
- **WHEN** 回放一局 `seats.length === 3` 的对局(斗地主)
- **THEN** 标题区**恰好**三个 `username-link`,`href` 分别解析到三个不同的 `/users/<id>`

#### Scenario: 两座位对局的标题区画两个人
- **WHEN** 回放一局 `seats.length === 2` 的对局
- **THEN** 标题区**恰好**两个 `username-link`。**这一条与上一条 MUST 同时存在** ——
  「每个座位都画出来了」在一个只有两座位样本的集合上恒真

#### Scenario: 长用户名在 375 px 下不横向溢出
- **WHEN** 三个 20 字符用户名(注册上限)的对局回放,视口 375 px
- **THEN** 页面 `scrollWidth == clientWidth`;标题区那一行 MUST 带 `break-words`

---

### Requirement: 按棋种复用共享棋盘组件的只读模式,回放页不自己写渲染

`ReplayPage` SHALL 通过传 `[readonly]="true"` 给**共享**棋盘组件来实现只读渲染;MUST NOT 在 `pages/replay/` 下复制粘贴任何 board 实现。

共享组件按棋种解析:`gameKey === 'xiangqi'` 用 `<app-xiangqi-board>`,其余(含未知棋种)用 `<app-board>`。选择方式与 `RoomPage` 一致 —— 容器模板里的 `@if`,MUST NOT 引入棋盘组件注册表。

本条原本写作「不引入第二个棋盘渲染层」。那个说法写于平台只有一种盘面形状的时候,而象棋的盘面**不是**五子棋盘的参数化(交叉点上的子 vs 格子里的子、两步走子 vs 一步落子)。约束的**意图**不变:回放页一行渲染代码都不自己写。变的是「共享组件」从单数变成了按棋种解析的两个。

`<app-board>` 的 `rows` / `cols` SHALL 与房间页同源:`GameCapabilitiesService.of(gameKey)`,即 `GET /api/games` 下发的服务端声明,MUST NOT 来自前端清单。描述符未到达时页面停在加载态,理由与房间页相同 —— 见 web-tictactoe「房间页按棋种决定棋盘尺寸」。

`boardState` `computed` SHALL 合成 `RoomState` 形状(synthesised partial)使棋盘组件自然消费 —— `status: 'Finished'` 触发落子按钮永远 disabled,所以 readonly 边界由两层共同保证(`[readonly]` 输入 + `status !== 'Playing'`)。

象棋回放 MUST 从 `MoveDto` 的 `fromRow`/`fromCol` → `row`/`col` 逐步推导盘面(与房间页同一个纯函数),MUST NOT 另写一份推导。

**画不出来的棋种 SHALL 明说,MUST NOT 什么都不画。** 当棋种既没有专用的只读渲染组件、
描述符又声明它没有盘面(`rows` / `cols` 为 `null`,`boardSizeFor` 返回 `null`)时,页面 SHALL
在棋盘的位置渲染一段翻译过的说明,告诉用户这个棋种的回放还画不出来。

这不是补一个空状态的礼节,是**实测**:斗地主的描述符没有 `rows` / `cols`,所以三个 `@if`
分支全部为假,今天那里**一个元素都没有** —— 标题区下面直接是一个能拖的 scrubber,拖动它
不改变任何看得见的东西。一个「拖了没反应」的控件读起来是**功能坏了**,而事实是这一格从来
就没画过。

**牌桌为什么不能顺手接上,理由要写下来:** `CardTable` 的画面全部来自
`state.game.seatView` —— 那是**按座位投影**的视图,由 SignalR 每一步下发,而 `GameReplayDto`
里没有它,也没有那副牌。牌是 `IDealtGameRules.CreateSetup` 生成的服务端侧设置,平台规则写着
它 MUST NOT 出现在任何 DTO 上。所以「牌局回放」要先回答「一局已结束的牌局,底牌该不该公开」——
那是一个**规则问题**,不是接一个组件的问题,本变更不回答它。**拆除条件:** 那个问题有了答案。

#### Scenario: 落子按钮永远禁用
- **WHEN** ReplayPage 渲染任意 currentPly
- **THEN** 棋盘的全部按钮都 `disabled`;点击不触发任何事件

#### Scenario: 最后一步高亮跟着 scrubber
- **WHEN** `currentPly` 从 5 移到 7
- **THEN** 棋盘的 last-move 高亮自动从第 5 步落点移到第 7 步落点(因为 `boardState` 重新合成了 `moves.slice`)

#### Scenario: 象棋回放画象棋盘
- **WHEN** 回放一局 `gameKey === 'xiangqi'` 的对局
- **THEN** 渲染 `<app-xiangqi-board>` 且为只读;MUST NOT 渲染 15×15 的 `<app-board>`

#### Scenario: 象棋回放的盘面随 scrubber 回溯
- **WHEN** `currentPly` 从 7 退回 3
- **THEN** 盘面等于「初始摆子 + 前 3 步」,被吃的子重新出现在盘上

#### Scenario: 五子棋回放不受影响
- **WHEN** 回放一局五子棋
- **THEN** 渲染 `<app-board>`,行为与本变更之前完全一致

#### Scenario: 一字棋回放画 3×3
- **WHEN** 回放一局 `gameKey === 'tictactoe'` 的对局,服务端描述符已到达
- **THEN** 棋盘渲染 9 格
#### Scenario: 画不出盘面的棋种给出说明而不是空白
- **WHEN** 回放一局 `gameKey === 'doudizhu'` 的对局,描述符已到达且 `rows` / `cols` 为 `null`
- **THEN** 棋盘位置渲染一段 `| transloco` 的说明文案;MUST NOT 渲染 `<app-board>`;
  MUST NOT 只留标题区与 scrubber 之间一片空白

#### Scenario: 成语接龙不受影响
- **WHEN** 回放一局 `gameKey === 'idiom-chain'` 的对局(它也没有 `rows` / `cols`)
- **THEN** 渲染 `<app-chain-board>`,**不**渲染那段说明 —— 判据是「有没有专用渲染组件」,
  MUST NOT 只看 `boardSizeFor` 是否为 `null`

