## ADDED Requirements

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

**它 MUST 是一个独立的 service,MUST NOT 并入 `GameCatalogService`。** 提案里写的是"合并进
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
