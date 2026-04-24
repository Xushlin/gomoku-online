## ADDED Requirements

### Requirement: `/users/:id` 路由由 `ProfilePage` 提供,惰性加载 + 鉴权守卫

`app.routes.ts` SHALL 新增 lazy 路由 `/users/:id`:

- `loadComponent: () => import('./pages/users/profile-page/profile-page').then((m) => m.ProfilePage)`
- `canMatch: [authGuard]`

`src/app/pages/users/profile-page/` SHALL 包含 `profile-page.{ts,html}` + spec、子组件 `games-list/{games-list.ts,games-list.html}`(分页对局列表)、 `profile-page` 自身负责 header card + 容器。组件全部 standalone、OnPush、`:host { display: block }`。

#### Scenario: 路径解析
- **WHEN** 已登录用户导航到 `/users/u-1`
- **THEN** 加载 `ProfilePage` lazy chunk;`id` 在组件中可读

---

### Requirement: `UsersApiService` 抽象 DI token + 默认实现

`src/app/core/api/users-api.service.ts` SHALL 定义抽象类 `UsersApiService` 作为 DI token,并提供 `DefaultUsersApiService` 实现:

- `getProfile(userId: string): Observable<UserPublicProfileDto>` —— `GET /api/users/{id}`,401/404 直接透传错误
- `getGames(userId: string, page: number, pageSize: number): Observable<PagedResult<UserGameSummaryDto>>` —— `GET /api/users/{id}/games?page=&pageSize=`
- `search(query: string, page: number, pageSize: number): Observable<PagedResult<UserPublicProfileDto>>` —— `GET /api/users?search=&page=&pageSize=`

注册位置:`app.config.ts` 与既存 `RoomsApiService` 等并列 `{ provide: UsersApiService, useClass: DefaultUsersApiService }`。

#### Scenario: 抽象类可被测试 stub
- **WHEN** 测试用 `{ provide: UsersApiService, useValue: stub }`
- **THEN** 组件通过 `inject(UsersApiService)` 拿到 stub,无需修改组件源码

#### Scenario: getProfile 路径正确
- **WHEN** `getProfile('abc 123')`
- **THEN** 实际发出 `GET /api/users/abc%20123`(URL 编码)

#### Scenario: getGames 默认分页参数
- **WHEN** `getGames('u-1', 2, 10)`
- **THEN** 实际发出 `GET /api/users/u-1/games?page=2&pageSize=10`

#### Scenario: search 编码 query
- **WHEN** `search('Ali ce', 1, 5)`
- **THEN** 实际发出 `GET /api/users?search=Ali%20ce&page=1&pageSize=5`

---

### Requirement: 前端类型 `UserPublicProfileDto` / `UserGameSummaryDto` / `PagedResult<T>` 完整化

`src/app/core/api/models/user-profile.model.ts` SHALL 声明:

```ts
export interface UserPublicProfileDto {
  readonly id: string;
  readonly username: string;
  readonly rating: number;
  readonly gamesPlayed: number;
  readonly wins: number;
  readonly losses: number;
  readonly draws: number;
  readonly createdAt: string;
}

export interface UserGameSummaryDto {
  readonly roomId: string;
  readonly name: string;
  readonly black: UserSummary;
  readonly white: UserSummary;
  readonly startedAt: string;
  readonly endedAt: string;
  readonly result: GameResult;
  readonly winnerUserId: string | null;
  readonly endReason: GameEndReason;
  readonly moveCount: number;
}

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}
```

字段名与后端 `System.Text.Json` camelCase + `JsonStringEnumConverter` 输出严格对齐。

#### Scenario: 编译通过
- **WHEN** 用上述类型解析真实 API 响应
- **THEN** 无 TypeScript 错误

#### Scenario: 枚举字段按字符串字面量
- **WHEN** 代码写 `summary.result === 'BlackWin'`
- **THEN** 编译通过;`=== 1` 不通过

---

### Requirement: 个人主页 header card 渲染身份与战绩

`ProfilePage` SHALL 在主体顶部渲染一个 header card,展示:

- Username(大字号)
- Rating(数字 + 翻译键 `profile.rating-label`)
- 战绩计数:`Wins:N`、`Losses:N`、`Draws:N`(三组,翻译键 `profile.{wins,losses,draws}-label`)
- Win rate:`wins / (wins + losses + draws)` 取百分比四舍五入到 1 位小数;若 `gamesPlayed === 0` 显示 "—"
- Joined-at:`createdAt` 走 Angular `formatDate` 按当前 locale 显示日期(无时分);翻译键 `profile.joined-label`

card MUST 用 `bg-surface text-text border-border rounded-card shadow-elevated` 等 token utilities;无硬编码色值。

#### Scenario: 战绩计数渲染
- **WHEN** profile 返回 `wins=3, losses=2, draws=1`
- **THEN** card 显示 "胜 3 / 负 2 / 平 1"(zh-CN)或 "Wins 3 / Losses 2 / Draws 1"(en)

#### Scenario: 胜率计算
- **WHEN** `wins=3, losses=2, draws=1, gamesPlayed=6`
- **THEN** Win rate 显示 "50.0%"

#### Scenario: 零场显示破折号
- **WHEN** `gamesPlayed=0`
- **THEN** Win rate 显示 "—"(避免除零或显示 0.0%)

#### Scenario: 404 fallback
- **WHEN** `getProfile` 返回 404
- **THEN** ProfilePage 不渲染 card,而是显示翻译键 `profile.not-found` + 返回大厅链接

---

### Requirement: 个人主页对局列表分页(prev/next)

`ProfilePage` 下半部 SHALL 渲染一个 `games-list` 子组件,显示该用户的对局列表(`GET /api/users/:id/games`),首屏拉 `page=1, pageSize=10`。

每行显示:

- **对手** username(链接 `[routerLink]="['/users', opponent.id]" class="username-link"`,`(click)="$event.stopPropagation()"`)。"对手" = 当前 profile 的 user **不是**的那一方。
- **我方视角的结果**:本 profile 的 user 是 winner → "胜";是 loser → "负";平局 → "平"。翻译键 `profile.result-{win,loss,draw}`。
- **End reason** 翻译(`game.ended.reason-*`)
- **Ended-at** Angular `formatDate`
- **Move count**(纯数字)

整行(除 username link)是一个 `<button>` 或可点击区域,点击 navigate 到 `/replay/:roomId`。

底部分页控件:

- **上一页** 按钮 —— `page === 1` 时 disabled
- **页码指示** —— `Page N of M`,M = `Math.ceil(total / pageSize)`,total === 0 时 M = 1
- **下一页** 按钮 —— `page * pageSize >= total` 时 disabled

切页发起新一次 `getGames(id, page, 10)` 请求,渲染 loading skeleton 直到响应。

#### Scenario: 首屏请求
- **WHEN** 用户打开 `/users/u-1`
- **THEN** 同时发起 `getProfile('u-1')` 和 `getGames('u-1', 1, 10)` 两个请求

#### Scenario: 行点击 navigate replay
- **WHEN** 用户点列表第 3 行(`roomId === 'r-x'`)
- **THEN** `router.navigateByUrl('/replay/r-x')` 被调一次

#### Scenario: 对手用户名是链接,不触发行 click
- **WHEN** 用户点列表第 3 行的对手 username
- **THEN** navigate 到 `/users/<opponent.id>`,**不**触发 navigate 到 `/replay/r-x`(stopPropagation 生效)

#### Scenario: 翻页
- **WHEN** 当前 `page=1`,用户点"下一页"
- **THEN** `page` 设为 2;新一次 `getGames('u-1', 2, 10)` 发出;旧数据被替换

#### Scenario: 上一页边界
- **WHEN** `page === 1`
- **THEN** "上一页" 按钮 `disabled`

#### Scenario: 下一页边界
- **WHEN** `total === 25`,`page === 3`,`pageSize === 10`(已经在最后一页)
- **THEN** "下一页" 按钮 `disabled`

#### Scenario: 空战绩
- **WHEN** `getGames` 返回 `items: [], total: 0`
- **THEN** 列表显示翻译键 `profile.games-empty`;翻页按钮全部 disabled

---

### Requirement: 大厅 "Find player" 卡片支持 prefix 搜索 + autocomplete 跳转

新 lobby 卡片 `src/app/pages/lobby/cards/find-player/find-player.{ts,html}` SHALL:

- 渲染一个输入框 `<input type="text">`,placeholder 为翻译键 `lobby.find-player.placeholder`
- 输入控件 250 ms debounce + distinctUntilChanged + trim;最小长度 3,低于阈值显示翻译键 `lobby.find-player.hint-too-short`
- 满足阈值后调 `users.search(query, 1, 5)`,渲染下拉:每条结果显示 username + rating(如 "alice (1280)")
- 点结果项 → navigate 到 `/users/:id`,清空输入
- 0 结果 → 显示翻译键 `lobby.find-player.no-results`
- 网络错误 → 显示翻译键 `lobby.find-player.error`,不阻塞输入

卡片样式与其他大厅卡片一致(`bg-surface border rounded-card shadow-elevated`)。

#### Scenario: 不到 3 字符不发请求
- **WHEN** 用户输入 "al"
- **THEN** 不发起任何 HTTP 调用;显示提示文本

#### Scenario: 3+ 字符触发搜索
- **WHEN** 用户输入 "ali",停顿 250ms
- **THEN** 发起 `GET /api/users?search=ali&page=1&pageSize=5` 一次

#### Scenario: 搜索期间继续打字取消旧请求
- **WHEN** 输入 "ali" 之后立即输入 "alic"(在 debounce 期间)
- **THEN** "ali" 的请求 MAY 被取消(via effect cleanup);"alic" 的请求继续

#### Scenario: 点结果跳转
- **WHEN** 下拉中显示了 alice 与 alex,用户点 alice
- **THEN** `router.navigateByUrl('/users/<alice.id>')` 被调;输入框清空

#### Scenario: 0 结果文案
- **WHEN** search 返回 `items: [], total: 0`
- **THEN** 显示翻译键 `lobby.find-player.no-results`

---

### Requirement: 用户名链接的统一约定 —— `.username-link` class + `routerLink` + stopPropagation

`global.css` SHALL 新增 `.username-link` class:

```css
.username-link {
  color: var(--color-primary);
  text-decoration: none;
}
.username-link:hover { text-decoration: underline; }
```

凡是渲染他人 username 的位置(下列 5 处),模板 MUST 用:

```html
<a class="username-link"
   [routerLink]="['/users', user.id]"
   (click)="$event.stopPropagation()">{{ user.username }}</a>
```

应用位置:

- `pages/lobby/cards/active-rooms/` —— host / black / white
- `pages/lobby/cards/my-active-rooms/` —— host / black / white
- `pages/lobby/cards/leaderboard/` —— top-10 player names
- `pages/rooms/room-page/sidebar/` —— host / black / white
- `pages/rooms/room-page/chat/chat-panel/` —— sender username

`stopPropagation` 防止链接点击冒泡到外层 row click handler(例如对局列表行的"看回放"点击)。

自己(当前登录用户)的 username 在 header 上**不**链接(避免点自己跳"自己");他处出现也照常链接。

#### Scenario: 大厅活跃房间的座位名是链接
- **WHEN** lobby 渲染一个 black=alice 的房间行
- **THEN** "alice" 文本被 `<a [routerLink]="['/users', '<alice.id>']">` 包裹

#### Scenario: 链接点击不触发外层 row click
- **WHEN** 在 my-active-rooms 行内点对手 username
- **THEN** navigate 到 `/users/<opp.id>`,该 row 既有的 "Resume" / Join 跳转 NOT 触发

---

### Requirement: `app.config.ts` 注册 `UsersApiService`

`app.config.ts` 的 providers SHALL 新增 `{ provide: UsersApiService, useClass: DefaultUsersApiService }`。

#### Scenario: 全局可注入
- **WHEN** 任何组件 / 服务 `inject(UsersApiService)`
- **THEN** 拿到默认实现实例,生命周期 app-scoped

---

### Requirement: i18n —— `replay.*` / `profile.*` / `lobby.find-player.*` / `game.ended.view-replay` 双语对齐

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增以下键集合:

- `replay.{title-prefix, errors.not-found, errors.still-in-progress, errors.generic, retry, back-to-lobby, scrubber.first, scrubber.prev, scrubber.next, scrubber.last, scrubber.play, scrubber.pause, scrubber.replay, scrubber.speed-label, scrubber.end-of-game}`
- `profile.{rating-label, wins-label, losses-label, draws-label, win-rate-label, joined-label, games-title, games-empty, result-win, result-loss, result-draw, page-indicator, prev-page, next-page, not-found, back-to-lobby}`
- `lobby.find-player.{title, placeholder, hint-too-short, no-results, error, search-button-aria}`
- `game.ended.view-replay`(注入到现有 `game.ended.*` 子树)

flatten 后两份 JSON 的 key 集合 MUST 完全一致(零漂移)。

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空

#### Scenario: 模板零硬编码
- **WHEN** grep `pages/replay/`、`pages/users/`、`pages/lobby/cards/find-player/` 下的 `.html` 文件
- **THEN** 0 个 CJK 或长英文显示字符串(全部 `| transloco`)

---

### Requirement: 颜色 / 组件 / 交互规则继承所有先前立下的约定

Replay / Profile / FindPlayer / 用户名链接相关的所有新增模板 SHALL 遵守 scaffold / auth / lobby / game-board 立下的全部横切规则,MUST NOT 引入任何绕过这些规则的图样:

- 颜色 token utilities / CSS variables;无硬编码色值或 `bg-gray-*` palette
- 对话框 / Overlay 基于 `@angular/cdk`
- 字符串走 `| transloco`
- HttpClient 只在 `core/api/`(包括新增 `users-api.service.ts`)
- 组件 < 200 LOC(`ProfilePage` 230 软上限,`ReplayPage` 250 软上限,`GamesList` 150,`FindPlayer` 150)
- 375px 视口可达;无横向滚动

#### Scenario: 全局 grep 通过
- **WHEN** 在新增的 4 个目录下跑色值 / palette / CJK 三套 grep
- **THEN** 0 匹配

#### Scenario: 375px 视口
- **WHEN** 在 375 × 667 视口分别打开 `/replay/:id` 和 `/users/:id`
- **THEN** 无横向滚动;主功能(scrubber / pagination)可达
