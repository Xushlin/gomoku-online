# web-leaderboard Specification

## Purpose
TBD - created by archiving change add-web-per-game-rating. Update Purpose after archive.
## Requirements
### Requirement: `/g/:gameKey/leaderboard` 是受保护的懒加载单棋种排行榜页

Web 客户端 SHALL 提供路由 `/g/:gameKey/leaderboard`,懒加载(`loadComponent`)且受鉴权守卫保护,渲染该棋种的分页排行榜。

`/g/<key>` 已经是每棋种的命名空间(`/g/tictactoe`、`/g/idiom-crossword`),榜跟着走。

**`/home` 的排行榜卡片 MUST NOT 改动**,仍然钉死五子棋。它是**五子棋大厅**的一张卡片;给它加
棋种切换等于开始泛化大厅,而 `/home` 在五份 web spec 里是规范路径。那是 roadmap 上单独的一步。

副作用记录在案:有一段时间会有两个入口看同一个五子棋榜(`/home` 的卡片与
`/g/gomoku/leaderboard`)。这是**已知重复**,不是遗漏 —— 大厅泛化那一步会消掉它。

#### Scenario: 路由懒加载
- **WHEN** 用户首次导航到 `/g/gomoku/leaderboard`
- **THEN** 该页面的 chunk 才被加载;它 MUST NOT 出现在主 bundle 里

#### Scenario: 未登录被重定向
- **WHEN** 未登录用户访问该路由
- **THEN** 走既有鉴权守卫,重定向到登录页

#### Scenario: `/home` 卡片不受影响
- **WHEN** 审阅 `/home` 的排行榜卡片
- **THEN** 它仍然不带 `gameKey` 调用,仍然显示五子棋前若干名

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

`LeaderboardApiService` 的方法签名 MUST 把 `gameKey` 作为**必填**参数:

```
abstract top(gameKey: string, count: number): Observable<readonly LeaderboardEntry[]>;
abstract getPage(gameKey: string, page: number, pageSize: number)
    : Observable<PagedResult<LeaderboardEntry>>;
```

服务层 MUST NOT 提供缺省棋种 —— 与后端 `GetLeaderboardQuery.GameKey` 必填同一条纪律:
**服务不猜自己在被问哪个棋种**。

后端的缺省之所以存在,是因为 controller 是向后兼容的边界(已发布客户端不送这个参数)。前端没有
这层义务:每个调用点都知道自己在看哪个棋种,让服务替它猜,只会把一个"忘了传"变成
"悄悄给了五子棋的数据" —— 而那个错误在屏幕上是看不出来的。

`/home` 的卡片因此显式传 `'gomoku'`,把它此刻的钉死变成**代码里写着的事实**而不是省略造成的默认。

#### Scenario: URL 带上棋种
- **WHEN** 调 `getPage('xiangqi', 2, 20)`
- **THEN** 请求 URL 为 `/api/leaderboard?gameKey=xiangqi&page=2&pageSize=20`(参数顺序不限)

#### Scenario: 大厅卡片显式钉死
- **WHEN** 审阅 `/home` 排行榜卡片的调用
- **THEN** 它显式传 `'gomoku'`,MUST NOT 依赖任何缺省

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

