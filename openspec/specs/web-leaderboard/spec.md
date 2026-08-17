# web-leaderboard Specification

## Purpose
TBD - created by archiving change add-web-per-game-rating. Update Purpose after archive.
## Requirements
### Requirement: `/g/:gameKey/leaderboard` 是受保护的懒加载单棋种排行榜页

Web 客户端 SHALL 提供路由 `/g/:gameKey/leaderboard`,懒加载(`loadComponent`)且受鉴权守卫保护,渲染该棋种的分页排行榜。

`/g/<key>` 已经是每棋种的命名空间(`/g/xiangqi`、`/g/gomoku/lobby`),榜跟着走。

排行榜**卡片**归 `/g/:gameKey/lobby`,棋种取自该路由参数。它此前钉死在 `/home` 上并钉死五子棋,理由是"泛化大厅是 roadmap 上单独的一步"——那一步已经走了(`generalize-lobby`)。

此前记录在案的副作用「有一段时间会有两个入口看同一个五子棋榜」**已消除**:`/home` 不再有榜卡,`/g/gomoku/lobby` 的卡片与 `/g/gomoku/leaderboard` 的整页是"摘要"与"全量分页"的关系,不是两个入口看同一份东西。

#### Scenario: 路由懒加载
- **WHEN** 用户首次导航到 `/g/gomoku/leaderboard`
- **THEN** 该页面的 chunk 才被加载;它 MUST NOT 出现在主 bundle 里

#### Scenario: 未登录被重定向
- **WHEN** 未登录用户访问该路由
- **THEN** 走既有鉴权守卫,重定向到登录页

#### Scenario: `/home` 不再有榜卡
- **WHEN** 审阅 `/home`
- **THEN** MUST NOT 存在排行榜卡片,也 MUST NOT 发出 `GET /api/leaderboard`

#### Scenario: 大厅卡片跟随路由棋种
- **WHEN** 用户打开 `/g/gomoku/lobby`
- **THEN** 榜卡调 `top('gomoku', 10)`;打开另一个计分棋种的大厅时,同一段代码以那个键取数

---

### Requirement: 排行榜页四态齐全,且空态说人话

页面 MUST 为 loading / empty / error / data 四种状态各提供真实 UI —— loading 是骨架占位(不产生布局位移),不是 "loading…" 文本。

**empty 态的文案 MUST 说明"还没有人下过这个棋种",而不是"暂无数据"。** 一个新棋种刚上线时空榜是
**常态而不是故障**,而通用的"暂无数据"会被读成后者。这是服务端刻意选择"未登记棋种返回 200 + 空榜
而不是 404"的前端对应物:两边都在说"空不等于坏"。

`gameKey` 不是计分棋种、或根本没登记时,页面 MUST 显示说明性空态,MUST NOT 渲染成错误 ——
后端对这些键返回的就是 200 + 空榜。

#### Scenario: 空榜
- **WHEN** 请求一个尚无人下过的棋种的榜
- **THEN** 渲染 empty 态,文案说明"还没有人下过",MUST NOT 显示错误图标或重试按钮之外的报错语气

#### Scenario: 不计分的棋种
- **WHEN** 直接访问 `/g/tictactoe/leaderboard`
- **THEN** HTTP 200 + 空榜 → 渲染说明性空态,MUST NOT 报错

#### Scenario: 未登记的棋种键
- **WHEN** 访问 `/g/a-game-nobody-registered/leaderboard`
- **THEN** 同样是说明性空态,MUST NOT 404 也 MUST NOT 报错

#### Scenario: 请求失败才是 error 态
- **WHEN** `GET /api/leaderboard` 返回 5xx 或网络失败
- **THEN** 渲染 error 态 + 重试入口

### Requirement: `LeaderboardApiService` 的 `gameKey` 是必填参数

前端 SHALL 让 `LeaderboardApiService` 的两个方法都带必填 `gameKey`:

```ts
abstract top(gameKey: string, count: number): Observable<readonly LeaderboardEntry[]>;
abstract getPage(gameKey: string, page: number, pageSize: number)
    : Observable<PagedResult<LeaderboardEntry>>;
```

服务层 MUST NOT 提供缺省棋种 —— 与后端 `GetLeaderboardQuery.GameKey` 必填同一条纪律:
**服务不猜自己在被问哪个棋种**。

每个调用点都知道自己在看哪个棋种,让服务替它猜,只会把一个"忘了传"变成"悄悄给了五子棋的数据"——而那个错误在屏幕上是看不出来的。

大厅的榜卡因此传路由里的棋种键,而不是任何字面量。

#### Scenario: URL 带上棋种
- **WHEN** 调 `getPage('xiangqi', 2, 20)`
- **THEN** 请求 URL 为 `/api/leaderboard?gameKey=xiangqi&page=2&pageSize=20`(参数顺序不限)

#### Scenario: 大厅卡片用路由的键
- **WHEN** 审阅 `/g/:gameKey/lobby` 榜卡的调用
- **THEN** 它传路由参数,MUST NOT 出现 `'gomoku'` 字面量,MUST NOT 依赖任何缺省

### Requirement: Rank 是全局名次,前三名图标沿用既有规则

页面 MUST 直接使用服务端返回的 `rank`(全局名次),MUST NOT 用页内下标重算 —— 第 2 页第一条的 `rank` 是 21 而不是 1。

前三名图标沿用 `web-lobby` 已经立下的规则:由 `rank` 驱动(1/2/3 → 🥇/🥈/🥉),主题锁定,
MUST NOT 在这里另立一套。

#### Scenario: 第二页名次不重置
- **WHEN** 翻到 page=2 / pageSize=20
- **THEN** 第一条显示的名次为 21

#### Scenario: 前三名图标
- **WHEN** 第 1 页渲染
- **THEN** 前三行分别带 🥇 / 🥈 / 🥉,第四行起无图标

### Requirement: 375px 可用、键盘可达、尊重减动偏好

页面 MUST 在 375px 宽度下可用,所有交互元素键盘可达且有可见的 `focus-visible` 环,并 MUST 尊重 `prefers-reduced-motion`。

宽表格 MUST 在自己的 `overflow-x: auto` 容器里横向滚动,页面主体 MUST NOT 横向滚动。

#### Scenario: 窄屏
- **WHEN** 视口宽 375px
- **THEN** 页面主体无横向滚动条;榜单内容在自己的容器内滚动或换行

#### Scenario: 键盘
- **WHEN** 用 Tab 遍历
- **THEN** 分页按钮与用户名链接都能获得焦点,焦点环可见

