## ADDED Requirements

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
