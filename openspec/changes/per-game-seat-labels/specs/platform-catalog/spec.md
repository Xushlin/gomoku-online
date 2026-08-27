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
- `seatLabelKeys?: readonly string[]` —— 这个棋种的**席位怎么称呼**,按座位号排。
  填了就用它(五子棋「黑方 / 白方」、象棋族「红方 / 黑方」、成语接龙「先手 / 后手」);
  不填表示这个棋种的席位**没有名字**,界面说座位号。
- `companionRoomKeys?: readonly string[]` —— 这个游戏的大厅还要列出哪些棋种键的房间。
  **本条此前漏了它**:`play-from-position` 加了这个字段,却把它记在需要它的那条*行为*
  要求下(`room-and-gameplay` 的「从选定局面开的房间要**找得到**」),而这条自称
  「唯一声明形状」的要求没跟上。一份声明了 N 个字段却只列出 N-1 个的形状要求,
  下一个人读它就会漏掉一个。

不变量:`status === 'available'` 的清单 MUST 提供非空 `launchRoute`;`status === 'planned'` 的清单 MUST NOT 依赖 `launchRoute` 被读取。

**`seatLabelKeys` 的缺省 MUST 是「说座位号」,MUST NOT 是「黑方 / 白方」。** 一个忘了
声明的棋种因此显示「第 1 位」—— 不好看,但它不会把一个红方叫成黑方。这条不变量是
本变更的全部要点:**旧的缺省本身就是那个失效**,而它在浏览器里的样子是象棋房的侧栏
写着「黑方:红方玩家」。

**清单 MUST NOT 携带盘面尺寸。** 它此前有一个 `board` 字段,是服务端权威数据的一份刻意副本,当时被接受的理由是「错了会被看见」——格数肉眼可辨,且服务端会挡住越界落子。

**`icon: string` 已被 `emblem` 取代,而 MUST NOT 两者并存。** 那个字段是一个字符(`'⬤'` 一类),而九个棋种呈现为九个字符贴在九张一模一样的牌上,正是「UI 太粗糙、不像游戏」被量出来时的样子:整个大厅只有四个视觉值。留着 `icon` 会让同一件事有两种表示,而**两份表示里必有一份会烂**。

#### Scenario: 每个注册棋种都有非空纹章
- **WHEN** 遍历 `GAME_REGISTRY`
- **THEN** 每份清单的 `emblem` 非空;断言从注册表推导,MUST NOT 手写棋种名单

#### Scenario: 声明了的席位名两份 locale 都有文案
- **WHEN** 遍历 `GAME_REGISTRY` 里每一个 `seatLabelKeys` 的每一个键
- **THEN** 两份 locale 中都存在且非空 —— 一个没有文案的键会把键名本身画到界面上

#### Scenario: 席位名的走查两种答案都要在样本里
- **WHEN** 遍历 `GAME_REGISTRY`
- **THEN** 声明了席位名的棋种与没声明的 MUST 都**至少各有一个**;且不同的席位名组合
  MUST 有**恰好三种**(黑白 / 红黑 / 先手后手)—— 「恰好」在第四种出现那天会红,
  而那正是该问「这个棋种的席位真的没有名字吗」的时刻

### Requirement: `GameCatalogService` 以抽象类作为 DI token

`src/app/games/game-catalog.service.ts` SHALL 导出抽象类 `GameCatalogService`(DI token)与 `DefaultGameCatalogService`(基于注册表的实现),消费方 MUST 注入抽象类而非具体实现,以便测试替换为 stub。

方法:

- `all(): readonly GameManifest[]` —— 全部清单,可用的排在规划中的之前。
- `available(): readonly GameManifest[]` / `planned(): readonly GameManifest[]`。
- `byKey(key: string): GameManifest | undefined`。
- `byRoomKey(key: string): GameManifest | undefined` —— 按**房间的棋种键**解析,
  匹配清单自己的 `key` **或**它声明的任一 `companionRoomKeys`。

  它与 `byKey` 不能合成一个:`byKey('xiangqi-endgame')` MUST 是 `undefined`
  (残局没有自己的清单,它不该出现在目录页上),而 `byRoomKey('xiangqi-endgame')`
  MUST 给出象棋那份 —— **一间残局房要画象棋的纹章、用象棋的席位名。**

  它同时收拢一处已经存在的重复:大厅的房间行此前自己拼了一张「伴生键 → 主棋种」的表,
  而一份被复制的解析规则迟早与另一份不一致。

#### Scenario: available 排在 planned 之前
- **WHEN** 调用 `all()`
- **THEN** 所有 `status === 'available'` 的条目下标 MUST 小于任何 `status === 'planned'` 的条目下标

#### Scenario: 按 key 查找
- **WHEN** 以注册表中存在的 key 调用 `byKey()`
- **THEN** 返回对应清单;以不存在的 key 调用时返回 `undefined`

