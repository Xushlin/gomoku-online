# Tasks — generalize-lobby

## 1. 轮询引擎抽出来

- [x] 1.1 `core/lobby/slice-engine.ts`:去重、可见性 gating、半间隔补刷、teardown —— 从 `DefaultLobbyDataService` 原样搬出,**不改行为**。它不是 `@Injectable`:没有自己的 DI,由构造它的 service 持有,于是"页面死了定时器就死"是结构事实而不是约定。
- [x] 1.2 `HomeDataService`(`onlineCount` + `myRooms`)。
- [x] 1.3 `LobbyDataService` 收窄到 `rooms` + `leaderboard`,棋种来自注入的 `LOBBY_GAME_KEY`。
- [x] 1.4 两个服务共用同一个引擎,机制只有一份实现。

## 2. 两个页面

- [x] 2.1 `Lobby`(`/home`,eager):hero、games-strip、my-active-rooms、my-recent-games、find-player。
- [x] 2.2 `GameLobby`(`/g/:gameKey/lobby`,lazy):棋种名标题、active-rooms、ai-game、leaderboard。
- [x] 2.3 卡片与 dialog 留在 `pages/lobby/` 下由两页共用。
- [x] 2.4 `games-strip` 读 `GAME_REGISTRY`,只列 `available`。
- [x] 2.5 两个 dialog 改为注入 `LOBBY_GAME_KEY`,不再拿 `GOMOKU_KEY` 字面量。
- [x] 2.6 **CDK dialog 默认用根注入器**,拿不到页面级的 `LOBBY_GAME_KEY`。两个打开处都显式传 `injector: this.injector`。这是那种单测能抓、但只有真跑一次才会想到去写的接线。

## 3. 不可用的棋种

- [x] 3.1 未登记 → "本平台没有这个游戏" + 链到 `/games`。
- [x] 3.2 `supportsHumanVsHuman === false` → "目前只有人机对战" + 链到该棋种 `launchRoute`。
- [x] 3.3 `capabilities.loaded()` 为 false 时保持骨架。
- [x] 3.4 不重定向;实测 `/g/go/lobby` 的 `location.pathname` 仍是 `/g/go/lobby`。

## 4. 路由与清单

- [x] 4.1 `app.routes.ts` 加 `g/:gameKey/lobby`(lazy + authGuard)。
- [x] 4.2 `gomokuManifest.launchRoute` → `/g/gomoku/lobby`。
- [x] 4.3 `app.routes.spec` 的"每份清单都有路由"断言以前是字符串相等,遇到参数化路由会误报。改成按段匹配(`:param` 匹配任意一段)。**这条不改的话,下一个游戏会为了让测试闭嘴而去写一条字面量路由。**
- [x] 4.4 新增断言:没有任何 `available` 清单的 `launchRoute` 等于 `/home`。

## 5. i18n

- [x] 5.1 新键双语齐备;flatten 后 388 个键,差集为空。

## 6. 测试

- [x] 6.1 引擎行为测试保留,并按两个服务各跑一遍。
- [x] 6.2 `/home` 的关键断言:`expectNone('/api/rooms')` + `expectNone('/api/leaderboard')`。
- [x] 6.3 `GameLobby`:棋种取自路由(`idiom-chain` 一例证明换键即换请求)。
- [x] 6.4 三种不可用态 + 骨架门各一条。
- [x] 6.5 `games-strip`:只列 available、跳过 planned、href 等于 launchRoute、源码里不出现任何棋种名。
- [x] 6.6 榜卡在 `isRated === false` 时不渲染。
- [x] 6.7 **469 passed**(此前 453)。

## 7. 验证

- [x] 7.1 `npm run lint` 全绿;`npm run test:ci` 469 passed。
- [x] 7.2 **bundle 实测**,提案里那句"应该会变小"是预测,这是结果:

  | | 变更前 | 变更后 |
  | --- | --- | --- |
  | Initial total(raw) | 537.05 kB | **500.35 kB** |
  | Estimated transfer | 137.44 kB | **130.82 kB** |
  | 超预算 | 37.05 kB | **350 bytes** |

  `game-lobby` 懒块 25.75 kB。那条挂了很久的"初始包超预算"待办没有被解决,但从超 37 kB 变成超 350 字节。

- [x] 7.3 浏览器实跑:
  - `/home` —— 只发 `presence/online-count`、`users/me/active-rooms`、`users/<id>/games`;**没有** `rooms?gameKey=`、**没有** `leaderboard`。游戏入口条列出 5 款可玩的。
  - `/g/gomoku/lobby` —— `GET /api/games` + `rooms?gameKey=gomoku` + `leaderboard?gameKey=gomoku`;建房 `POST /api/rooms → 201`,响应体 `gameKey: "gomoku"`,列表随即刷新。
  - `/g/xiangqi/lobby` —— "目前只有人机对战" + 去处链接,不发房间请求。
  - `/g/go/lobby` —— "本平台没有这个游戏",URL 未变。
- [x] 7.4 375px 无横向滚动 —— 见 §9。
- [x] 7.5 **后端零改动**:`git status` 中无 `backend/` 文件。

## 8. 明确留给下一步

- [x] 8.1 `lobby-return-target`:`room-page` 五处 `navigateByUrl('/home')` 改为回到该棋种的大厅。
- [x] 8.2 把 `/g/tictactoe`、`/g/xiangqi` 折进大厅路由不在本次范围。
- [x] 8.3 AI 卡片**无条件渲染**。「这个棋种有没有 AI」只能由 `IGameAiRegistry` 回答,而 `GameDescriptorDto` 上还没有那个投影字段;加它是后端改动,且今天没有消费者(有大厅的棋种只有五子棋,它有 AI)。留到第一个"有人人对战、没有 AI"的棋种出现 —— 那时这个分支才第一次能被真实用例检验。

## 9. 浏览器里抓到的一个缺陷(单测抓不到)

- [x] 9.1 375px 下 `/g/gomoku/lobby` 横向溢出 8px(`scrollWidth` 383 > `clientWidth` 375),违反"每个路由 MUST 在 375px 无横向滚动"。

  成因:房间行的 `<p>` 里是一串相邻 inline `<span>`/`<a>`。**Angular 默认 `preserveWhitespaces: false`,把元素之间的空白删掉了**,于是整行没有任何换行机会。改成 `flex flex-wrap items-center gap-x-1` 后正常。

  它**不是本变更引入的** —— `active-rooms.html` 的这段标记没被本变更碰过,而 375px 下两个页面给它的可用宽度相同。它一直没被发现,是因为只有**房间列表非空**时才出现,而此前每次量 375px 的时候列表都是空的。

  > 一条"375px 无横向滚动"的验收,在空列表上永远通过。

  按仓库规矩,这属于"让代码符合既有 spec 的纯 bug 修复",不需要单独提案。`my-active-rooms.html` 有同样的标记,一并改了。

- [x] 9.2 复测:`/g/gomoku/lobby`(1 个房间行)与 `/home`(6 个列表项)在 375px 下 `scrollWidth === clientWidth === 375`。
