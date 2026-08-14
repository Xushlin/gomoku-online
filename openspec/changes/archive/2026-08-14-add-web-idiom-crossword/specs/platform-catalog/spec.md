## MODIFIED Requirements

### Requirement: `src/app/games/index.ts` 是唯一注册点

`src/app/games/index.ts` SHALL 导出一个 `GameManifest` 数组,作为平台的全部游戏来源。新增一个游戏 MUST 只需要:新建 `src/app/games/<key>/` 目录、在本文件数组中增加一个条目、在两份 i18n JSON 中增加 `games.<key>.*` 键。

新增游戏 MUST NOT 需要修改目录页组件、`GameCatalogService`、或任何既有游戏的文件。

注册表 MUST 包含平台规划中的全部游戏,未实现的以 `status: 'planned'` 声明 —— 目录页因此从第一天起就展示平台的完整形状。

一个游戏从"规划中"变为"可玩",MUST 只需要改动它自己 manifest 里的 `status` 与 `launchRoute` 两个字段 —— 这是 `add-platform-catalog` 承诺的机制,由 成语纵横 第一次真正兑现。

#### Scenario: key 唯一
- **WHEN** 读取注册表
- **THEN** 所有清单的 `key` 互不重复

#### Scenario: 五子棋已可用
- **WHEN** 读取注册表
- **THEN** 存在 `key === 'gomoku'` 且 `status === 'available'` 的清单,`category === 'match'`

#### Scenario: 成语纵横已可用
- **WHEN** 读取注册表
- **THEN** 存在 `key === 'idiom-crossword'` 且 `status === 'available'` 的清单,`category === 'puzzle'`,`launchRoute === '/g/idiom-crossword'`,且 `contentLocales` 为 `['zh-CN']`

#### Scenario: 状态翻转只动自己的 manifest
- **WHEN** 比对 成语纵横 上线前后的 diff
- **THEN** `src/app/games/` 下除 `idiom-crossword/` 以外的文件 MUST NOT 被修改;`index.ts` 的条目顺序可变,但其它游戏的 manifest 内容不变
