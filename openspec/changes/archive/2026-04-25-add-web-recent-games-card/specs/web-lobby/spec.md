## ADDED Requirements

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
