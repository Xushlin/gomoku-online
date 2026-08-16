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

### Requirement: `GamesApiService` 拉取服务端的棋种能力

Web 客户端 SHALL 提供 `GamesApiService`(抽象类作为 DI token + 默认实现),对 `GET /api/games` 发一次请求,返回 `GameDescriptor[]`。

```
export interface GameDescriptor {
  readonly gameKey: string;
  readonly isRated: boolean;
  readonly supportsHumanVsHuman: boolean;
  readonly rows: number;
  readonly cols: number;
}
```

组件 MUST 注入抽象 token,MUST NOT 注入默认实现 —— 与既有四个 API service 同一形状。

#### Scenario: 抽象 token
- **WHEN** 审阅任何消费它的组件
- **THEN** 注入的是抽象类,测试可以替换成 stub

### Requirement: `GameCapabilitiesService` 独立于 `GameCatalogService`,按 key 提供服务端能力

Web 客户端 SHALL 提供 `GameCapabilitiesService`(抽象类 DI token + 默认实现),一次性拉取 `GamesApiService` 的结果并按 `gameKey` 提供查询:`ensureLoaded()` / `of(key)` / `ratedKeys()` / `loaded()`。

**它 MUST 是一个独立的 service,MUST NOT 并入 `GameCatalogService`。** `add-web-per-game-rating` 的提案里写的是"合并进
`GameCatalogService`",实现时发现那是错的:目录服务读的是静态 import —— 同步、不会失败、不会为空,
而好几个组件与它们的 spec 都依赖这一点。为了两个布尔把它变成异步的,就要把 loading / error 状态
推进每一个消费者。

于是两层分开、在调用点组合:**manifest 说"有哪些游戏、怎么进去",本 service 说"服务端允许它们做
什么"。**

一个键没有描述符表示**"不适用"**,而不是 `false`。MUST NOT 用 `false` / `0` 之类的缺省值填 ——
谜题类根本没有 `IGameRules`,把它折叠成 `isRated: false` 会让"一字棋不计分"和"成语纵横不是对战
游戏"再也分不开。

`GAME_REGISTRY`(manifest 清单)**仍然是唯一的注册点**,并且仍然并排列出三个类别。

加载失败时 MUST 退化为"全部不适用" —— 于是没有排行榜入口、没有棋种切换,即本变更之前的界面。
**失败要退化成少一个入口,而不是退化成一个错的入口**(比如一个指向空榜的链接)。

#### Scenario: 对战棋种查得到
- **WHEN** 服务端返回 `gomoku` 的能力
- **THEN** `of('gomoku')?.isRated === true`,且 `ratedKeys()` 含 `gomoku`

#### Scenario: 谜题游戏没有能力信息
- **WHEN** 查询 `idiom-crossword` 的能力
- **THEN** 结果为 `undefined`,MUST NOT 是一个 `isRated: false` 的对象

#### Scenario: 规划中的游戏没有能力信息
- **WHEN** 查询尚未在服务端登记的 `xiangqi`
- **THEN** 同样是 `undefined`

#### Scenario: 只拉一次
- **WHEN** 多个组件各调一次 `ensureLoaded()`
- **THEN** MUST 只发出一次 `GET /api/games`

#### Scenario: 失败退化为少一个入口
- **WHEN** `GET /api/games` 失败
- **THEN** `of(...)` 全部返回 `undefined`、`ratedKeys()` 为空;界面 MUST NOT 出现任何排行榜入口或棋种切换器

### Requirement: 目录卡片为计分的可玩棋种提供排行榜入口

`/games` 目录页 SHALL 为同时满足 `status === 'available'` 与服务端 `isRated === true` 的游戏卡片渲染一个次级入口"排行榜",指向 `/g/<key>/leaderboard`。

不满足的卡片 MUST NOT 渲染这个入口。具体地:

- **一字棋 MUST NOT 有**(`isRated === false`)。这条要有测试 —— 它是"为什么用服务端投影而不是
  manifest 上一个布尔副本"那份论证的唯一可执行形式。测试挂掉,就说明那份副本又爬回来了。
- **谜题类 MUST NOT 有**(没有能力信息,不适用)。
- **规划中的游戏 MUST NOT 有**(卡片本身就不可交互)。
- **能力尚未加载 / 加载失败时一个都 MUST NOT 有**(退化成本变更之前的界面)。

入口是**次级**的:主入口仍然是"开始游戏"。

可玩卡片的标记因此 MUST 从"整张卡是一个 `<a>`"改为"卡片是容器,启动链接靠伸展的伪元素
(`after:inset-0`)覆盖整张卡"。**`<a>` 里套 `<a>` 是非法 HTML**,浏览器会把它拆开,键盘顺序
和屏幕阅读器都会坏掉 —— 所以两个链接不能嵌套。整张卡片仍然可点,排行榜入口靠更高的
`z-index` 赢得重叠区域。

#### Scenario: 五子棋卡片有榜入口
- **WHEN** 目录页渲染,服务端说 `gomoku` 计分
- **THEN** 该卡片有一个指向 `/g/gomoku/leaderboard` 的次级入口

#### Scenario: 一字棋卡片没有
- **WHEN** 目录页渲染,服务端说 `tictactoe` 不计分
- **THEN** 该卡片 MUST NOT 出现排行榜入口

#### Scenario: 成语纵横没有
- **WHEN** 目录页渲染一张谜题卡片
- **THEN** 它 MUST NOT 出现排行榜入口 —— 谜题阶梯是星数 + 用时,不是 ELO

#### Scenario: 点榜入口不触发卡片导航
- **WHEN** 点击排行榜入口
- **THEN** 导航到榜页,MUST NOT 同时触发"开始游戏"

