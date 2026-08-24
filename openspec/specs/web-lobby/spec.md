# web-lobby Specification

## Purpose
TBD - created by archiving change add-web-lobby. Update Purpose after archive.
## Requirements
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
  - `abstract list(gameKey: string): Observable<readonly RoomSummary[]>` — GET `/api/rooms?gameKey=<gameKey>`
  - `abstract myActiveRooms(): Observable<readonly RoomSummary[]>` — GET `/api/users/me/active-rooms`(**不**按棋种过滤 —— 它回答"我此刻在哪些局里")
  - `abstract getById(roomId: string): Observable<RoomState>` — GET `/api/rooms/{id}`
  - `abstract create(name: string, gameKey: string): Observable<RoomSummary>` — POST `/api/rooms` `{ name, gameKey }`
  - `abstract join(roomId: string): Observable<RoomState>` — POST `/api/rooms/{id}/join`
  - `abstract leave(roomId: string): Observable<void>` — POST `/api/rooms/{id}/leave`

- **`LeaderboardApiService`**
  - `abstract top(count: number, gameKey: string): Observable<readonly LeaderboardEntry[]>` — GET `/api/leaderboard?gameKey=<gameKey>&page=1&pageSize=<count>`,返回 `items` 数组
  - `abstract getPage(page: number, pageSize: number, gameKey: string): Observable<PagedResult<LeaderboardEntry>>` — 同端点,返回完整 `PagedResult`

`gameKey` 在上述方法上 MUST 是**必填参数**,MUST NOT 有默认值。服务端已不再为它填缺省,而一个可选参数会把「这个调用是给哪个棋种的」重新变成一件读调用点看不出来的事 —— 那正是缺省搬到客户端而不是被删掉。

所有 service MUST `inject(HttpClient)`;各 `Default*ApiService` MUST 用 `@Injectable({ providedIn: 'root' })` 注册,然后在 `app.config.ts` 通过 `{ provide: PresenceApiService, useClass: DefaultPresenceApiService }` 把抽象类绑到实现。

组件 MUST NOT 直接 `inject(HttpClient)`;所有 HTTP 只能从 `src/app/core/api/**/*.ts` 里发出(沿用 `web-shell` 立的规则)。

#### Scenario: 组件通过抽象类拿 service
- **WHEN** `LobbyDataService` 想拉房间列表
- **THEN** 它 `inject(RoomsApiService)`(抽象类),不 `inject(DefaultRoomsApiService)`;测试可提供 stub

#### Scenario: 正确的 URL + 方法
- **WHEN** 各 service 的 method 被调用
- **THEN** 实际发出的 HTTP 请求 method + path 符合上表

#### Scenario: create-room 请求体带棋种
- **WHEN** 调 `rooms.create('My room', 'gomoku')`
- **THEN** 实际发出 `POST /api/rooms` 带 body 严格等于 `{ name: 'My room', gameKey: 'gomoku' }`

#### Scenario: 房间列表带棋种查询串
- **WHEN** 调 `rooms.list('gomoku')`
- **THEN** 实际发出 `GET /api/rooms?gameKey=gomoku`

#### Scenario: 棋种参数没有默认值
- **WHEN** 审阅 `RoomsApiService` 与 `LeaderboardApiService` 的抽象签名
- **THEN** `gameKey` MUST NOT 带 `= '...'` 默认值,也 MUST NOT 是可选参数(`?`)

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
    /** 全部**在座**的座位。三座位棋种的第三个人只在这里 —— `black` / `white` 读不到他。 */
    readonly seats: readonly { readonly index: number; readonly player: UserSummary }[];
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

字段名 MUST 对齐后端实际 wire 形态(camelCase);实施时 MUST 通过读 `backend/src/Gewu.Api/Common/DTOs/*.cs`(或等价)确认 `RoomSummaryDto` 的真实字段名后再 ship。

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
- `lobby.rooms.{title, create-button, empty, loading-retry, join, watch, status-waiting, status-playing, status-finished, players, host, spectators, seat-vacant}`

  `seat-vacant` 标的是**空座位圆片**,而它 MUST NOT 叫 `seat-empty` —— 那个名字连同
  `seat-black` / `seat-white` 一起退役了(见下一段)。重用一个退役的键名会让这份规格
  自己的历史读不懂:下一个人读到「seat-empty 被 players 取代」,再在 JSON 里看到它,
  只能自己去推哪个意思是活的。

  `seat-black` / `seat-white` / `seat-empty` 被 `players` 取代:大厅行渲染**在座的玩家**,
  而 MUST NOT 用颜色说话 —— `board-seats.ts` 自己的文档写着那套读法只有棋盘家族可以调,
  而一个座位数大于二的棋种没有颜色可映。

  **钉住这一条的检查 MUST 看属性,不能只看 `textContent`** —— 一个退役的键名完全可以从
  `aria-label` 或 `title` 里溜回来,而那种情况下只查文本的断言是绿的。而它的 fixture
  MUST 真的渲染出一个空位:一条负向断言在「什么都没发生」时恒真。两条都是量出来的 ——
  加强之后的第一次变异仍然判绿,原因是 fixture 用了 3 个在座配一个 2 座位的棋种。
- `lobby.my-rooms.{title, empty, resume, you-are-seated, you-are-spectator}`

  `you-are-black` / `you-are-white` 被 `you-are-seated` 取代,理由同上;而它修掉的是一条
  真缺陷:三座位房间里 2 号座位上的人此前被标成「你是观战」——**在他自己的对局里**。
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
- **THEN** 0 匹配(技术 test-id 等非展示字符串除外)

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

- `active-rooms`:每行的 host username,以及 `seats` 里**每一个**在座玩家的 username

  (这一行此前写的是「host / black 座位 / white 座位」,而 `fix-lobby-seats` 之后房间行渲染的是
  `seats` —— 它是那个变更留下的漂移,在同一个 PR 里改掉。**MODIFIED 是整体替换,所以一条
  没被 delta 覆盖到的 requirement 会静静保留旧句子**,而这正是它。)
- `my-active-rooms`:**这一行是既有漂移,不是 `fix-lobby-seats` 造的** —— 这张卡的模板
  一个 username 都不渲染(只有房间名、「我在座 / 我在观战」、状态、Resume),所以这条覆盖
  要求从写下来那天起就没有实现。留在这里带标注,好过悄悄删掉一条本该被实现的要求。
- `leaderboard`:Top 10 列表的每个 player username

`stopPropagation` MUST 防止链接点击冒泡到外层 row 的 click handler(Active rooms 行整体可触发 Join/Watch,my-active-rooms 行可触发 Resume,leaderboard 无 row click 但同样应用一致性)。

#### Scenario: active-rooms 行每个在座玩家的用户名都是链接
- **WHEN** active-rooms 卡片渲染一行,其 `seats` 为 `[{ index: 0, player: { id: 'u-7', username: 'alice' } }]`
- **THEN** "alice" 文本被 `<a [routerLink]="['/users', 'u-7']">` 包裹;三个座位时**三个**都是链接

#### Scenario: 点链接不触发外层 Join
- **WHEN** active-rooms 行的 `seats` 含 alice、status=Waiting,用户点 "alice"
- **THEN** navigate 到 `/users/u-7`;**不**触发该房间的 Join 流程

#### Scenario: leaderboard 用户名是链接
- **WHEN** leaderboard 卡片渲染 Top 10
- **THEN** 每个 username 是 `<a [routerLink]="['/users', <id>']">`,带 `username-link` class

### Requirement: `CreateAiRoomDialog` 提供房间名 + 难度选择 + 提交

`src/app/pages/lobby/dialogs/create-ai-room-dialog/create-ai-room-dialog.{ts,html}` SHALL 渲染一个 CDK Dialog,内含:

- 标题(翻译键 `lobby.ai-game.dialog-title`)
- 房间名输入框 —— 验证规则与 `CreateRoomDialog` 一致:`Validators.required`、`minLength(3)`、`maxLength(50)`、`Validators.pattern(/\S/)`(非全空白);标签和 placeholder 走 `lobby.ai-game.name-label` / `.name-placeholder`
- 难度选择按钮组(`role="radiogroup"`),三个 `role="radio"` 按钮分别对应 `Easy` / `Medium` / `Hard`,labels 走 `lobby.ai-game.difficulty-{easy,medium,hard}`
- **黑白选边按钮组**(本次新增,`role="radiogroup"`),两个 `role="radio"` 按钮 `Black` / `White`,labels 走 `lobby.ai-game.side-{black,white}`,标签头走 `lobby.ai-game.side-label`
- 默认选中 `Medium` 难度与 `Black` 边
- 提交按钮(`lobby.ai-game.submit`,加载中 `lobby.ai-game.submit-loading`)与取消按钮(`lobby.ai-game.cancel`)
- 错误 banner —— 翻译键 `lobby.ai-game.errors.generic` / `.errors.network`,出现在 dialog 顶部

提交 SHALL:

1. 校验表单;无效 → `markAllAsTouched`,不提交。
2. 调 `rooms.createAiRoom(name.trim(), difficulty, humanSide)` —— **三个参数**(本次新增 humanSide 为第三参,见下一条 Requirement)。
3. 成功 → `dialogRef.close(roomState)`(传完整 `RoomState`)。
4. 400 ProblemDetails(name 字段)→ 通过 `mapProblemDetailsToForm` 把字段错误映射到表单(沿用 lobby 现有约定)。
5. 网络错误(status === 0)→ banner `lobby.ai-game.errors.network`。
6. 其它错误 → banner `lobby.ai-game.errors.generic`。

#### Scenario: 默认难度 Medium
- **WHEN** dialog 打开
- **THEN** 难度按钮组中 "Medium" 处于 active(`aria-checked="true"`)状态

#### Scenario: 默认边 Black
- **WHEN** dialog 打开
- **THEN** 边按钮组中 "Black" 处于 active(`aria-checked="true"`)状态

#### Scenario: 难度切换影响出参
- **WHEN** 用户点 "Hard" 按钮,然后输入合法 name 提交
- **THEN** `rooms.createAiRoom(<name>, 'Hard', 'Black')` 被调一次

#### Scenario: 边切换影响出参
- **WHEN** 用户保持难度 Medium,点 "White" 按钮,然后输入合法 name 提交
- **THEN** `rooms.createAiRoom(<name>, 'Medium', 'White')` 被调一次

#### Scenario: 表单非法不发请求
- **WHEN** 用户在 name 输入 "ab"(长度 2)点提交
- **THEN** `rooms.createAiRoom` MUST NOT 被调;name 字段显示 minLength 错误

#### Scenario: 成功关闭传 RoomState
- **WHEN** 后端回 201 + `{ id: 'r-ai-1', ..., game: {...}, ... }`(完整 RoomStateDto)
- **THEN** `dialogRef.close(<roomState>)` 被调一次(参数是收到的对象)

#### Scenario: 网络错误显示 banner
- **WHEN** `createAiRoom` reject `HttpErrorResponse status: 0`
- **THEN** dialog 顶部显示翻译键 `lobby.ai-game.errors.network` 的 banner;表单仍可继续修改 / 重试

---

### Requirement: `RoomsApiService` 增加 `createAiRoom(name, difficulty)` 方法

`src/app/core/api/rooms-api.service.ts` SHALL 在抽象 `RoomsApiService` 类与 `DefaultRoomsApiService` 实现中提供:

```ts
abstract createAiRoom(
  name: string,
  difficulty: BotDifficulty,
  humanSide: BotSide,
  gameKey: string,
): Observable<RoomState>;
```

`BotSide` 是字符串字面量联合类型 `'Black' | 'White'`,声明在 `src/app/core/api/models/room.model.ts`。

`gameKey` MUST 必填(服务端不再补缺省)。`humanSide` **也**提升为必填,但理由不同:服务端仍然接受省略它,前端此前每一个调用点都已经在显式传值,所以那个可选性只是签名上的残留,而残留的可选参数会诱使下一个调用点省略它。

Default 实现 `POST /api/rooms/ai`,body 严格等于 `{ name, difficulty, humanSide, gameKey }` —— 四个字段全在,不再按参数是否 `undefined` 拼装。

#### Scenario: 路径与 body
- **WHEN** 调 `rooms.createAiRoom('Defense', 'Medium', 'White', 'gomoku')`
- **THEN** 实际发出 `POST /api/rooms/ai`,body 严格等于 `{ name: 'Defense', difficulty: 'Medium', humanSide: 'White', gameKey: 'gomoku' }`

#### Scenario: 其它棋种
- **WHEN** 调 `rooms.createAiRoom('Xiangqi vs AI', 'Hard', 'Black', 'xiangqi')`
- **THEN** body 中 `gameKey === 'xiangqi'`

#### Scenario: 响应形状
- **WHEN** 后端回 201 + 完整 RoomStateDto
- **THEN** Observable emit 该对象;调用方按 `RoomState` 类型消费

### Requirement: i18n —— `lobby.ai-game.*` 双语键集合对齐

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增 `lobby.ai-game.*` 子树,至少包含:

- `title`(卡片标题)
- `description`(卡片副标题)
- `button`(主按钮 "New AI game" / "新建 AI 对局")
- `dialog-title`、`name-label`、`name-placeholder`
- `difficulty-label`、`difficulty-easy`、`difficulty-medium`、`difficulty-hard`
- **`side-label`、`side-black`、`side-white`**(本次新增)
- `submit`、`submit-loading`、`cancel`
- `errors.generic`、`errors.network`

flatten 后两份 JSON 的 key 集合 MUST 完全相等(零漂移)。

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空

### Requirement: 颜色 / 组件 / 交互规则继承所有先前立下的约定

`ai-game` 卡片 + `CreateAiRoomDialog` SHALL 遵守 scaffold / lobby / game-board 立下的全部横切规则,MUST NOT 引入任何绕过这些规则的图样:

- 颜色仅 token utilities(无 hex/rgb/hsl,无 `bg-gray-*`)
- 对话框基于 `@angular/cdk/dialog`
- 字符串走 `| transloco`
- HttpClient 仅在 `core/api/rooms-api.service.ts`(已存在)

#### Scenario: 全局 grep 通过
- **WHEN** 在 `pages/lobby/cards/ai-game/`、`pages/lobby/dialogs/create-ai-room-dialog/` 下跑色值 / palette / CJK 三套 grep
- **THEN** 0 匹配

### Requirement: 大厅 "Recent games" 卡片显示当前用户最近 5 局

Lobby 页 SHALL 在卡片网格右列(在 `my-active-rooms` 与 `ai-game` 之间)新增 `my-recent-games` 卡片。代码位于 `src/app/pages/lobby/cards/my-recent-games/my-recent-games.{ts,html}` + spec。

行为契约:

- 注入 `AuthService`、`UsersApiService`、`Router`。`userId` 来自 `auth.user()?.id`(在 `home` 路由下永远 non-null,因为有 `authGuard`)。
- 构造时调一次 `users.getGames(userId, 1, 5)`,**不轮询**(决策见 design D2)。
- 渲染至少四态:loading / empty / error / data。
  - **loading**:3 行骨架占位,token-themed `bg-border` + `animate-pulse`。
  - **empty**:翻译键 `lobby.recent-games.empty`(全新用户友好文案)。
  - **error**:翻译键 `lobby.recent-games.error` + 重试按钮(再次调 `getGames`)。
  - **data**:up to 5 行,每行:
    - 对手 username(链接 `[routerLink]="['/users', opp.id]"` + `class="username-link"` + `(click)="$event.stopPropagation()"`)。
    - "我方视角"结果:profile user 是 winner → 翻译键 `profile.result-win`;loser → `result-loss`;draw → `result-draw`(复用 profile 已有的翻译,不新增键)。
    - End reason 翻译(`game.ended.reason-*`)。
    - Ended-at 通过 Angular `formatDate` 按当前 locale 显示(`'short'` 风格)。
    - Move count 数字。
  - 整行(除 username 链接外)是一个 `<button>`,点击 navigate 到 `/replay/:roomId`。
- 卡片底部 SHALL 有 "View all" 链接 `[routerLink]="['/users', userId]"`,文本走 `lobby.recent-games.view-all` 翻译键。
- 单卡 error 不影响其它卡渲染(沿用大厅"四态独立"规则)。

样式 MUST 用 token utilities,无硬编码色值。

#### Scenario: 卡片渲染
- **WHEN** 登录用户打开 `/home`
- **THEN** 卡片网格中能找到 `my-recent-games` 卡片(标题 `lobby.recent-games.title`)

#### Scenario: 首屏请求形状
- **WHEN** lobby 首次加载,`auth.user().id === 'u-1'`
- **THEN** `users.getGames('u-1', 1, 5)` 被调一次

#### Scenario: 行点击进入回放
- **WHEN** 卡片显示数据,用户点第 2 行(roomId === 'r-x')
- **THEN** `router.navigateByUrl('/replay/r-x')` 被调一次

#### Scenario: 对手 username 链接独立跳转
- **WHEN** 用户点某行的对手 username
- **THEN** navigate 到 `/users/<opp.id>`;**不**触发该行的 navigate `/replay/...`(stopPropagation 生效)

#### Scenario: 空战绩文案
- **WHEN** `getGames` 返回 `items: [], total: 0`
- **THEN** 卡片显示翻译键 `lobby.recent-games.empty`;不显示行,不显示 "View all"(可选 —— 让卡片显得"完整空")

#### Scenario: View all 跳到自己资料页
- **WHEN** 卡片有 ≥ 1 行数据,用户点 "View all"
- **THEN** navigate 到 `/users/<userId>`(当前登录用户)

#### Scenario: 单卡 error 不影响其它
- **WHEN** `getGames` 网络失败
- **THEN** 本卡显示 error + retry;其它 5 张卡正常渲染

---

### Requirement: i18n —— `lobby.recent-games.*` 双语键集合

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增以下键:

- `lobby.recent-games.title`
- `lobby.recent-games.view-all`
- `lobby.recent-games.empty`
- `lobby.recent-games.error`

flatten 后两份 JSON 的 key 集合 MUST 完全相等(零漂移)。复用现有 `profile.result-{win,loss,draw}` 与 `game.ended.reason-*` 等键,不重复声明。

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空

### Requirement: `/home` 路由是受保护的平台主页,渲染 Lobby 组件

`app.routes.ts` SHALL 把 `/home` 路由绑到 `Lobby` 组件并加上 `canMatch: [authGuard]`。未登录用户访问 `/home` MUST 被 `authGuard` 重定向到 `/login?returnUrl=/home`,而不是渲染一个空/崩的页面。

`/home` 是**平台主页**,不是任何一个棋种的大厅。它渲染与账号有关的卡片(见下一条),MUST NOT 渲染任何需要棋种键才能取数的卡片 —— 房间列表、创建房间、人机入口、排行榜都归 `/g/:gameKey/lobby`。

路由保持 eager(`component: Lobby`,而非 `loadComponent`),理由不变:主页是登录后的落地页,放在主包里避免登录后再 round-trip 拉 chunk。该理由现在适用于一个**更小**的页面 —— 分棋种的那一半移出去并改为懒加载。

`src/app/pages/lobby/` 目录 SHALL 同时容纳两个页面组件(`Lobby` 与 `GameLobby`)及它们共用的 `cards/` 与 `dialogs/`。**MUST NOT** 为此再改一次目录名:`add-web-lobby` 已经把 `pages/home/` 改成了 `pages/lobby/`,而"大厅"恰好是这两个页面共同的名字。

#### Scenario: 匿名用户被守卫拦截
- **WHEN** 未登录用户访问 `/home`
- **THEN** `authGuard` 返回 UrlTree 重定向到 `/login?returnUrl=/home`;`Lobby` 组件 MUST NOT 被实例化(无 API 请求发出)

#### Scenario: 已登录用户看到平台主页
- **WHEN** 已登录用户访问 `/home`
- **THEN** `Lobby` 组件挂载,账号相关卡片渲染各自的 loading 状态

#### Scenario: `/home` 不再拉分棋种数据
- **WHEN** 已登录用户停留在 `/home`
- **THEN** MUST NOT 发出任何 `GET /api/rooms?gameKey=…` 或 `GET /api/leaderboard?…` 请求 —— 包括轮询

#### Scenario: `/home` 仍在主包
- **WHEN** 生产构建完成
- **THEN** `Lobby` 组件的代码 MUST 位于初始 eager chunk 中(不是 lazy chunk)

---

### Requirement: `/home` 的卡片是账号范围的 —— loading / empty / error / data 四态

`/home` SHALL 渲染以下语义独立的卡片,每张 MUST 有明确的四态 UI。**判据是它调的端点要不要棋种键** —— 不要的归这里,要的归 `/g/:gameKey/lobby`:

- **Hero** —— 欢迎语(`{{ 'lobby.hero.welcome' | transloco : { username: user()?.username } }}`)+ 当前在线人数。loading 时数字替换为骨架块,error 时显示占位符 `—`(不整卡报错)。
- **My active rooms** —— 四态齐全,data 行显示 `{ name, 我在座 / 我在观战, status }` + `Resume` 按钮。empty 文案 "你目前没有进行中的对局。" 该卡 **MUST 跨棋种** —— `GET /api/users/me/active-rooms` 不带棋种键,"我此刻在哪些局里"跨棋种正是该问题的正确答案。行内 MUST 显示该局的棋种名(翻译键 `games.<key>.title`),否则跨棋种的列表读不出所以然。
- **My recent games** —— 同形四态,行为见既有 Requirement。同样跨棋种,同样显示棋种名。
- **Find player** —— 见 `web-user-profile`。
- **Games strip** —— 见下面新增的 Requirement。

任一卡片的 error 状态 MUST NOT 影响其它卡片的渲染 —— 整体页面不能因一个端点失败而白屏。

#### Scenario: 一个 API 失败,其它卡片正常
- **WHEN** `GET /api/users/me/active-rooms` 回 500,其余端点正常
- **THEN** 该卡片显示 error 状态 + 重试按钮;其它卡片正常渲染数据

#### Scenario: loading → data
- **WHEN** 用户首次进入 `/home`
- **THEN** 各卡片 MUST 先显示各自的骨架(骨架 MUST 使用主题 token 着色,不能硬编码灰色)直到对应 API 响应回来,然后各自独立切到 data 状态

#### Scenario: 我的对局跨棋种且标出棋种
- **WHEN** 用户同时在一个五子棋房间和一个象棋房间里
- **THEN** My active rooms 两行都显示,且各自标出棋种名

---

### Requirement: 页面级 Signal store —— 两个服务共用一套轮询引擎

`src/app/core/lobby/` SHALL 提供两个 abstract class 作为 DI token,各由一个 `Default*` 实现:

- **`HomeDataService`** —— `onlineCount`、`myRooms` 两个 slice。由 `Lobby` 组件 `providers` 注入。
- **`LobbyDataService`** —— `rooms`、`leaderboard` 两个 slice,**按棋种取数**,棋种来自注入的 `LOBBY_GAME_KEY`。由 `GameLobby` 组件 `providers` 注入。

两者 MUST 通过组件的 `providers: [...]` 注入,**不要** `providedIn: 'root'` —— 生命周期 MUST 与页面组件绑定,组件销毁即停表。

拆成两个服务而不是给一个四片服务传棋种键,是因为后者会让 `/home` 每 15 秒轮询一次它已经不再渲染的房间列表。**一个不被渲染的 slice 仍然在发请求,是一种只会出现在 network 面板里的缺陷。**

轮询引擎 —— 去重、可见性 gating、半间隔补刷、teardown —— MUST 只有**一份实现**,由两个服务共用。拆的是 slice 集合,不是机制。

每个 slice 暴露三个只读 signal + 一个 `refresh()`:

```ts
interface LobbySlice<T> {
  readonly data: Signal<T | null>;
  readonly loading: Signal<boolean>;
  readonly error: Signal<unknown | null>;
  refresh(): void;
}
```

行为契约(两个服务一致):

- 构造时:每个 slice `refresh()` 一次;有间隔的启动各自的 `setInterval`(间隔来自 `LOBBY_POLLING_CONFIG`)。`leaderboard` 无轮询。
- 轮询 gating:每个 tick MUST 检查 `document.visibilityState === 'visible'`,非 visible 时跳过(不计入"刚拉过")。
- `visibilitychange` → `visible` 时:MUST 立即 `refresh()` 每一个"自上次成功拉取已过去半个 interval 以上"的 slice。
- `refresh()` 去重:该 slice 上一个 HTTP 还在飞时 MUST NOT 发起新的。
- 组件销毁 → 清所有 `setInterval`,解绑 `visibilitychange`。
- 一个 slice 的 error MUST NOT 影响其它 slice 的状态 signal。

#### Scenario: 两个页面各自只拉自己的数据
- **WHEN** `Lobby` 挂载
- **THEN** 只发出 `presence/online-count` 与 `users/me/active-rooms`;`GameLobby` 挂载时只发出 `rooms?gameKey=…` 与 `leaderboard?gameKey=…`

#### Scenario: 隐藏 tab 不轮询
- **WHEN** tab 隐藏 30 分钟
- **THEN** 在这 30 分钟内 MUST NOT 有任何来自这两个 service 的 HTTP 请求发出

#### Scenario: 重新可见立即补刷
- **WHEN** tab 从 hidden 变回 visible,且距离上次成功拉取 rooms 已 > 7.5 s(interval 15 s 的一半)
- **THEN** MUST 立即 `rooms.refresh()`,无需等 interval

#### Scenario: 去重并发
- **WHEN** 前一个 `rooms` 请求还在 pending 时,interval tick 到期
- **THEN** MUST NOT 发出第二个请求

#### Scenario: 组件销毁停表
- **WHEN** 用户离开页面
- **THEN** 该页 service 的 `setInterval` MUST 全部被 `clearInterval`;`visibilitychange` listener MUST 被 `removeEventListener`

#### Scenario: 引擎只有一份
- **WHEN** 审阅两个 `Default*DataService`
- **THEN** 去重 / 可见性 / 补刷 / teardown 的实现 MUST 只出现一次,由两者共用

---

### Requirement: "Play vs AI" 卡片提供创建 AI 对局入口

`/g/:gameKey/lobby` SHALL 在卡片网格中渲染一张 `ai-game` 卡片,代码位于 `src/app/pages/lobby/cards/ai-game/ai-game.{ts,html}` + spec。

卡片渲染标题(`lobby.ai-game.title`)、一行说明(`lobby.ai-game.description`)、一个主按钮(`lobby.ai-game.button`)。

卡片 SHALL 只在该棋种的描述符 `supportsAi === true` 时渲染,与 Leaderboard 卡按 `isRated` 渲染同形。`supportsAi` 来自 `GET /api/games`,投影自 `IGameAiRegistry`(见 `game-rules-registry`)。

**本条此前写的是"无条件渲染,并且这不是疏漏",附带一段推迟的理由,而那段理由自己写下了触发条件:**

> 于是它留到第一个"有人人对战、但没有 AI"的棋种出现那天再做 —— 那时它才第一次能被真实用例检验。

触发条件已经到了:成语接龙就是那个棋种。**推迟本身是对的** —— 为一个不存在的情况建一个测不了的分支确实是这个仓库反复付过的账。错的是它对代价的估计。

那段理由从头到尾在谈**卡片**:今天只有五子棋有大厅,五子棋有 AI,所以没有消费者。它没有谈 `POST /api/rooms/ai`,而那个端点从来不看有没有大厅。实测:该端点为 `idiom-chain` 返回 201,房间进入 `Playing` 且轮到一个不存在的机器人,60 秒后超时判真人胜 —— 成语接龙计分,于是零手棋换约 +46 ELO,可无限重复。

**所以这不是一个"渲染了没用的按钮"的缺陷,而是一个计分漏洞;卡片只是通往它的第二条路。** 这与 `enforce-human-vs-human` 是同一种错法:一条结论对着 Web UI 成立、对着 API 不成立,而写下它的人只检查了前者。本条因此明确:**隐藏卡片是展示决定,不是防线**;防线在 `ai-opponent` 的校验器里,并且 MUST 独立于本条成立。

点击按钮 SHALL 打开 `CreateAiRoomDialog`(CDK Dialog),并把当前棋种键传给它。Dialog 关闭后:

- 若 `closed` emit 一个 `RoomState` → `router.navigateByUrl('/rooms/' + state.id)`,**MUST NOT** 再发任何 REST 请求。
- 若 emit `undefined`(取消)→ 不导航。

样式契约与其它大厅卡一致:`bg-surface text-text border-border rounded-card shadow-elevated`,无硬编码色值。

#### Scenario: 卡片在棋种大厅渲染
- **WHEN** 登录用户打开 `/g/gomoku/lobby`
- **THEN** 卡片网格中能找到 `ai-game` 卡片

#### Scenario: 没有 AI 的棋种不渲染这张卡
- **WHEN** 登录用户打开 `/g/idiom-chain/lobby`
- **THEN** MUST NOT 渲染 `ai-game` 卡片

#### Scenario: 描述符未到时不下结论
- **WHEN** `capabilities.loaded()` 为 false
- **THEN** 页面显示骨架,MUST NOT 已经决定这张卡片渲不渲染

#### Scenario: 卡片带上路由的棋种
- **WHEN** 在 `/g/gomoku/lobby` 提交该卡片的 dialog
- **THEN** `createAiRoom` 收到的第四个参数是 `'gomoku'`,MUST NOT 是任何字面量常量

#### Scenario: 创建成功后跳转
- **WHEN** 用户点按钮 → dialog 提交合法表单 → 后端回 201 + RoomStateDto
- **THEN** `router.navigateByUrl('/rooms/<roomId>')` 被调一次;dialog 关闭

#### Scenario: 取消不跳转
- **WHEN** dialog 关闭 with `undefined`
- **THEN** `router.navigateByUrl` MUST NOT 被调

### Requirement: `/g/:gameKey/lobby` 是受保护的懒加载单棋种大厅

Web 客户端 SHALL 提供路由 `/g/:gameKey/lobby`,懒加载(`loadComponent`)且受 `authGuard` 保护,渲染 `GameLobby` 组件。

`/g/<key>` 已经是每棋种的命名空间(`/g/xiangqi`、`/g/gomoku/leaderboard`),大厅跟着走。棋种键 MUST 来自路由参数,MUST NOT 来自任何组件常量或本地存储 —— URL 是这件事的唯一真源,这样一个大厅可以被分享、被收藏、被刷新。

页面渲染三张分棋种的卡片:**Active rooms**(含"创建房间"按钮)、**Play vs AI**、**Leaderboard**。每张的四态契约与既有约定一致。页面 MUST 显示当前棋种名(`games.<key>.title`)作为标题,否则三张卡片说不清自己属于哪个棋。

`Leaderboard` 卡 MUST 只在该棋种 `isRated` 时渲染 —— 一个永远为空的榜看起来和"没人玩过"一样,而它其实是"这个棋不计分"。

`GameLobby` 组件 MUST 通过 `providers` 提供 `LOBBY_GAME_KEY`(取自路由)与 `LobbyDataService`。

#### Scenario: 路由懒加载
- **WHEN** 用户首次导航到 `/g/gomoku/lobby`
- **THEN** 该页面的 chunk 才被加载;它 MUST NOT 出现在主 bundle 里

#### Scenario: 未登录被重定向
- **WHEN** 未登录用户访问该路由
- **THEN** 走既有鉴权守卫,重定向到登录页

#### Scenario: 棋种来自 URL
- **WHEN** 用户访问 `/g/gomoku/lobby`
- **THEN** 房间列表请求为 `GET /api/rooms?gameKey=gomoku`,创建房间的 body 中 `gameKey === 'gomoku'`

#### Scenario: 换一个棋种就是换一个 URL
- **WHEN** 用户访问 `/g/idiom-chain/lobby`
- **THEN** 同一个组件以 `idiom-chain` 取数,MUST NOT 需要任何新代码

#### Scenario: 不计分的棋种不渲染榜卡
- **WHEN** 打开一个 `isRated === false` 的棋种大厅
- **THEN** MUST NOT 渲染 Leaderboard 卡片

---

### Requirement: 无人人对战 / 未知棋种的大厅显示说明,而不是重定向

`/g/:gameKey/lobby` 在棋种未登记、或其 `supportsHumanVsHuman === false` 时 SHALL 渲染一个说明性面板并给出去处链接,MUST NOT 静默重定向。

重定向会把一个拼错的 URL 伪装成别的东西 —— 用户看到的是一个他没要求的页面,却没有任何提示说明为什么。

面板 MUST 区分两种情况:未登记的键说"本平台没有这个游戏"(链接到 `/games`);已登记但无人人对战的说"这个游戏目前只有人机对战"(链接到该棋种的 `launchRoute`)。

能力来自 `GameCapabilitiesService`。页面 MUST 在 `capabilities.loaded()` 为 false 时保持骨架 —— 沿用 `remove-manifest-board` 立下的门:**"描述符还没到"与"这个键不认识"是两件事**,把后者的界面画在前者身上,就是在用户即将得知答案的那一刻先给他一个错的。

这是**展示决定**。服务端无论客户端画什么都会拒绝为这类棋种创建人人对战房间(见 `game-rules-registry` 的强制要求),本条 MUST NOT 被当作强制手段。

#### Scenario: 未登记的键
- **WHEN** 用户访问 `/g/go/lobby`
- **THEN** 显示"本平台没有这个游戏" + 指向 `/games` 的链接;MUST NOT 发出房间列表请求;MUST NOT 重定向

#### Scenario: 只有人机的棋种
- **WHEN** 用户访问 `/g/tictactoe/lobby`
- **THEN** 显示"目前只有人机对战" + 指向 `/g/tictactoe` 的链接;MUST NOT 渲染 Active rooms 卡片

#### Scenario: 象棋不再走这条路
- **WHEN** 用户访问 `/g/xiangqi/lobby`
- **THEN** 渲染完整大厅(房间列表 + 人机卡 + 排行榜),MUST NOT 显示"目前只有人机对战"。**本场景此前正是以象棋举例的**,而象棋自 `enable-xiangqi-human-play` 起开放人人对战 —— 一字棋现在是这条路径唯一的真实用例

#### Scenario: 描述符未到时不下结论
- **WHEN** `capabilities.loaded()` 为 false
- **THEN** 页面显示骨架,MUST NOT 显示上述任何一种说明面板

### Requirement: `/home` 的游戏入口条

`/home` SHALL 渲染一条紧凑的游戏入口(`games-strip`),数据来自 `GAME_REGISTRY`,只列 `status === 'available'` 的条目,每项链接到它的 `launchRoute`。

它**不是**第二个游戏目录。`/games` 列全部八款(含规划中)、带描述与内容语言徽标;入口条只是让"登录后落地"到"进了一局"仍然是一次点击 —— 分棋种的卡片移走之后,`/home` 否则就没有任何入口了。

新增一款游戏 MUST NOT 需要改动本组件:它读注册表。

#### Scenario: 只列可玩的
- **WHEN** 注册表中有 5 款 `available`、3 款 `planned`
- **THEN** 入口条渲染 5 项

#### Scenario: 链接指向 launchRoute
- **WHEN** 渲染五子棋一项
- **THEN** 其 `href` 等于 `gomokuManifest.launchRoute`

#### Scenario: 新游戏零改动
- **WHEN** 往 `GAME_REGISTRY` 追加一条 `available` 清单
- **THEN** 入口条自动多一项,`games-strip` 的源码 MUST NOT 需要改动

---

### Requirement: i18n —— 本次新增的键双语齐备

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增:

- `lobby.game-lobby.{title, back-to-home}`
- `lobby.game-lobby.unavailable.{unknown-title, unknown-body, unknown-cta, ai-only-title, ai-only-body, ai-only-cta}`
- `lobby.games-strip.{title, all-games}`
- `lobby.my-rooms.game-label`、`lobby.recent-games.game-label`

flatten 后两份 JSON 的 key 集合 MUST 完全相等(零漂移)。

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空

#### Scenario: 模板零硬编码
- **WHEN** 在 `src/app/pages/lobby/**/*.html` 中搜索 CJK 字符或 ≥ 3 字母的显示英文字符串
- **THEN** 0 匹配(技术 test-id 等非展示字符串除外)

### Requirement: 大厅的房间行按在座玩家渲染,MUST NOT 假设两个座位

`active-rooms` 的每一行 SHALL 渲染 `room.seats` 里**全部**在座玩家,而 MUST NOT 渲染
写死的两个座位标签。渲染出的玩家链接数 MUST 等于 `seats.length`。

**行上 MUST NOT 出现颜色词。** `board-seats.ts` 的文档写着那套「座位号 → 颜色」的读法
只有棋盘家族可以调用,而一个座位数大于二的棋种没有颜色可映。大厅不是棋盘。

`my-active-rooms` 判断「我在这个房间里是什么身份」SHALL 查 `seats`,而 MUST NOT 只比
`black` / `white`。**「不在座位上」与「在第三个座位上」MUST NOT 得到同一个答案** ——
这与 `fix-three-seat-membership` 在服务端修的是同一句话。

这条 SHALL 由一条**遍历**断言强制:2 个座位与 3 个座位各走一遍,断言渲染出的人数等于
`seats.length`。写成「斗地主房间画三个人」在一个把第三个人硬编码进去的实现上同样是绿的。

375 px 的检查 SHALL 在**三个人名都在行上**时做 —— 两个人名的行过得去,三个未必,而
`generalize-lobby` 已经记过这条:一条「无横向滚动」的检查在空列表上是白过的。

#### Scenario: 三座位房间的第三个人出现在大厅行里
- **WHEN** 一行的 `seats` 有三项
- **THEN** 三个用户名都渲染出来,且都是 `/users/:id` 链接

#### Scenario: 两座位房间不因此多画东西
- **WHEN** 一行的 `seats` 有两项
- **THEN** 恰好两个用户名;行上没有颜色词

#### Scenario: 第三个座位上的人不是观战者
- **WHEN** 当前用户占着 `seats` 里 `index == 2` 的那一项
- **THEN** `my-active-rooms` 说他**在座**,而 MUST NOT 说他在观战

### Requirement: 房间行画座位,而座位总数与在座人数是两个数

Active rooms 的每一行 SHALL 渲染:该棋种的**纹章**、房间名与状态、**一排座位**、观战人数、以及操作按钮。

座位 SHALL 画满**该棋种的座位总数**:在座的画成有人的样子,其余画成空位。座位总数取自 `GET /api/games` 的 `seatCount`,而 **MUST NOT 退化成 `room.seats.length`** —— 后者是「坐上了几个」,前者是「一共有几个」,那句区别写在 `RoomSummary.seats` 的文档里。退化会把每个等待中的房间画成满座,也就是**一个看起来不能加入、其实能加入的房间**。

`seatCount` 是异步的,而大厅列表**没有整页 loading 门**(房间页侧栏有,所以它不必处理这件事)。所以「还不知道有几个座位」SHALL 是一个画得出来的状态:一个占位。它 MUST NOT 先画在座的、等描述符到达再补空位 —— 那是布局跳动。占位 MUST 是 `aria-hidden`:一个还不知道数量的占位被朗读成「空位」,比不朗读更糟,因为它在说一件没被确认的事。

**座位是叠加在名字之上,不是替换名字。** 在座玩家的 username 作为文本存在、并且是 `/users/:id` 链接,这是既有行为(`fix-lobby-seats` 立的),而座位圆片给的是另一件事:「还差不差人」在余光里可读。窄屏(< `sm`)可以把名字那行收起来,但圆片 SHALL 仍然带 `aria-label` 与 `title` 的**全名** —— 视觉上省掉的东西 MUST NOT 在语义上也省掉。

观战人数 MUST NOT 混进座位里:「3/3」读起来会变成「5 个人在玩」。

#### Scenario: 等待中的三人房画三个位子
- **WHEN** 一个 `seatCount == 3` 的房间里坐了 2 个人
- **THEN** 渲染 2 个在座座位与 1 个空位

#### Scenario: 满座房间没有空位
- **WHEN** 一个 `seatCount == 3` 的房间里坐了 3 个人
- **THEN** 渲染 3 个在座座位,空位为 0

#### Scenario: 座位总数未到达时画占位
- **WHEN** 描述符还没到达
- **THEN** 渲染占位而**不是**空位,也**不是**满座;占位 `aria-hidden="true"`

#### Scenario: 圆片只显示一个字,而全名仍然可达
- **WHEN** 渲染一个在座座位
- **THEN** 可见文本是首字,而 `aria-label` / `title` 是完整 username,`href` 指向 `/users/:id`

#### Scenario: 接线由真服务验证
- **WHEN** 用真的 `DefaultGameCapabilitiesService`(只在 HTTP 边界打桩)渲染一行
- **THEN** 描述符到达后空位补齐 —— 一个不调 `ensureLoaded()` 的实现在这条下会红,而在桩下不会

