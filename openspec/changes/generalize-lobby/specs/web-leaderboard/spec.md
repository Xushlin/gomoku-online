# web-leaderboard Specification Delta

## MODIFIED Requirements

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

### Requirement: `LeaderboardApiService` 的每个方法都必须点名棋种

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
