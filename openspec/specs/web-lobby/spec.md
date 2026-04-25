# web-lobby Specification

## Purpose
TBD - created by archiving change add-web-lobby. Update Purpose after archive.
## Requirements
### Requirement: `/home` 路由是受保护的大厅页,渲染 Lobby 组件

`app.routes.ts` SHALL 把 `/home` 路由从 scaffold 的占位 `Home` 切换为 `Lobby` 组件,并加上 `canMatch: [authGuard]`。未登录用户访问 `/home` MUST 被 `authGuard` 重定向到 `/login?returnUrl=/home`,而不是渲染一个空/崩的页面。

路由保持 eager(component: Lobby,而非 loadComponent),理由:大厅是登录后的落地页,放在主包里避免登录后再 round-trip 拉 chunk。

`src/app/pages/home/` 目录 SHALL 被重命名为 `src/app/pages/lobby/`,`Home` 类 SHALL 更名为 `Lobby`(selector `app-lobby`)。旧 `home.spec.ts` SHALL 由 `lobby.spec.ts` 替换。

#### Scenario: 匿名用户被守卫拦截
- **WHEN** 未登录用户访问 `/home`
- **THEN** `authGuard` 返回 UrlTree 重定向到 `/login?returnUrl=/home`;`Lobby` 组件 MUST NOT 被实例化(无 API 请求发出)

#### Scenario: 已登录用户看到大厅
- **WHEN** 已登录用户访问 `/home`
- **THEN** `Lobby` 组件挂载,四张卡片(Hero / Active rooms / My active rooms / Leaderboard)渲染各自的 loading 状态,并并行发起四次 REST 请求

#### Scenario: `/home` 仍在主包
- **WHEN** 生产构建完成
- **THEN** `Lobby` 组件的代码 MUST 位于初始 eager chunk 中(不是 lazy chunk)

---

### Requirement: 四张卡片的可视契约 —— loading / empty / error / data 四态

Lobby 页 SHALL 渲染四张语义独立的卡片,每张 MUST 有明确的四态 UI:

- **Hero** —— 欢迎语(`{{ 'lobby.hero.welcome' | transloco : { username: user()?.username } }}`)+ 当前在线人数(数字 + 带翻译后缀的文本)。loading 时数字替换为骨架块,error 时显示占位符 `—`(不整卡报错)。
- **Active rooms** —— 卡片标题 + "创建房间"按钮 + 房间列表。
  - loading:骨架占位 3 行。
  - empty:翻译文案 "还没有房间 — 创建一个开始" + 一个次按钮 "创建房间"。
  - error:错误文案 + "重试"按钮(调用 `service.rooms.refresh()`)。
  - data:每行显示 `{ name, status badge, host, black/white seats, spectators count }` + 右侧操作按钮(`Waiting` → `Join`,`Playing` → `Watch`,`Finished` 不显示 —— 但列表过滤不返回 Finished)。
- **My active rooms** —— 同形四态,data 行显示 `{ name, host, 我是 Black/White/spectator, status }` + `Resume` 按钮。empty 文案 "你目前没有进行中的对局。"
- **Leaderboard** —— 同形四态,data 展示 Top 10 `{ rank, icon, username, rating, W-L-D }`。Rank ≤ 3 时 icon 为金/银/铜(见下一条)。leaderboard 不轮询(只在 mount 时拉一次),所以没有"自动变化后刷新"场景,但 error 态仍提供重试按钮。

任一卡片的 error 状态 MUST NOT 影响其它三张的渲染 —— 整体页面不能因一个端点失败而白屏。

#### Scenario: 一个 API 失败,其它卡片正常
- **WHEN** `GET /api/leaderboard` 回 500,其余三个端点正常
- **THEN** Leaderboard 卡片显示 error 状态 + 重试按钮;Hero / Active rooms / My active rooms 正常渲染数据

#### Scenario: loading → data
- **WHEN** 用户首次进入 `/home`
- **THEN** 四张卡片 MUST 先显示各自的骨架(骨架 MUST 使用主题 token 着色,不能硬编码灰色)直到每个 API 响应回来,然后各自独立切到 data 状态

#### Scenario: 空房间列表
- **WHEN** `GET /api/rooms` 返回 `[]`
- **THEN** Active rooms 卡片显示 empty 文案(`lobby.rooms.empty` 翻译)+ "创建房间"按钮;MUST NOT 显示"发生错误"之类的 error 态

---

### Requirement: 主题锁定的排行榜前三图标 —— 客户端 rank 驱动

`LeaderboardCard` SHALL 根据 `entry.rank` 值渲染图标:

- `rank === 1` → 金(默认 Unicode `🥇` 或主题化 SVG)
- `rank === 2` → 银(`🥈`)
- `rank === 3` → 铜(`🥉`)
- `rank >= 4` → 无图标,只显示数字 rank

图标元素 MUST `aria-hidden="true"`;相邻的 `<span>` 用 `lobby.leaderboard.tier-gold` / `tier-silver` / `tier-bronze` 翻译键承载语义(给屏幕阅读器)。服务端 DTO MUST NOT 被要求新增 tier 字段 —— 全部由客户端从 `rank` 派生。

#### Scenario: 图标映射
- **WHEN** 后端返回 Top 10 其中 rank=1、2、3 分别是 Alice、Bob、Carol
- **THEN** 卡片中 Alice 行显示金图标 + `aria-label="Gold"`,Bob 行银、Carol 行铜;第 4~10 行只显示数字 rank,无图标

#### Scenario: 不耦合后端字段
- **WHEN** 后端 `LeaderboardEntry` 增加或未来移除 tier 字段
- **THEN** 前端行为不变(rank 是唯一决定因素)

---

### Requirement: `LobbyDataService` —— 页面级 Signal store + 每片独立轮询

`src/app/core/lobby/lobby-data.service.ts` SHALL 定义 `LobbyDataService`(abstract class 做 DI token,可选实现替换),但本次 ship 一个 `DefaultLobbyDataService` 即可。Service MUST 通过 `Lobby` 组件的 `providers: [...]` 注入,**不要** `providedIn: 'root'` —— 生命周期 MUST 与 `Lobby` 组件绑定,组件销毁即停表。

每个 slice 暴露三个只读 signal + 一个 `refresh()`:

```ts
readonly onlineCount: { data: Signal<number | null>; loading: Signal<boolean>; error: Signal<unknown | null>; refresh(): void };
readonly rooms:       { data: Signal<readonly RoomSummary[] | null>; loading: ...; error: ...; refresh(): void };
readonly myRooms:     { data: Signal<readonly RoomSummary[] | null>; loading: ...; error: ...; refresh(): void };
readonly leaderboard: { data: Signal<readonly LeaderboardEntry[] | null>; loading: ...; error: ...; refresh(): void };
```

行为契约:

- 构造 service 时:四个 slice 都 `refresh()` 一次(初始拉取);为 `onlineCount` / `rooms` / `myRooms` 启动各自的 `setInterval`(间隔来自 `LOBBY_POLLING_CONFIG`)。`leaderboard` 无轮询。
- 轮询 gating:在每个 interval tick 处,MUST 检查 `document.visibilityState === 'visible'`。非 visible 时跳过本次 tick(不计入"刚拉过")。
- `visibilitychange` → `visible` 时:MUST 立即 `refresh()` 每一个"自上次成功拉取已经过去半个 interval 以上"的 slice。
- `refresh()` 去重:如果该 slice 上一个 HTTP 还在飞(`loading === true`),MUST NOT 发起新的 HTTP,直接返回。
- 组件 `ngOnDestroy` → service `teardown()`:清所有 `setInterval`,解绑 `visibilitychange`。
- 一个 slice 的 error MUST NOT 影响其它 slice 的状态 signal。

#### Scenario: 初始拉取
- **WHEN** `Lobby` 组件首次挂载
- **THEN** 四个 slice 的 `loading` 先变 `true`,随后对应 HTTP 完成时各自变回 `false`,且 `data` 被填充;四次 HTTP 请求 MUST 并行发出

#### Scenario: 隐藏 tab 不轮询
- **WHEN** tab 隐藏 30 分钟(`document.visibilityState === 'hidden'`)
- **THEN** 在这 30 分钟内 MUST NOT 有任何来自 LobbyDataService 的 HTTP 请求发出

#### Scenario: 重新可见立即补刷
- **WHEN** tab 从 hidden 变回 visible,且距离上次成功拉取 rooms 已 > 7.5 s(interval 15 s 的一半)
- **THEN** MUST 立即 `rooms.refresh()`,无需等 interval

#### Scenario: 去重并发
- **WHEN** 前一个 `rooms` 请求还在 pending 时,interval tick 到期
- **THEN** MUST NOT 发出第二个 `/api/rooms` 请求

#### Scenario: 组件销毁停表
- **WHEN** 用户离开 `/home`(比如跳 `/account/password`)
- **THEN** `LobbyDataService` 的 `setInterval` MUST 全部被 `clearInterval`;`visibilitychange` listener MUST 被 `removeEventListener`

#### Scenario: 错误不传染
- **WHEN** `GET /api/leaderboard` 回 500,随后其它 slice 的 tick 到期
- **THEN** 其它 slice 的轮询 MUST 继续,且它们的 `error` signal MUST NOT 被 leaderboard 的错误污染

---

### Requirement: 轮询间隔通过 `LOBBY_POLLING_CONFIG` InjectionToken 配置

前端 SHALL 定义 `LOBBY_POLLING_CONFIG = new InjectionToken<LobbyPollingConfig>('lobby.polling-config')`,默认值:

```ts
{
  onlineCountMs: 30_000,
  roomsMs: 15_000,
  myRoomsMs: 30_000,
}
```

`DefaultLobbyDataService` MUST `inject(LOBBY_POLLING_CONFIG)` 读取值;测试可通过 `{ provide: LOBBY_POLLING_CONFIG, useValue: { onlineCountMs: 0, roomsMs: 0, myRoomsMs: 0 } }` 把轮询压到 0 以同步测试"是否轮询"的逻辑而不用 `vi.useFakeTimers`。

#### Scenario: 默认值
- **WHEN** 生产代码运行 `inject(LOBBY_POLLING_CONFIG)`
- **THEN** 返回 `{ onlineCountMs: 30_000, roomsMs: 15_000, myRoomsMs: 30_000 }`

#### Scenario: 测试覆盖
- **WHEN** 测试用 `TestBed.configureTestingModule` 提供 `{ provide: LOBBY_POLLING_CONFIG, useValue: { onlineCountMs: 50, roomsMs: 50, myRoomsMs: 50 } }`
- **THEN** `DefaultLobbyDataService` 使用 50ms 作为间隔(测试可 `await new Promise(r => setTimeout(r, 200))` 观察多次轮询)

---

### Requirement: REST API 服务 —— 抽象类 DI token,典型结构

前端 SHALL 在 `src/app/core/api/` 下提供以下 service,每个 MUST 是 abstract class 作为 DI token(匹配 `AuthService` / `ThemeService` 的模式),由 `Default*ApiService` 实现:

- **`PresenceApiService`**
  - `abstract getOnlineCount(): Observable<number>` — GET `/api/presence/online-count`,把响应的 `{ count }` 解包为纯数字再给调用方。

- **`RoomsApiService`**
  - `abstract list(): Observable<readonly RoomSummary[]>` — GET `/api/rooms`
  - `abstract myActiveRooms(): Observable<readonly RoomSummary[]>` — GET `/api/users/me/active-rooms`
  - `abstract getById(roomId: string): Observable<RoomState>` — GET `/api/rooms/{id}`
  - `abstract create(name: string): Observable<RoomSummary>` — POST `/api/rooms` `{ name }`
  - `abstract join(roomId: string): Observable<RoomState>` — POST `/api/rooms/{id}/join`
  - `abstract leave(roomId: string): Observable<void>` — POST `/api/rooms/{id}/leave`

- **`LeaderboardApiService`**
  - `abstract top(count: number): Observable<readonly LeaderboardEntry[]>` — GET `/api/leaderboard?page=1&pageSize=<count>`,返回 `items` 数组(不暴露分页元信息到此方法)
  - `abstract getPage(page: number, pageSize: number): Observable<PagedResult<LeaderboardEntry>>` — 同端点,返回完整 `PagedResult`

所有 service MUST `inject(HttpClient)`;各 `Default*ApiService` MUST 用 `@Injectable({ providedIn: 'root' })` 注册,然后在 `app.config.ts` 通过 `{ provide: PresenceApiService, useClass: DefaultPresenceApiService }` 把抽象类绑到实现。

组件 MUST NOT 直接 `inject(HttpClient)`;所有 HTTP 只能从 `src/app/core/api/**/*.ts` 里发出(沿用 `web-shell` 立的规则)。

#### Scenario: 组件通过抽象类拿 service
- **WHEN** `LobbyDataService` 想拉房间列表
- **THEN** 它 `inject(RoomsApiService)`(抽象类),不 `inject(DefaultRoomsApiService)`;测试可提供 stub

#### Scenario: 正确的 URL + 方法
- **WHEN** 各 service 的 method 被调用
- **THEN** 实际发出的 HTTP 请求 method + path 符合上表

#### Scenario: create-room 请求体
- **WHEN** 调 `rooms.create('My room')`
- **THEN** 实际发出 `POST /api/rooms` 带 body `{ name: 'My room' }`,响应成功时返回一个 `RoomSummary`

---

### Requirement: 类型化 DTO —— `src/app/core/api/models/` 下的扁平数据类型

DTO 文件 SHALL 独立于 service 文件,放在 `src/app/core/api/models/`:

- `room.model.ts`:
  ```ts
  export type RoomStatus = 'Waiting' | 'Playing' | 'Finished';
  export interface UserSummary { readonly id: string; readonly username: string; }
  export interface RoomSummary {
    readonly id: string;
    readonly name: string;
    readonly status: RoomStatus;
    readonly host: UserSummary;
    readonly black: UserSummary | null;
    readonly white: UserSummary | null;
    readonly spectatorCount: number;
    readonly createdAt: string; // ISO8601 from wire; parse lazily if needed
  }
  export interface RoomState { /* shape pinned to backend's RoomStateDto — placeholder page only reads name/host/side; full shape is filled in by add-web-game-board */ }
  ```

- `presence.model.ts`:
  ```ts
  export interface OnlineCountWire { readonly count: number }
  ```
  (service method unwraps this into a plain `number` before handing to caller)

- `leaderboard.model.ts`:
  ```ts
  export interface LeaderboardEntry {
    readonly rank: number;
    readonly userId: string;
    readonly username: string;
    readonly rating: number;
    readonly gamesPlayed: number;
    readonly wins: number;
    readonly losses: number;
    readonly draws: number;
  }
  export interface PagedResult<T> {
    readonly items: readonly T[];
    readonly total: number;
    readonly page: number;
    readonly pageSize: number;
  }
  ```

字段名 MUST 对齐后端实际 wire 形态(camelCase);实施时 MUST 通过读 `backend/src/Gomoku.Api/Common/DTOs/*.cs`(或等价)确认 `RoomSummaryDto` 的真实字段名后再 ship。

#### Scenario: 类型收敛到后端
- **WHEN** 实施期对比 `backend/` 下的 DTO 源文件
- **THEN** `RoomSummary` 的每个字段名与后端 DTO 的 JSON 序列化名完全一致(camelCase 对 camelCase)

---

### Requirement: 创建房间对话框 —— CDK Dialog + Reactive Forms

`src/app/pages/lobby/dialogs/create-room-dialog/` SHALL 基于 `@angular/cdk/dialog`(`CdkDialog` / `DialogRef`,不用手写 `<div>` + `*ngIf`)。表单字段:

- `name` —— 必填,长度 3~50,非纯空白,client-side validator:`[Validators.required, Validators.minLength(3), Validators.maxLength(50), Validators.pattern(/\S/)]`

提交流程:

1. 提交按钮在 in-flight 期间(`submitting()` signal 为 true)MUST disabled。
2. 调 `rooms.create(name)`:
   - 成功:关闭对话框并 `emit()` / return 新建的 `RoomSummary` 给调用方;调用方(Active rooms card)触发 `lobbyData.rooms.refresh()`(保证新房间立即出现在列表)。
   - 400 + `ProblemDetails.errors` 有 `Name` 字段:调 `mapProblemDetailsToForm` 把错误落到对应字段。
   - 其它错误:顶部 banner 翻译 `lobby.create-room.errors.generic` / `.network`。

对话框 Header / 标签 / 占位符 / 按钮文本全部走 `| transloco`,零硬编码字符串。

#### Scenario: 成功创建后刷新房间列表
- **WHEN** 用户打开对话框输入 "My room" 点击提交,后端回 201 + RoomSummary
- **THEN** 对话框关闭;Active rooms 卡片 MUST 在下一帧/下一次 refresh 前就看到新房间(由调用方显式 `rooms.refresh()` 触发),而不是等 15 s 的轮询

#### Scenario: 名字太短本地拦截
- **WHEN** 用户输入 `"ab"` 并失焦
- **THEN** 表单显示 `auth.errors.required` 风格的翻译(`lobby.create-room.errors.min-length`),MUST NOT 发起 HTTP

#### Scenario: 名字重复 / 其它 400
- **WHEN** 后端回 400 `ProblemDetails.errors.Name = ["..."]`
- **THEN** 对应字段显示服务端 `server` error(模板 `ctrl.errors['server']` 插值,不走 innerHTML)

---

### Requirement: 点击房间导航到 `/rooms/:id`,并在需要时自动 `POST join`

Active rooms / My active rooms 卡片的操作按钮 SHALL 执行如下流程:

- **Active rooms → "Join"**(对 `Waiting` 房间):`rooms.join(id)` → 等待 200 → `router.navigate(['/rooms', id])`
- **Active rooms → "Watch"**(对 `Playing` 房间):`rooms.spectate(id)` → 等待 204 → `router.navigate(['/rooms', id])` (注:spectate 端点本次需要在 `RoomsApiService` 上追加 `spectate(roomId): Observable<void>` 方法)
- **My active rooms → "Resume"**:直接 `router.navigate(['/rooms', id])`(已在房间里,不重复 join)

任何一步失败:保持在 lobby,按卡片的 error 状态 UX 展示。

`/rooms/:id` 路由由 **本 change** ship 一个 `RoomPlaceholder` 懒加载组件(见下一条)临时填充,`add-web-game-board` 会替换。

#### Scenario: Join Waiting 房间
- **WHEN** 用户点击 Active rooms 卡片某 Waiting 行的 Join
- **THEN** 先 POST `/api/rooms/:id/join` 返回 200;随后 `router.navigate(['/rooms', id])` 触发

#### Scenario: 409 AlreadyInRoom 也继续导航
- **WHEN** 后端 join 回 409 `AlreadyInRoom`
- **THEN** 视同成功 —— 用户本来就在房间里 —— `router.navigate(['/rooms', id])` 仍然执行

#### Scenario: Resume 不重复 join
- **WHEN** 用户点 My active rooms 的 Resume
- **THEN** MUST NOT 发出 `/api/rooms/:id/join`;直接跳 `/rooms/:id`

---

### Requirement: i18n —— `lobby.*` 翻译树同步扩充

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增 `lobby.*` 键集合:

- `lobby.hero.{welcome, online-count-label, online-count-empty}`
- `lobby.rooms.{title, create-button, empty, loading-retry, join, watch, status-waiting, status-playing, status-finished, seat-black, seat-white, seat-empty, host, spectators}`
- `lobby.my-rooms.{title, empty, resume, you-are-black, you-are-white, you-are-spectator}`
- `lobby.leaderboard.{title, empty, rank, rating, wins, losses, draws, tier-gold, tier-silver, tier-bronze}`
- `lobby.create-room.{dialog-title, name-label, name-placeholder, submit, submit-loading, cancel}`
- `lobby.create-room.errors.{min-length, max-length, whitespace-only, generic, network}`
- `lobby.errors.{generic, network, retry}`
- `lobby.placeholder.{coming-soon, leave-room, room-not-found, back-to-lobby}`

两份 JSON 的 flattened key 集合 MUST 完全相等。

#### Scenario: 键集合一致
- **WHEN** 对比 flattened 后的 `en.json` 与 `zh-CN.json`
- **THEN** 差集为空

#### Scenario: 模板零硬编码
- **WHEN** 在 `src/app/pages/lobby/**/*.html` 与 `src/app/pages/rooms/**/*.html` 中搜索 CJK 字符或 ≥ 3 字母的显示英文字符串
- **THEN** 0 匹配(brand 名 `Gomoku`、技术 test-id 等非展示字符串除外)

---

### Requirement: 色彩 / 组件 / 交互规则继承 scaffold 与 auth

大厅页 SHALL 遵守 scaffold 与 auth 已立的所有横切规则,MUST NOT 引入任何绕过这些规则的图样:

- 所有颜色 MUST 来自 token utilities(`bg-bg` / `bg-surface` / `text-text` / `text-primary` / `border-border` / `text-danger` / `text-success` / `rounded-card` / `shadow-elevated`)。MUST NOT 出现硬编码色值或 `bg-gray-*` 等 tailwind palette utility。
- 弹层 / 对话框 / 下拉 MUST 基于 `@angular/cdk`(`CdkDialog` / `CdkMenu`)。
- 所有字符串走 `| transloco`。
- `Lobby` 容器组件 < 250 LOC;每张卡片组件 < 150 LOC。
- 站点 MUST 在 375px 视口渲染:所有卡片可达,无横向滚动。

#### Scenario: 全局 grep 通过
- **WHEN** 在 `src/app/pages/lobby/`、`src/app/pages/rooms/`、`src/app/core/api/`、`src/app/core/lobby/` 下跑和 auth 一致的色值 / tailwind 色 / CJK 三套 grep
- **THEN** 0 匹配(home.spec.ts 类的 fixture 例外延续 auth 的约定)

#### Scenario: 375px 无横向滚动
- **WHEN** 在 375 × 667 视口访问 `/home` 且已登录
- **THEN** `document.documentElement.scrollWidth <= document.documentElement.clientWidth`

### Requirement: 大厅"Find player"卡片支持名字前缀搜索 + 跳转资料页

Lobby 页 SHALL 在卡片网格中新增一张 `find-player` 卡片,与现有 4 张卡片(Hero / Active rooms / My active rooms / Leaderboard)并列。卡片代码位于 `src/app/pages/lobby/cards/find-player/find-player.{ts,html}` + spec。

行为契约见 `web-user-profile` 的 `Find player 卡片` Requirement(本 capability 只负责把它纳入 lobby 卡片网格)。

#### Scenario: 卡片在大厅渲染
- **WHEN** 用户登录后打开 `/home`
- **THEN** 卡片网格中能找到 `find-player` 卡片(标题翻译键 `lobby.find-player.title`)

#### Scenario: 单卡 error 不影响其它
- **WHEN** find-player 的搜索调用失败
- **THEN** 卡片显示 `lobby.find-player.error` 但仍可重新输入;其它 4 张卡片正常渲染

---

### Requirement: 大厅卡片中的他人 username 全部为 `/users/:id` 链接

`active-rooms`、`my-active-rooms`、`leaderboard` 三张卡片中渲染他人 username 的地方 SHALL 用 `<a class="username-link" [routerLink]="['/users', user.id]" (click)="$event.stopPropagation()">{{ user.username }}</a>` 替代纯文本。

具体覆盖位置:

- `active-rooms`:每行的 host / black 座位 / white 座位 username
- `my-active-rooms`:每行的 host / 对手座位(因为列出的都是当前用户参与的对局)
- `leaderboard`:Top 10 列表的每个 player username

`stopPropagation` MUST 防止链接点击冒泡到外层 row 的 click handler(Active rooms 行整体可触发 Join/Watch,my-active-rooms 行可触发 Resume,leaderboard 无 row click 但同样应用一致性)。

#### Scenario: active-rooms 行黑方用户名是链接
- **WHEN** active-rooms 卡片渲染一行 `{ black: { id: 'u-7', username: 'alice' } }`
- **THEN** "alice" 文本被 `<a [routerLink]="['/users', 'u-7']">` 包裹

#### Scenario: 点链接不触发外层 Join
- **WHEN** active-rooms 行 black=alice、status=Waiting,用户点 "alice"
- **THEN** navigate 到 `/users/u-7`;**不**触发该房间的 Join 流程

#### Scenario: leaderboard 用户名是链接
- **WHEN** leaderboard 卡片渲染 Top 10
- **THEN** 每个 username 是 `<a [routerLink]="['/users', <id>']">`,带 `username-link` class

