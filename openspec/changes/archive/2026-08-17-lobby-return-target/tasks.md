# Tasks — lobby-return-target

## 1. helper

- [x] 1.1 `games/game-entry-route.ts`:`gameEntryRoute(catalog, gameKey)` → 清单的 `launchRoute`,取不到就 `PLATFORM_HOME`。
- [x] 1.2 只用 `GameCatalogService`(静态 import,同步、不会失败、不会为空),**不用** `GameCapabilitiesService` —— 后者是异步的,会给一条"要导航离开本页"的路径引入一个它不需要的 loading 门。也不需要 `supportsHumanVsHuman` 分支:`generalize-lobby` 之后清单本身就已经回答了。

## 2. 房间页

- [x] 2.1 `roomDissolved$` → `exitRoute()`。
- [x] 2.2 离开 / 解散成功 → `exitRoute()`。
- [x] 2.3 结束弹窗主按钮 → `exitRoute()`。
- [x] 2.4 `rehydrate()` 的 404 与"房间不存在"面板的链接**保持** `/home`,代码里写清为什么。

## 3. 回放页 —— 不改

- [x] 3.1 提案原本把回放页算在内。**它只有 404 分支上有一个返回链接,成功态根本没有"返回"这个入口。** 那一个链接因为和房间页那两处同一个理由,本来就该是 `/home`。

  于是回放页零改动,`web-replay` 的增量也删了。计划里写的那个链接不存在;把它"改对"的另一条路是**发明一个没人要的按钮好让 spec 成真** —— 那是让代码去迁就一份写错的规格。

## 4. 测试

- [x] 4.1 `game-entry-route.spec`:五子棋 → `/g/gomoku/lobby`;象棋 / 一字棋 → 各自的人机页;未知键 → `/home`;`null` / `undefined` / `''` → `/home`。
- [x] 4.2 遍历清单:每个 `available` 游戏都解析出一个**不是** `/home` 的去处 —— 负向断言才是关键,一份仍指向 `/home` 的清单会让这个 helper 对那个游戏静默失效。
- [x] 4.3 `room-page.spec`:离开五子棋房 → `/g/gomoku/lobby`;离开象棋房 → `/g/xiangqi`。
- [x] 4.4 解析不出棋种键时回落 `/home`。
- [x] 4.5 `dotnet`/前端全绿:**478 passed**(此前 469)。

## 5. 验证

- [x] 5.1 `npm run lint` 全绿;`npm run test:ci` 478 passed;bundle 500.34 kB(未回退)。
- [x] 5.2 浏览器实跑(真 API、真房间):

  | 操作 | 落在 |
  | --- | --- |
  | 离开五子棋人机房 | `/g/gomoku/lobby` |
  | 离开象棋人机房 | `/g/xiangqi` |
  | 打开不存在的房间 | 停在 `/rooms/<id>`,面板链接 `href="/home"` |

- [x] 5.3 后端零改动。

## 6. 两处我搞错了,记下来

- [x] 6.1 **提案说"五个调用点",实际是三个。** 另外两个恰好在房间**没能加载**时触发(初次加载 404、"房间不存在"面板的链接),那里读不到棋种键 —— 想从一个不存在的房间对象上取字段来决定去哪,本身就说不通。
- [x] 6.2 **404 那条导航不在初次加载路径上,在 `rehydrate()` 里。** 初次加载 404 是渲染面板,不导航;只有重连后发现房间没了才 `navigateByUrl`。第一版测试打的是错的那条路径,一直不通过 —— 它没通过是对的。改成驱动"reconnecting → connected"来触发真正那条。

## 7. 记下但不做

- [x] 7.1 `game.ended.back-to-lobby` 的文案不改。象棋的目标是人机设置页而不是房间列表,叫它"大厅"略有出入;改键要动好几份 web spec 的 i18n 条文,而从玩家角度那一页确实就是"再来一局"的地方。
