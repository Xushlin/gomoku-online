# web-game-board Specification Delta

## ADDED Requirements

### Requirement: 离开房间回到该棋种的入口,而不是平台主页

前端 SHALL 提供一个纯函数 `gameEntryRoute(catalog, gameKey)`,返回该棋种在客户端清单里的 `launchRoute`;棋种键为空、或清单里没有这个键时返回 `/home`。

所有"离开这局"的导航 MUST 走它,MUST NOT 硬编码 `/home`:

- `roomDissolved$` 事件
- 离开 / 解散成功回调
- 结束弹窗的主按钮

判据只用**客户端清单**,不用 `GET /api/games` 的能力描述符。`generalize-lobby` 之后五子棋的 `launchRoute` 就是它的大厅,所以清单已经回答了这个问题;而清单是静态 import —— 同步、不会失败、不会为空,于是这条路径 MUST NOT 引入任何 loading 门。

没有大厅的棋种(`supportsHumanVsHuman == false`)因此回到它自己的人机页面。那对玩家是对的:那一页就是他再来一局的地方。

**下面两种情况 MUST 仍然去 `/home`**,而且不是遗漏:初次加载 404、以及"房间不存在"面板上的链接。那两处房间**没有加载成功**,不存在可读的棋种键 —— 去房间对象上取一个字段来决定去哪,前提是那个房间存在。

#### Scenario: 五子棋房间离开后回大厅
- **WHEN** 玩家在一个 `gameKey === 'gomoku'` 的房间点离开且后端回 204
- **THEN** navigate 到 `/g/gomoku/lobby`,MUST NOT 是 `/home`

#### Scenario: 没有大厅的棋种回自己的入口页
- **WHEN** 玩家离开一个 `gameKey === 'xiangqi'` 的人机房间
- **THEN** navigate 到 `/g/xiangqi`(该清单的 `launchRoute`)

#### Scenario: 客户端不认识的棋种回主页
- **WHEN** 房间的 `gameKey` 在客户端清单里不存在(服务端比客户端新)
- **THEN** navigate 到 `/home`,MUST NOT 导航到一条拼出来的 `/g/<key>/lobby`

#### Scenario: 房间根本没加载出来时回主页
- **WHEN** `GET /api/rooms/{id}` 回 404,或用户点"房间不存在"面板上的链接
- **THEN** navigate 到 `/home` —— 此时没有棋种键可读

#### Scenario: 房间解散广播
- **WHEN** 房间被 host 解散,`roomDissolved$` 触发
- **THEN** 房内每个连接都 navigate 到该棋种的入口路由

#### Scenario: 结束弹窗主按钮
- **WHEN** 结束弹窗中点 `game.ended.back-to-lobby`
- **THEN** navigate 到该棋种的入口路由;dialog 关闭;RoomPage 被销毁

#### Scenario: 不再有硬编码的 `/home`
- **WHEN** 在 `pages/rooms/room-page/` 下搜索字面量 `'/home'`
- **THEN** 只允许出现在上述"房间未加载"的两处
