# web-lobby 的规格变化

## MODIFIED Requirements

### Requirement: i18n —— `lobby.*` 翻译树同步扩充

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增 `lobby.*` 键集合:

- `lobby.hero.{welcome, online-count-label, online-count-empty}`
- `lobby.rooms.{title, create-button, empty, loading-retry, join, watch, status-waiting, status-playing, status-finished, players, host, spectators, seat-vacant}`

  `seat-vacant` 标的是**空座位圆片**,而它 MUST NOT 叫 `seat-empty` —— 那个名字连同
  `seat-black` / `seat-white` 一起退役了(见下一段)。重用一个退役的键名会让这份规格
  自己的历史读不懂:下一个人读到「seat-empty 被 players 取代」,再在 JSON 里看到它,
  只能自己去推哪个意思是活的。

  `seat-black` / `seat-white` / `seat-empty` 被 `players` 取代:大厅行渲染**在座的玩家**,
  而 MUST NOT 用颜色说话 —— `board-seats.ts` 自己的文档写着那套读法只有棋盘家族可以调,
  而一个座位数大于二的棋种没有颜色可映。

  **钉住这一条的检查 MUST 看属性,不能只看 `textContent`** —— 一个退役的键名完全可以从
  `aria-label` 或 `title` 里溜回来,而那种情况下只查文本的断言是绿的。而它的 fixture
  MUST 真的渲染出一个空位:一条负向断言在「什么都没发生」时恒真。两条都是量出来的 ——
  加强之后的第一次变异仍然判绿,原因是 fixture 用了 3 个在座配一个 2 座位的棋种。
- `lobby.my-rooms.{title, empty, resume, you-are-seated, you-are-spectator}`

  `you-are-black` / `you-are-white` 被 `you-are-seated` 取代,理由同上;而它修掉的是一条
  真缺陷:三座位房间里 2 号座位上的人此前被标成「你是观战」——**在他自己的对局里**。
- `lobby.leaderboard.{title, empty, rank, rating, wins, losses, draws, tier-gold, tier-silver, tier-bronze}`
- `lobby.create-room.{dialog-title, name-label, name-placeholder, submit, submit-loading, cancel}`
- `lobby.create-room.errors.{min-length, max-length, whitespace-only, generic, network}`
- `lobby.errors.{generic, network, retry}`
- `lobby.placeholder.{coming-soon, leave-room, room-not-found, back-to-lobby}`

两份 JSON 的 flattened key 集合 MUST 完全相等。

#### Scenario: 键集合一致
- **WHEN** 对比 flattened 后的 `en.json` 与 `zh-CN.json`
- **THEN** 差集为空

#### Scenario: 模板零硬编码
- **WHEN** 在 `src/app/pages/lobby/**/*.html` 与 `src/app/pages/rooms/**/*.html` 中搜索 CJK 字符或 ≥ 3 字母的显示英文字符串
- **THEN** 0 匹配(技术 test-id 等非展示字符串除外)

## ADDED Requirements

### Requirement: 房间行画座位,而座位总数与在座人数是两个数

Active rooms 的每一行 SHALL 渲染:该棋种的**纹章**、房间名与状态、**一排座位**、观战人数、以及操作按钮。

座位 SHALL 画满**该棋种的座位总数**:在座的画成有人的样子,其余画成空位。座位总数取自 `GET /api/games` 的 `seatCount`,而 **MUST NOT 退化成 `room.seats.length`** —— 后者是「坐上了几个」,前者是「一共有几个」,那句区别写在 `RoomSummary.seats` 的文档里。退化会把每个等待中的房间画成满座,也就是**一个看起来不能加入、其实能加入的房间**。

`seatCount` 是异步的,而大厅列表**没有整页 loading 门**(房间页侧栏有,所以它不必处理这件事)。所以「还不知道有几个座位」SHALL 是一个画得出来的状态:一个占位。它 MUST NOT 先画在座的、等描述符到达再补空位 —— 那是布局跳动。占位 MUST 是 `aria-hidden`:一个还不知道数量的占位被朗读成「空位」,比不朗读更糟,因为它在说一件没被确认的事。

**座位是叠加在名字之上,不是替换名字。** 在座玩家的 username 作为文本存在、并且是 `/users/:id` 链接,这是既有行为(`fix-lobby-seats` 立的),而座位圆片给的是另一件事:「还差不差人」在余光里可读。窄屏(< `sm`)可以把名字那行收起来,但圆片 SHALL 仍然带 `aria-label` 与 `title` 的**全名** —— 视觉上省掉的东西 MUST NOT 在语义上也省掉。

观战人数 MUST NOT 混进座位里:「3/3」读起来会变成「5 个人在玩」。

#### Scenario: 等待中的三人房画三个位子
- **WHEN** 一个 `seatCount == 3` 的房间里坐了 2 个人
- **THEN** 渲染 2 个在座座位与 1 个空位

#### Scenario: 满座房间没有空位
- **WHEN** 一个 `seatCount == 3` 的房间里坐了 3 个人
- **THEN** 渲染 3 个在座座位,空位为 0

#### Scenario: 座位总数未到达时画占位
- **WHEN** 描述符还没到达
- **THEN** 渲染占位而**不是**空位,也**不是**满座;占位 `aria-hidden="true"`

#### Scenario: 圆片只显示一个字,而全名仍然可达
- **WHEN** 渲染一个在座座位
- **THEN** 可见文本是首字,而 `aria-label` / `title` 是完整 username,`href` 指向 `/users/:id`

#### Scenario: 接线由真服务验证
- **WHEN** 用真的 `DefaultGameCapabilitiesService`(只在 HTTP 边界打桩)渲染一行
- **THEN** 描述符到达后空位补齐 —— 一个不调 `ensureLoaded()` 的实现在这条下会红,而在桩下不会
