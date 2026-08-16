## MODIFIED Requirements

### Requirement: `GameManifest` 是游戏的唯一声明形状

`src/app/games/game-manifest.ts` SHALL 导出类型 `GameManifest`,字段如下:

- `key: string` —— 全局唯一的 kebab-case 游戏标识(如 `gomoku`、`idiom-crossword`)。
- `category: 'match' | 'puzzle' | 'score'` —— 回合对抗 / 单人关卡 / 单人计分。
- `status: 'available' | 'planned'`。
- `titleKey: string` / `descriptionKey: string` —— Transloco 键,MUST 形如 `games.<key>.title` / `games.<key>.description`。
- `icon: string` —— 卡片图标(当前为字符/emoji 形式的字符串)。
- `contentLocales: readonly string[]` —— 该游戏**内容**(而非 UI)可用的 locale 列表。
- `launchRoute?: string` —— 仅当 `status === 'available'` 时有意义的入口路由。

不变量:`status === 'available'` 的清单 MUST 提供非空 `launchRoute`;`status === 'planned'` 的清单 MUST NOT 依赖 `launchRoute` 被读取。

**清单 MUST NOT 携带盘面尺寸。** 它此前有一个 `board` 字段,是服务端权威数据的一份刻意副本,当时被接受的理由是「错了会被看见」——格数肉眼可辨,且服务端会挡住越界落子。

那个理由后来不成立了:`add-web-xiangqi` 给象棋填了 `{ rows: 10, cols: 9 }`,而象棋的棋盘组件**硬编码自己的 10×9**(交叉点上的盘不是格子盘的参数化),于是那份副本**没有任何读者** —— 它错了永远不会被任何人发现,正是本仓库判据要挡的那种副本。它当时还活着,只因为一条测试要求每个可玩的对战棋种都声明它。

尺寸的真源在服务端,并且 `add-web-per-game-rating` 起就已经在线上:`GET /api/games` 的描述符带 `rows` / `cols`,由 `GameCapabilitiesService` 缓存。清单说的是**有哪些游戏、怎么进去**,服务端说的是**它们能做什么**,盘面属于后者。

#### Scenario: available 游戏必须有入口路由
- **WHEN** 注册表中存在 `status === 'available'` 的清单
- **THEN** 该清单的 `launchRoute` MUST 为非空字符串

#### Scenario: 键命名与清单 key 对齐
- **WHEN** 遍历注册表中每一份清单
- **THEN** `titleKey === 'games.' + key + '.title'` 且 `descriptionKey === 'games.' + key + '.description'`

#### Scenario: 清单里没有盘面字段
- **WHEN** 遍历注册表中每一份清单
- **THEN** 其中 MUST NOT 存在 `board` 属性 —— 尺寸只来自 `GET /api/games`
