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
- `board?: { readonly rows: number; readonly cols: number }` —— 盘面格数,只对 `category: 'match'` 有意义。

不变量:`status === 'available'` 的清单 MUST 提供非空 `launchRoute`;`status === 'planned'` 的清单 MUST NOT 依赖 `launchRoute` 被读取。

`board` 是**服务端权威数据在客户端的一份刻意副本**。真源是后端的 `IGameRules`,本字段存在的
唯一理由是房间 DTO 不下发尺寸(见 `room-and-gameplay` 的"房间 DTO 携带棋种键")。
失配的代价被两件事限住:症状是棋盘格数肉眼可见地不对,而不是潜伏的错;且服务端
`rules.IsInBounds` 会挡住越界落子,所以一个画大了的客户端棋盘点下去只会拿到错误,
不会写坏对局。`generalize-match-contract` 改为服务端下发尺寸后,本字段 MUST 被删除。

`category: 'match'` 的清单在 `status === 'available'` 时 MUST 提供 `board`;
`category` 为 `'puzzle'` / `'score'` 的清单 MUST NOT 依赖它被读取。

#### Scenario: available 游戏必须有入口路由
- **WHEN** 注册表中存在 `status === 'available'` 的清单
- **THEN** 该清单的 `launchRoute` MUST 为非空字符串

#### Scenario: 键命名与清单 key 对齐
- **WHEN** 遍历注册表中每一份清单
- **THEN** `titleKey === 'games.' + key + '.title'` 且 `descriptionKey === 'games.' + key + '.description'`

#### Scenario: 可玩的对战棋种必须声明盘面
- **WHEN** 遍历注册表中 `category === 'match'` 且 `status === 'available'` 的清单
- **THEN** 每一份的 `board.rows` 与 `board.cols` MUST 为正整数

#### Scenario: 盘面与后端注册一致
- **WHEN** 读取 `gomoku` 与 `tictactoe` 的 `board`
- **THEN** 分别为 `{ rows: 15, cols: 15 }` 与 `{ rows: 3, cols: 3 }`,与后端 `BuiltInGameRules` 的注册参数相同

### Requirement: `src/app/games/index.ts` 是唯一注册点

`src/app/games/index.ts` SHALL 导出一个 `GameManifest` 数组,作为平台的全部游戏来源。新增一个游戏 MUST 只需要:新建 `src/app/games/<key>/` 目录、在本文件数组中增加一个条目、在两份 i18n JSON 中增加 `games.<key>.*` 键。

新增游戏 MUST NOT 需要修改目录页组件、`GameCatalogService`、或任何既有游戏的文件。

注册表 MUST 包含平台规划中的全部游戏,未实现的以 `status: 'planned'` 声明 —— 目录页因此从第一天起就展示平台的完整形状。

一个游戏从"规划中"变为"可玩",MUST 只需要改动它自己 manifest 里的 `status` 与 `launchRoute` 两个字段(对战棋种再加 `board`)—— 这是 `add-platform-catalog` 承诺的机制,由 成语纵横 第一次真正兑现,一字棋第二次。

#### Scenario: key 唯一
- **WHEN** 读取注册表
- **THEN** 所有清单的 `key` 互不重复

#### Scenario: 五子棋已可用
- **WHEN** 读取注册表
- **THEN** 存在 `key === 'gomoku'` 且 `status === 'available'` 的清单,`category === 'match'`

#### Scenario: 成语纵横已可用
- **WHEN** 读取注册表
- **THEN** 存在 `key === 'idiom-crossword'` 且 `status === 'available'` 的清单,`category === 'puzzle'`,`launchRoute === '/g/idiom-crossword'`,且 `contentLocales` 为 `['zh-CN']`

#### Scenario: 一字棋已可用
- **WHEN** 读取注册表
- **THEN** 存在 `key === 'tictactoe'` 且 `status === 'available'` 的清单,`category === 'match'`,`launchRoute === '/g/tictactoe'`,`board === { rows: 3, cols: 3 }`

#### Scenario: 状态翻转只动自己的 manifest
- **WHEN** 比对 一字棋 上线前后 `src/app/games/` 下的 diff
- **THEN** 除 `tictactoe/manifest.ts` 与 `game-manifest.ts`(新增 `board` 字段本身)以外,其它游戏的 manifest 内容 MUST NOT 被修改
