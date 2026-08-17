# web-lobby Specification Delta

## RENAMED Requirements

四条 requirement 连**标题**一起改了 —— 大厅拆成两个页面之后,「大厅页」「四张卡片」
「LobbyDataService」这些名字说的都不再是它们描述的东西。archive 的应用顺序是
RENAMED → REMOVED → MODIFIED → ADDED,所以下面 MODIFIED 用的是新标题。

- FROM: ### Requirement: `/home` 路由是受保护的大厅页,渲染 Lobby 组件
- TO: ### Requirement: `/home` 路由是受保护的平台主页,渲染 Lobby 组件

- FROM: ### Requirement: 四张卡片的可视契约 —— loading / empty / error / data 四态
- TO: ### Requirement: `/home` 的卡片是账号范围的 —— loading / empty / error / data 四态

- FROM: ### Requirement: `LobbyDataService` —— 页面级 Signal store + 每片独立轮询
- TO: ### Requirement: 页面级 Signal store —— 两个服务共用一套轮询引擎

- FROM: ### Requirement: 大厅 "Play vs AI" 卡片提供创建 AI 对局入口
- TO: ### Requirement: "Play vs AI" 卡片提供创建 AI 对局入口

## MODIFIED Requirements

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
- **My active rooms** —— 四态齐全,data 行显示 `{ name, host, 我是 Black/White/spectator, status }` + `Resume` 按钮。empty 文案 "你目前没有进行中的对局。" 该卡 **MUST 跨棋种** —— `GET /api/users/me/active-rooms` 不带棋种键,"我此刻在哪些局里"跨棋种正是该问题的正确答案。行内 MUST 显示该局的棋种名(翻译键 `games.<key>.title`),否则跨棋种的列表读不出所以然。
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

该卡片在棋种大厅上**无条件渲染**,并且这**不是**疏漏。「这个棋种有没有 AI」由 `IGameAiRegistry.For(gameKey)` 回答,而 `game-rules-registry` 明令禁止在 `IGameRules` 上加 `SupportsAi` 之类的声明(那会是第二份真源)。要让卡片有条件渲染,得给 `GameDescriptorDto` 加一个**由注册表投影出来的** `hasAi` 字段 —— 那是合法的(与 `isRated` 投影 `IGameRules.IsRated` 同形),但它是后端改动,而且**今天没有任何消费者**:只有 `supportsHumanVsHuman === true` 的棋种才有大厅,今天那只有五子棋,而五子棋有 AI。

于是它留到第一个"有人人对战、但没有 AI"的棋种出现那天再做 —— 那时它才第一次能被真实用例检验。**为一个还不存在的情况建一个测不了的分支,正是这个仓库反复付过账的事。**

点击按钮 SHALL 打开 `CreateAiRoomDialog`(CDK Dialog),并把当前棋种键传给它。Dialog 关闭后:

- 若 `closed` emit 一个 `RoomState` → `router.navigateByUrl('/rooms/' + state.id)`,**MUST NOT** 再发任何 REST 请求。
- 若 emit `undefined`(取消)→ 不导航。

样式契约与其它大厅卡一致:`bg-surface text-text border-border rounded-card shadow-elevated`,无硬编码色值。

#### Scenario: 卡片在棋种大厅渲染
- **WHEN** 登录用户打开 `/g/gomoku/lobby`
- **THEN** 卡片网格中能找到 `ai-game` 卡片

#### Scenario: 卡片带上路由的棋种
- **WHEN** 在 `/g/gomoku/lobby` 提交该卡片的 dialog
- **THEN** `createAiRoom` 收到的第四个参数是 `'gomoku'`,MUST NOT 是任何字面量常量

#### Scenario: 创建成功后跳转
- **WHEN** 用户点按钮 → dialog 提交合法表单 → 后端回 201 + RoomStateDto
- **THEN** `router.navigateByUrl('/rooms/<roomId>')` 被调一次;dialog 关闭

#### Scenario: 取消不跳转
- **WHEN** dialog 关闭 with `undefined`
- **THEN** `router.navigateByUrl` MUST NOT 被调

## ADDED Requirements

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
- **WHEN** 用户访问 `/g/xiangqi/lobby`
- **THEN** 显示"目前只有人机对战" + 指向 `/g/xiangqi` 的链接;MUST NOT 渲染 Active rooms 卡片

#### Scenario: 描述符未到时不下结论
- **WHEN** `capabilities.loaded()` 为 false
- **THEN** 页面显示骨架,MUST NOT 显示上述任何一种说明面板

---

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
