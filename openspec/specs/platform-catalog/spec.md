# platform-catalog Specification

## Purpose
TBD - created by archiving change add-platform-catalog. Update Purpose after archive.
## Requirements
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

#### Scenario: available 游戏必须有入口路由
- **WHEN** 注册表中存在 `status === 'available'` 的清单
- **THEN** 该清单的 `launchRoute` MUST 为非空字符串

#### Scenario: 键命名与清单 key 对齐
- **WHEN** 遍历注册表中每一份清单
- **THEN** `titleKey === 'games.' + key + '.title'` 且 `descriptionKey === 'games.' + key + '.description'`

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

### Requirement: `GameCatalogService` 以抽象类作为 DI token

`src/app/games/game-catalog.service.ts` SHALL 导出抽象类 `GameCatalogService`(DI token)与 `DefaultGameCatalogService`(基于注册表的实现),消费方 MUST 注入抽象类而非具体实现,以便测试替换为 stub。

方法:

- `all(): readonly GameManifest[]` —— 全部清单,可用的排在规划中的之前。
- `available(): readonly GameManifest[]` / `planned(): readonly GameManifest[]`。
- `byKey(key: string): GameManifest | undefined`。

#### Scenario: available 排在 planned 之前
- **WHEN** 调用 `all()`
- **THEN** 所有 `status === 'available'` 的条目下标 MUST 小于任何 `status === 'planned'` 的条目下标

#### Scenario: 按 key 查找
- **WHEN** 以注册表中存在的 key 调用 `byKey()`
- **THEN** 返回对应清单;以不存在的 key 调用时返回 `undefined`

### Requirement: `/games` 是受保护的懒加载游戏目录页

`app.routes.ts` SHALL 新增路由 `games`,带 `canMatch: [authGuard]`,并通过 `loadComponent: () => import(...)` 懒加载 —— 与既有根路由契约一致,MUST NOT 使用 `component:` 直接引用。

未登录用户访问 `/games` MUST 被 `authGuard` 重定向到 `/login?returnUrl=/games`。

本路由的加入 MUST NOT 改变任何既有路由、guard、重定向目标或落地页 —— `/home` 仍是登录后的落地页与五子棋大厅。

#### Scenario: 懒加载
- **WHEN** 已登录用户从 `/home` 导航到 `/games`
- **THEN** 目录页的 JS chunk 在此刻才被请求,MUST NOT 在应用启动时下载

#### Scenario: 未登录被拦
- **WHEN** 未登录用户直接访问 `/games`
- **THEN** 路由落在 `/login?returnUrl=/games`,目录页 chunk MUST NOT 被下载

### Requirement: 目录页为每份清单渲染一张卡片

目录页 SHALL 从 `GameCatalogService.all()` 渲染卡片,每张卡片包含:`icon`、`titleKey` 翻译、`descriptionKey` 翻译、`category` 徽标(`catalog.category-{match,puzzle,score}`)。

- `status === 'available'` 的卡片 SHALL 是导航到 `launchRoute` 的链接。
- `status === 'planned'` 的卡片 SHALL 显示 `catalog.coming-soon` 文案。
- 当活动 locale **不在** `contentLocales` 内时,卡片 SHALL 额外显示 `catalog.chinese-only` 徽标。

模板 MUST NOT 硬编码任何游戏名、描述或状态文案 —— 全部走 Transloco。

#### Scenario: 卡片数等于清单数
- **WHEN** 注入一个含 N 份清单的 stub catalog 并渲染目录页
- **THEN** 页面渲染 N 张卡片

#### Scenario: 可用游戏可点进
- **WHEN** 渲染一份 `status: 'available'`、`launchRoute: '/home'` 的清单
- **THEN** 该卡片是 `href="/home"` 的链接

#### Scenario: 内容语言不匹配时给出提示
- **WHEN** 活动 locale 为 `en`,清单 `contentLocales` 为 `['zh-CN']`
- **THEN** 该卡片显示 `catalog.chinese-only` 对应文案

#### Scenario: 内容语言匹配时不提示
- **WHEN** 活动 locale 为 `zh-CN`,清单 `contentLocales` 包含 `'zh-CN'`
- **THEN** 该卡片 MUST NOT 显示 `catalog.chinese-only` 文案

### Requirement: 规划中的卡片不可交互且对辅助技术明确

`status === 'planned'` 的卡片 MUST NOT 渲染为 `<a>`(不得出现指向空处的 href),也 MUST NOT 渲染为可聚焦的 `<button>`。它 SHALL 是非交互元素并带 `aria-disabled="true"`。

状态 MUST NOT 仅以颜色表达 —— `catalog.coming-soon` 文案本身承载该信息。

#### Scenario: 不是链接
- **WHEN** 渲染一份 `status: 'planned'` 的清单
- **THEN** 该卡片内 MUST NOT 存在 `<a>` 元素

#### Scenario: 对辅助技术标记为不可用
- **WHEN** 渲染一份 `status: 'planned'` 的清单
- **THEN** 该卡片元素带 `aria-disabled="true"`

### Requirement: 目录页响应式基线 375px

目录页 SHALL 在 375px 宽度下单列可用,并通过 Tailwind `sm:` / `lg:` 断点渐进增加列数。页面 MUST NOT 产生横向滚动。

颜色 MUST 全部引用 CSS 变量(`--color-*` / `--radius-*` / `--shadow-*`),MUST NOT 出现字面色值,以保证两套主题 × 深浅色都成立。

#### Scenario: 375px 无横向滚动
- **WHEN** 视口宽 375px 渲染 `/games`
- **THEN** `document.documentElement.scrollWidth <= document.documentElement.clientWidth`

### Requirement: Header 提供目录入口

`src/app/shell/header/header.html` SHALL 新增一个指向 `/games` 的链接,文案走 `catalog.title`,位置在语言切换器之前。

#### Scenario: 入口可达
- **WHEN** shell 渲染完成
- **THEN** header 中存在 `href="/games"` 的链接

### Requirement: i18n —— `catalog.*` 与 `games.*` 双语对齐

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增:

- `catalog.{title, subtitle, coming-soon, chinese-only, category-match, category-puzzle, category-score}`
- `games.<key>.{title, description}`,`<key>` 覆盖注册表中全部游戏

flatten 后两份 JSON 的 key 集合 MUST 完全相等(零漂移)。

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空

#### Scenario: 每个游戏都有双语文案
- **WHEN** 遍历注册表中每一份清单
- **THEN** 两份 JSON 中均存在 `games.<key>.title` 与 `games.<key>.description`

