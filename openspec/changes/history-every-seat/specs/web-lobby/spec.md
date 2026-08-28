## MODIFIED Requirements

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
    - **对手们** —— `seats` 里除本人以外的每一个座位,各一个 username 链接
      (`[routerLink]="['/users', <id>]"` + `class="username-link"` + `(click)="$event.stopPropagation()"`)。
      数量由数据决定,MUST NOT 写死一个 —— 三人局有两个对手。
    - "我方视角"结果:与 `web-user-profile` **同一套四支判据**(含说不出时的
      `profile.result-unrecorded`)。两处 MUST 给同一局对局同一个答案 ——
      它们读的是同一个 DTO,一处说「负」另一处说「说不出」是自相矛盾。
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

#### Scenario: 三人局在大厅卡片里也列出两个对手
- **WHEN** 最近 5 局里有一局三座位对局
- **THEN** 那一行**恰好**两个对手链接;结果那一格与个人主页对同一局给出同一个翻译键

