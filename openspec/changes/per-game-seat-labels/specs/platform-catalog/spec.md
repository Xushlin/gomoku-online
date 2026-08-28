## MODIFIED Requirements

### Requirement: `GameManifest` 是游戏的唯一声明形状

一份 `GameManifest` SHALL 声明:

- `key: string` —— 全局唯一的 kebab-case 标识。
- `category: 'match' | 'puzzle' | 'score'`。
- `status: 'available' | 'planned'`。
- `titleKey: string` / `descriptionKey: string` —— Transloco 键,MUST 形如 `games.<key>.title` / `games.<key>.description`。
- `emblem: readonly EmblemShape[]` —— 纹章的形状表,见下条要求。**MUST 非空,连 `planned` 的棋种也是** —— 空表画出来是一张看不见的牌,而看不见的牌不会让任何走查变红。
- `contentLocales: readonly string[]` —— 该游戏**内容**(而非 UI)可用的 locale 列表。
- `launchRoute?: string` —— 仅当 `status === 'available'` 时有意义的入口路由。
- `manualRoute?: string` / `manualLabelKey?: string` —— 这个棋种的古谱入口;填了前者就 MUST 一起填后者。
- `companionRoomKeys?: readonly string[]` —— 这个游戏的大厅还要列出哪些棋种键的房间。
- `seatLabelKeys?: readonly string[]` —— **席位怎么称呼**,按座位号排。

  **不填 = 这个棋种的席位没有名字,界面说座位号。** 缺省是编号而 MUST NOT 是「黑方 / 白方」:
  后者正是它要修的那个失效 —— 象棋房的侧栏因此写着「黑方:<红方玩家>」,而那张盘上
  0 号座位画的是 帥。一个忘了声明的棋种显示「第 1 位」,不好看,但它不把红方叫成黑方。

  填了就 MUST **填满** —— 条数与该棋种的座位数相等。半边有名字半边没有(「黑方 / 白方 /
  第 3 位」)读起来像是第三个人不算玩家,所以条数对不上时整间房 MUST 退回编号。

  它是**文案键**,所以它在清单上而不在服务端描述符上:`board-seats.ts` 已经把这条界线画好了
  ——「座位号 → 棋子颜色是一份显示读法,不是线上格式」。服务端给的是**有几个座位**
  (一个结构性事实),客户端答的是**它们叫什么**。**两者 MUST NOT 由同一个数字回答** ——
  而此前正是同一个数字在回答两个问题。

**这份清单此前漏了三个字段**(`manualRoute` / `manualLabelKey` / `companionRoomKeys`),
它们是在后续变更里加的,而这条「唯一声明形状」没跟上。一份声称自己是「唯一形状」的要求
一旦不全,它就只是一份注释。

不变量:`status === 'available'` 的清单 MUST 提供非空 `launchRoute`;`status === 'planned'` 的清单 MUST NOT 依赖 `launchRoute` 被读取。

**清单 MUST NOT 携带盘面尺寸。** 它此前有一个 `board` 字段,是服务端权威数据的一份刻意副本,当时被接受的理由是「错了会被看见」——格数肉眼可辨,且服务端会挡住越界落子。

**`icon: string` 已被 `emblem` 取代,而 MUST NOT 两者并存。** 那个字段是一个字符(`'⬤'` 一类),而九个棋种呈现为九个字符贴在九张一模一样的牌上,正是「UI 太粗糙、不像游戏」被量出来时的样子:整个大厅只有四个视觉值。留着 `icon` 会让同一件事有两种表示,而**两份表示里必有一份会烂**。

#### Scenario: 每个注册棋种都有非空纹章
- **WHEN** 遍历 `GAME_REGISTRY`
- **THEN** 每份清单的 `emblem` 非空;断言从注册表推导,MUST NOT 手写棋种名单

#### Scenario: 声明了席位名就要填满
- **WHEN** 一个棋种声明了 `seatLabelKeys`
- **THEN** 条数 MUST 等于它的座位数;对不上时该棋种的每一个席位 MUST 说编号,MUST NOT 只给前几个起名

#### Scenario: 声明与不声明两支都要在样本里
- **WHEN** 遍历 `GAME_REGISTRY` 断言席位名
- **THEN** 声明席位名的与不声明的 MUST 都存在 —— 否则两条断言各自都可能在空集合上恒真

