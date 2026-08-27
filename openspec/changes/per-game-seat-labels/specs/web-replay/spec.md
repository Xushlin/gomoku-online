## MODIFIED Requirements

### Requirement: 标题区元信息使用用户名链接组件

ReplayPage 标题区 SHALL 渲染:

- 房间名(纯文本)
- 两位玩家,**席位名由该棋种的 manifest 给**(象棋读作「红方 / 黑方」),
  username 是 `<a [routerLink]="['/users', <id>]" class="username-link">`。
  没声明席位名的棋种说座位号。
- 状态徽章:`endReason` 翻译(`game.ended.reason-connected-5` / `.reason-resigned` / `.reason-timeout`)
- 结束时间(`endedAt`,通过 Angular `formatDate` 按当前 locale 显示)

#### Scenario: 用户名是链接
- **WHEN** 渲染标题区
- **THEN** 两位玩家的 username 文本是 `<a>`,`href` 解析为 `/users/<userId>`;有 `username-link` class

#### Scenario: 象棋回放说红黑
- **WHEN** 渲染一局象棋的回放标题区
- **THEN** MUST 说「红方 / 黑方」;MUST NOT 出现「白方」

